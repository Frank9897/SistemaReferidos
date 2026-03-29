// ============================================================
// ServicioPagos.cs
// Ubicación: Services/ServicioPagos.cs
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones; // incluye EstadoReferido y EstadoPago
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

    // [MEJORA] Porcentajes por nivel configurables desde appsettings.
    // Si no se define en config, usa los valores por defecto: 10%, 5%, 2%.
    private readonly Dictionary<int, decimal> _niveles;

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

        // Carga porcentajes desde config o usa defaults
        _niveles = new Dictionary<int, decimal>
        {
            { 1, configuration.GetValue<decimal>("Comisiones:Nivel1", 0.10m) },
            { 2, configuration.GetValue<decimal>("Comisiones:Nivel2", 0.05m) },
            { 3, configuration.GetValue<decimal>("Comisiones:Nivel3", 0.02m) }
        };
    }

    // ----------------------------------------------------------------
    // Crea un pago simulado para pruebas internas sin pasar por MP.
    // Solo debe usarse en entorno de desarrollo.
    // ----------------------------------------------------------------
    public async Task<Pago> CrearPagoSimuladoAsync(int usuarioId, int productoId, decimal monto)
    {
        var usuario = await _contexto.Users.FirstOrDefaultAsync(x => x.Id == usuarioId)
            ?? throw new Exception("Usuario no encontrado");

        var pago = new Pago
        {
            UsuarioId = usuarioId,
            ProductoId = productoId,
            Monto = monto,
            EstadoPago = EstadoPago.Aprobado,
            EsSimulado = true,
            NombreCuentaEnmascarado = "CUENTA_TEST_****",
            FechaConfirmacion = DateTime.UtcNow,
            Confirmado = true
        };

        _contexto.Pagos.Add(pago);

        // Activa el usuario directamente (flujo simulado, sin referido)
        usuario.EstadoUsuario = EstadoUsuario.Activo;
        usuario.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return pago;
    }

    // ----------------------------------------------------------------
    // [BUG-3 CORREGIDO] GenerarComisiones
    //
    // Sube el árbol hasta 3 niveles pagando comisión a cada padre.
    // ANTES: solo guardaba MovimientoPuntos.Monto pero nunca actualizaba
    //        PuntosAcumulados ni TipoRangoActual del padre. Los padres
    //        nunca subían de rango por comisiones recibidas.
    //
    // AHORA: cada padre recibe puntos proporcionales a la comisión y
    //        se recalcula su rango inmediatamente.
    //
    // [BUG-1 CORREGIDO] La deduplicación ahora verifica por referidoId
    //        específico en lugar de buscar por texto del motivo.
    // ----------------------------------------------------------------
    public async Task GenerarComisiones(int referidoId, int usuarioOrigenId, decimal montoBase)
    {
        int nivelActual = 1;
        int? usuarioActualId = usuarioOrigenId;

        while (usuarioActualId != null && _niveles.ContainsKey(nivelActual))
        {
            // Busca el padre del usuario actual en el árbol
            var usuario = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioActualId);

            if (usuario?.IdUsuarioPadre == null)
                break;

            var padre = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuario.IdUsuarioPadre);

            if (padre == null)
                break;

            // [BUG-1 CORREGIDO] Verifica que no se haya pagado ya esta comisión
            // para este referido específico en este nivel (no por texto genérico)
            var yaExiste = await _contexto.MovimientosPuntos
                .AnyAsync(x =>
                    x.UsuarioId == padre.Id &&
                    x.ReferidoId == referidoId &&
                    x.Nivel == nivelActual);

            if (yaExiste)
            {
                // Ya cobró esta comisión, saltar sin sumar puntos
                usuarioActualId = padre.Id;
                nivelActual++;
                continue;
            }

            var porcentaje = _niveles[nivelActual];
            var comision = Math.Round(montoBase * porcentaje, 2);

            // Puntos de ranking: 1 punto por cada peso de comisión (ajustable)
            var puntosGanados = (int)Math.Floor(comision);

            // Registra el movimiento con referidoId para idempotencia correcta
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = padre.Id,
                Monto = comision,
                CantidadPuntos = puntosGanados,
                Motivo = $"Comisión nivel {nivelActual}",
                ReferidoId = referidoId,       // <-- clave para deduplicar
                Nivel = nivelActual,
                FechaMovimiento = DateTime.UtcNow
            });

            // [BUG-3 CORREGIDO] Acumula puntos al padre y recalcula su rango
            padre.PuntosAcumulados += puntosGanados;
            padre.TipoRangoActual = await _servicioRangos.ObtenerRangoAsync(padre.PuntosAcumulados);

            // Sube un nivel en el árbol
            usuarioActualId = padre.Id;
            nivelActual++;
        }

        await _contexto.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    // [BUG-2 CORREGIDO] ConfirmarPago
    //
    // ANTES: llamaba a ActivarReferidoAsync que sumaba 100 puntos Y además
    //        aquí también se sumaban puntos → doble suma garantizada.
    //
    // AHORA: toda la lógica de activación está centralizada aquí.
    //        La flag PagoConfirmado es el guard principal para idempotencia.
    //        Solo el webhook puede confirmar un pago.
    // ----------------------------------------------------------------
    public async Task ConfirmarPago(int referidoId)
    {
        using var transaccion = await _contexto.Database.BeginTransactionAsync();

        try
        {
            var referido = await _contexto.Referidos
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.Id == referidoId);

            if (referido == null)
            {
                await transaccion.RollbackAsync();
                return;
            }

            // Guard de idempotencia: si ya fue confirmado, no hace nada
            // Esto protege contra webhooks duplicados o doble llamada manual
            if (referido.PagoConfirmado)
            {
                await transaccion.RollbackAsync();
                return;
            }

            // Marca el pago como confirmado (flag atómico de idempotencia)
            referido.PagoConfirmado = true;

            // Activa el referido
            referido.Estado = EstadoReferido.Pagado;   // [CORREGIDO] era EstadoUsuario.Activo
            referido.FechaActivacion = DateTime.UtcNow;

            // Suma 100 puntos al usuario que refirió (una sola vez, protegido por PagoConfirmado)
            referido.Usuario!.PuntosAcumulados += 100;

            // Registra el movimiento de puntos por referido activado
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = referido.UsuarioId,
                CantidadPuntos = 100,
                Monto = 0,
                Motivo = "Referido activado",
                ReferidoId = referido.Id,
                Nivel = 0,
                FechaMovimiento = DateTime.UtcNow
            });

            // Recalcula el rango del referidor después de sumar los puntos
            referido.Usuario.TipoRangoActual =
                await _servicioRangos.ObtenerRangoAsync(referido.Usuario.PuntosAcumulados);

            await _contexto.SaveChangesAsync();

            // Genera comisiones para los ancestros en el árbol (niveles 1-3)
            await GenerarComisiones(referido.Id, referido.UsuarioId, 100);

            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    // ----------------------------------------------------------------
    // Crea una preferencia de pago en MercadoPago y devuelve la URL
    // de checkout (init_point) para redirigir al usuario.
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
            payer = new
            {
                email = "test_user_123@testuser.com"
            },
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
    // Procesa el webhook entrante de MercadoPago.
    // Solo activa el referido si el pago está aprobado y no fue procesado.
    // El RegistroWebhook garantiza idempotencia a nivel de evento MP.
    // ----------------------------------------------------------------
    public async Task<bool> ProcesarWebhookPagoAsync(string idPago)
    {
        // Idempotencia: si este idPago ya fue procesado, ignorar
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

        var status = paymentJson.RootElement
            .GetProperty("status")
            .GetString();

        // Solo procesar pagos aprobados
        if (status != "approved")
            return false;

        var externalReference = paymentJson.RootElement
            .GetProperty("external_reference")
            .GetString();

        if (string.IsNullOrEmpty(externalReference))
            return false;

        int referidoId = int.Parse(externalReference);

        var referido = await _contexto.Referidos.FindAsync(referidoId);
        if (referido == null)
            return false;

        // Confirma el pago (activa referido, suma puntos, genera comisiones)
        await ConfirmarPago(referidoId);

        // Convierte el referido en usuario del sistema
        await _servicioReferidos.ConvertirReferidoAUsuarioAsync(referidoId);

        // Registra el webhook para evitar reprocesamiento
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
