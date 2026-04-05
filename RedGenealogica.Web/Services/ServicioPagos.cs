// ============================================================
// ServicioPagos.cs
// Ubicación: Services/ServicioPagos.cs
//
// CAMBIO: integración con ServicioNotificaciones.
// Se disparan notificaciones automáticas en ConfirmarPago:
//   - Al referidor: "Tu referido pagó"
//   - Al referidor: "Ganaste comisión" (si aplica)
//   - Al referidor: "Subiste de rango" (si subió)
//   - A cada ancestro que recibe comisión en GenerarComisiones
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
    private readonly ServicioNotificaciones _servicioNotificaciones;

    private readonly ServicioPremios _servicioPremios;

    public ServicioPagos(
        ContextoAplicacion contexto,
        IConfiguration configuration,
        ServicioReferidos servicioReferidos,
        ServicioRangos servicioRangos,
        ServicioNotificaciones servicioNotificaciones,
        ServicioPremios servicioPremios)
    {
        _contexto = contexto;
        _configuration = configuration;
        _servicioReferidos = servicioReferidos;
        _servicioRangos = servicioRangos;
        _servicioNotificaciones = servicioNotificaciones;
        _servicioPremios = servicioPremios;
    }

    

    // ----------------------------------------------------------------
    // Confirma el pago de un referido y dispara todas las notificaciones.
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

            if (referido == null) { await transaccion.RollbackAsync(); return; }
            if (referido.PagoConfirmado) { await transaccion.RollbackAsync(); return; }

            referido.PagoConfirmado = true;
            referido.Estado         = EstadoReferido.Pagado;
            referido.FechaActivacion = DateTime.UtcNow;

            var referidor    = referido.Usuario!;
            var rangoAnterior = referidor.TipoRangoActual;
            var eraActivo    = referidor.EstadoUsuario == EstadoUsuario.Activo;

            // Activar al referidor si estaba Pendiente
            if (!eraActivo)
            {
                referidor.EstadoUsuario  = EstadoUsuario.Activo;
                referidor.FechaActivacion = DateTime.UtcNow;
            }

            referidor.PuntosAcumulados += 100;
            referidor.TipoRangoActual   = await _servicioRangos.ObtenerRangoAsync(referidor.PuntosAcumulados);

            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId      = referidor.Id,
                CantidadPuntos = 100,
                Monto          = 0m,
                Motivo         = $"Referido activado — {referido.NombreCompleto}",
                ReferidoId     = referido.Id,
                Nivel          = 0,
                FechaMovimiento = DateTime.UtcNow
            });

            await _contexto.SaveChangesAsync();

            // Notificación: referido pagó
            await _servicioNotificaciones.CrearAsync(
                referidor.Id,
                TipoNotificacion.ReferidoPago,
                "✅ Tu referido pagó",
                $"{referido.NombreCompleto} completó el pago de {referido.Producto?.Nombre}. Ganaste 100 puntos.",
                "/Referidos/MisReferidos"
            );

            // Notificación: cuenta activada (solo si estaba pendiente)
            if (!eraActivo)
            {
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id,
                    TipoNotificacion.Sistema,
                    "🎉 ¡Tu cuenta está activa!",
                    "Tu cuenta fue activada. Ya podés registrar más referidos y ganar comisiones.",
                    "/Usuario/Panel"
                );
            }

            // Notificación: subida de rango del referidor
            if (referidor.TipoRangoActual != rangoAnterior)
            {
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id,
                    TipoNotificacion.SubidaDeRango,
                    "🏆 ¡Subiste de rango!",
                    $"Felicitaciones, ahora sos {referidor.TipoRangoActual}. Tus comisiones aumentaron.",
                    "/Usuario/Panel"
                );
            }

            // Generar comisiones para ancestros (con sus propias notificaciones)
            await _servicioPremios.ProcesarPagoReferidoAsync(referido.Id);

            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    // ----------------------------------------------------------------
    // Crea preferencia de pago en MercadoPago.
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
                    title     = referido.Producto!.Nombre,
                    quantity  = 1,
                    unit_price = referido.Producto.Precio
                }
            },
            payer            = new { email = "test_user_123@testuser.com" },
            back_urls        = new
            {
                success  = $"{baseUrl}/Pagos/Exito?ok=1",
                failure  = $"{baseUrl}/Pagos/Error",
                pending  = $"{baseUrl}/Pagos/Pendiente"
            },
            auto_return      = "approved",
            notification_url = $"{baseUrl}/Pagos/Webhook",
            external_reference = referidoId.ToString(),
            metadata         = new { referido_id = referidoId }
        };

        var json     = JsonSerializer.Serialize(body);
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
    // ----------------------------------------------------------------
    public async Task<bool> ProcesarWebhookPagoAsync(string idPago)
    {
        var yaProcesado = await _contexto.RegistrosWebhook
            .AnyAsync(x => x.IdPago == idPago);

        if (yaProcesado) return false;

        var accessToken = _configuration["MercadoPago:AccessToken"]
            ?? throw new Exception("Token de MercadoPago no configurado");

        using var cliente = new HttpClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await cliente.GetAsync($"https://api.mercadopago.com/v1/payments/{idPago}");
        if (!response.IsSuccessStatusCode)
            throw new Exception("Error al consultar MercadoPago");

        var content     = await response.Content.ReadAsStringAsync();
        var paymentJson = JsonDocument.Parse(content);
        var status      = paymentJson.RootElement.GetProperty("status").GetString();

        if (status != "approved") return false;

        var externalReference = paymentJson.RootElement
            .GetProperty("external_reference").GetString();

        if (string.IsNullOrEmpty(externalReference)) return false;

        int referidoId = int.Parse(externalReference);
        var referido   = await _contexto.Referidos.FindAsync(referidoId);
        if (referido == null) return false;

        await ConfirmarPago(referidoId);

        _contexto.RegistrosWebhook.Add(new RegistroWebhook
        {
            IdPago        = idPago,
            Estado        = status,
            FechaRegistro = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync();
        return true;
    }
}
