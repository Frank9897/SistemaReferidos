// ============================================================
// ServicioPagos.cs
// Ubicación: Services/ServicioPagos.cs
//
// CAMBIOS PRINCIPALES:
//
//   [NUEVO] Activación automática de A cuando B paga:
//     ConfirmarPago ahora verifica si el referidor (A) está Pendiente.
//     Si es así, lo activa automáticamente. A no paga nada — se activa
//     cuando su primer referido paga el producto.
//
//   [NUEVO] Comisiones calculadas sobre precio real del producto:
//     GenerarComisiones usa los porcentajes de Producto.ComisionNivelX
//     multiplicados por el BonusComisionPorcentaje del rango del receptor.
//     Fórmula: comision = precio * (porcentajeProducto/100) * (1 + bonus/100)
//
//   [NUEVO] SaldoDisponible: cada comisión acredita dinero real en
//     el campo Usuario.SaldoDisponible, además de puntos de ranking.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RedGenealogica.Web.Services;

public class ServicioPagos
{
    private readonly ContextoAplicacion _contexto;
    private readonly IConfiguration _configuration;
    private readonly ServicioReferidos _servicioReferidos;
    private readonly ServicioRangos _servicioRangos;

    public ServicioPagos(
        ContextoAplicacion contexto,
        IConfiguration configuration,
        ServicioReferidos servicioReferidos,
        ServicioRangos servicioRangos)
    {
        _contexto = contexto;
        _configuration = configuration;
        _servicioReferidos = servicioReferidos;
        _servicioRangos = servicioRangos;
    }

    // ----------------------------------------------------------------
    // [NUEVO] GenerarComisiones
    //
    // Calcula la comisión de cada ancestro según:
    //   1. El porcentaje del producto para ese nivel (ComisionNivelX)
    //   2. El bonus de rango del receptor (BonusComisionPorcentaje)
    //
    // Fórmula: comision = precioProducto * (pctNivel/100) * (1 + bonus/100)
    //
    // Ejemplo: Switch $100, nivel 1 = 10%, receptor Oro (bonus 40%)
    //   comision = $100 * 0.10 * 1.40 = $14
    //
    // Además de acreditar dinero (SaldoDisponible), suma puntos de
    // ranking proporcionales para el leaderboard.
    // ----------------------------------------------------------------
    public async Task GenerarComisiones(int referidoId, int usuarioOrigenId, Producto producto)
    {
        // Porcentajes base por nivel definidos en el producto
        var porcentajesPorNivel = new Dictionary<int, decimal>
        {
            { 1, producto.ComisionNivel1Porcentaje },
            { 2, producto.ComisionNivel2Porcentaje },
            { 3, producto.ComisionNivel3Porcentaje }
        };

        int nivelActual = 1;
        int? usuarioActualId = usuarioOrigenId;

        while (usuarioActualId != null && porcentajesPorNivel.ContainsKey(nivelActual))
        {
            var usuario = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioActualId);

            if (usuario?.IdUsuarioPadre == null)
                break;

            var padre = await _contexto.Users
                .Include(u => u.MovimientosPuntos)
                .FirstOrDefaultAsync(u => u.Id == usuario.IdUsuarioPadre);

            if (padre == null)
                break;

            // Idempotencia: no pagar dos veces la misma comisión
            var yaExiste = await _contexto.MovimientosPuntos
                .AnyAsync(x =>
                    x.UsuarioId == padre.Id &&
                    x.ReferidoId == referidoId &&
                    x.Nivel == nivelActual);

            if (yaExiste)
            {
                usuarioActualId = padre.Id;
                nivelActual++;
                continue;
            }

            // Obtener el bonus de comisión según el rango actual del padre
            var rangoInfo = await _contexto.RangosUsuario
                .FirstOrDefaultAsync(r => r.TipoRango == padre.TipoRangoActual && r.Activo);

            var bonusPorcentaje = rangoInfo?.BonusComisionPorcentaje ?? 0m;
            var pctBase = porcentajesPorNivel[nivelActual];

            // Comisión en dinero real: precio del producto * % nivel * multiplicador de rango
            var comisionDinero = Math.Round(
                producto.Precio * (pctBase / 100m) * (1m + bonusPorcentaje / 100m),
                2);

            // Puntos de ranking: 1 punto por cada peso de comisión (redondeado)
            var puntosGanados = (int)Math.Floor(comisionDinero);

            // Registrar movimiento con todos los datos para auditoría
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = padre.Id,
                Monto = comisionDinero,
                CantidadPuntos = puntosGanados,
                Motivo = $"Comisión nivel {nivelActual} — {producto.Nombre}",
                ReferidoId = referidoId,
                Nivel = nivelActual,
                FechaMovimiento = DateTime.UtcNow
            });

            // Acreditar dinero real al saldo del padre (retirable)
            padre.SaldoDisponible += comisionDinero;

            // Acumular puntos de ranking y recalcular rango
            padre.PuntosAcumulados += puntosGanados;
            padre.TipoRangoActual = await _servicioRangos.ObtenerRangoAsync(padre.PuntosAcumulados);

            usuarioActualId = padre.Id;
            nivelActual++;
        }

        await _contexto.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    // [ACTUALIZADO] ConfirmarPago
    //
    // Flujo completo cuando B paga el producto:
    //   1. Marca el referido como Pagado (idempotencia con PagoConfirmado)
    //   2. [NUEVO] Activa a A automáticamente si estaba Pendiente
    //      (A se activa con el primer pago de cualquiera de sus referidos)
    //   3. Suma 100 puntos de ranking a A por referido activado
    //   4. Genera comisiones en dinero para A y sus ancestros
    // ----------------------------------------------------------------
    public async Task ConfirmarPago(int referidoId)
    {
        using var transaccion = await _contexto.Database.BeginTransactionAsync();

        try
        {
            var referido = await _contexto.Referidos
                .Include(r => r.Usuario)
                .Include(r => r.Producto)
                .FirstOrDefaultAsync(r => r.Id == referidoId);

            if (referido == null)
            {
                await transaccion.RollbackAsync();
                return;
            }

            // Guard de idempotencia: protege contra webhooks duplicados
            if (referido.PagoConfirmado)
            {
                await transaccion.RollbackAsync();
                return;
            }

            // Marca como confirmado antes de cualquier otra operación
            referido.PagoConfirmado = true;
            referido.Estado = EstadoReferido.Pagado;
            referido.FechaActivacion = DateTime.UtcNow;

            var referidor = referido.Usuario!;

            // [NUEVO] Activa al referidor (A) automáticamente si estaba Pendiente.
            // A no paga nada: se activa cuando su primer referido completa el pago.
            if (referidor.EstadoUsuario == EstadoUsuario.Pendiente)
            {
                referidor.EstadoUsuario = EstadoUsuario.Activo;
                referidor.FechaActivacion = DateTime.UtcNow;
            }

            // Suma 100 puntos de ranking a A por tener un referido que pagó
            referidor.PuntosAcumulados += 100;
            referidor.TipoRangoActual =
                await _servicioRangos.ObtenerRangoAsync(referidor.PuntosAcumulados);

            // Registra el movimiento de puntos (no dinero, solo ranking)
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = referidor.Id,
                CantidadPuntos = 100,
                Monto = 0m,
                Motivo = $"Referido activado — {referido.NombreCompleto}",
                ReferidoId = referido.Id,
                Nivel = 0,
                FechaMovimiento = DateTime.UtcNow
            });

            await _contexto.SaveChangesAsync();

            // Genera comisiones en dinero para A y sus ancestros (niveles 1-3)
            // Usa el producto real del referido para calcular los montos
            await GenerarComisiones(referido.Id, referidor.Id, referido.Producto!);

            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    // ----------------------------------------------------------------
    // Crea preferencia de pago en MercadoPago y devuelve la URL de checkout.
    // ----------------------------------------------------------------
    public async Task<string> CrearPreferencia(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.Id == referidoId)
            ?? throw new Exception("Referido no encontrado");

        var accessToken = _configuration["MercadoPago:AccessToken"]
            ?? throw new Exception("Token de MercadoPago no configurado");

        var baseUrl = _configuration["App:BaseUrl"]
            ?? throw new Exception("BaseUrl no configurado en appsettings");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            items = new[]
            {
                new
                {
                    title = referido.Producto!.Nombre,
                    quantity = 1,
                    unit_price = referido.Producto.Precio
                }
            },
            payer = new { email = "test_user_123@testuser.com" },
            back_urls = new
            {
                success = $"{baseUrl}/Pagos/Exito",
                failure = $"{baseUrl}/Pagos/Error",
                pending = $"{baseUrl}/Pagos/Pendiente"
            },
            auto_return = "approved",
            notification_url = $"{baseUrl}/Pagos/Webhook",
            external_reference = referidoId.ToString(),
            metadata = new { referido_id = referidoId }
        };

        var json = JsonSerializer.Serialize(body);
        var response = await http.PostAsync(
            "https://api.mercadopago.com/checkout/preferences",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception("Error MercadoPago: " + content);

        var result = JsonDocument.Parse(content);

        if (!result.RootElement.TryGetProperty("init_point", out var initPoint))
            throw new Exception("Respuesta inválida MP: " + content);

        return initPoint.GetString()!;
    }

    // ----------------------------------------------------------------
    // Procesa el webhook de MercadoPago.
    // Llama a ConfirmarPago y luego convierte el referido en usuario.
    // ----------------------------------------------------------------
    public async Task<bool> ProcesarWebhookPagoAsync(string idPago)
    {
        var yaProcesado = await _contexto.RegistrosWebhook
            .AnyAsync(x => x.IdPago == idPago);

        if (yaProcesado)
            return false;

        var accessToken = _configuration["MercadoPago:AccessToken"]
            ?? throw new Exception("Token de MercadoPago no configurado");

        using var cliente = new HttpClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await cliente.GetAsync($"https://api.mercadopago.com/v1/payments/{idPago}");

        if (!response.IsSuccessStatusCode)
            throw new Exception("Error al consultar MercadoPago");

        var content = await response.Content.ReadAsStringAsync();
        var paymentJson = JsonDocument.Parse(content);

        var status = paymentJson.RootElement.GetProperty("status").GetString();

        if (status != "approved")
            return false;

        var externalReference = paymentJson.RootElement
            .GetProperty("external_reference").GetString();

        if (string.IsNullOrEmpty(externalReference))
            return false;

        int referidoId = int.Parse(externalReference);

        var referido = await _contexto.Referidos.FindAsync(referidoId);
        if (referido == null)
            return false;

        await ConfirmarPago(referidoId);

        // El referido queda como Pagado. Solo se convierte a usuario
        // cuando el admin lo decide desde el panel (flujo manual intencional)
        // await _servicioReferidos.ConvertirReferidoAUsuarioAsync(referidoId);

        _contexto.RegistrosWebhook.Add(new RegistroWebhook
        {
            IdPago = idPago,
            Estado = status,
            FechaRegistro = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync();

        return true;
    }
}
