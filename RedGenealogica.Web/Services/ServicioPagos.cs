using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using RedGenealogica.Web.Services;
namespace RedGenealogica.Web.Services;

public class ServicioPagos
{
    private readonly ContextoAplicacion _contexto;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly ServicioReferidos _servicioReferidos;

    public ServicioPagos(
    ContextoAplicacion contexto,
    IConfiguration configuration,
    ServicioReferidos servicioReferidos)
    {
        _contexto = contexto;
        _configuration = configuration;
        _servicioReferidos = servicioReferidos;
        _http = new HttpClient();
    }

    public async Task<Pago> CrearPagoSimuladoAsync(int usuarioId, int productoId, decimal monto)
    {
        var usuario = await _contexto.Users.FirstOrDefaultAsync(x => x.Id == usuarioId);

        if (usuario == null)
            throw new Exception("Usuario no encontrado");

        var pago = new Pago
        {
            UsuarioId = usuarioId,
            ProductoId = productoId,
            Monto = monto,
            EstadoPago = EstadoPago.Aprobado,
            EsSimulado = true,
            NombreCuentaEnmascarado = "CUENTA_TEST_****",
            FechaConfirmacion = DateTime.UtcNow
        };

        _contexto.Pagos.Add(pago);

        // 🔥 ACTIVAR USUARIO
        usuario.EstadoUsuario = EstadoUsuario.Activo;
        usuario.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return pago;
    }

    private readonly Dictionary<int, decimal> niveles = new()
    {
        { 1, 0.10m }, // 10%
        { 2, 0.05m }, // 5%
        { 3, 0.02m }  // 2%
    };

    public async Task GenerarComisiones(int usuarioId, decimal montoBase)
    {
        int nivelActual = 1;
        int? usuarioActualId = usuarioId;

        while (usuarioActualId != null && niveles.ContainsKey(nivelActual))
        {
            // 🔍 buscar padre
            var usuario = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioActualId);

            if (usuario?.IdUsuarioPadre == null)
                break;

            var padre = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuario.IdUsuarioPadre);

            if (padre == null)
                break;

            var porcentaje = niveles[nivelActual];
            var comision = montoBase * porcentaje;

            var existe = await _contexto.MovimientosPuntos
                .AnyAsync(x => x.UsuarioId == padre.Id
                            && x.Nivel == nivelActual
                            && x.Motivo.Contains("Comisión"));

            if (existe)
            {
                usuarioActualId = padre.Id;
                nivelActual++;
                continue;
            }
            // 💰 guardar comisión
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = padre.Id,
                Monto = comision,
                Motivo = $"Comisión nivel {nivelActual}",
                Nivel = nivelActual,
                FechaMovimiento = DateTime.UtcNow
            });

            // subir nivel
            usuarioActualId = padre.Id;
            nivelActual++;
        }

        await _contexto.SaveChangesAsync();
    }

    public async Task ConfirmarPago(int referidoId)
    {
        using var transaccion = await _contexto.Database.BeginTransactionAsync();

        try
        {
            var referido = await _contexto.Referidos
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.Id == referidoId);

            if (referido == null)
                return;

            // 🔒 VALIDACIÓN FUERTE
            if (referido.Estado == EstadoUsuario.Activo)
            {
                await transaccion.RollbackAsync();
                return;
            }
            referido.PagoConfirmado = true;
            // 🟢 activar referido
            referido.Estado = EstadoUsuario.Activo;
            referido.FechaActivacion = DateTime.UtcNow;

            // 🎯 puntos (solo una vez)
            referido.Usuario!.PuntosAcumulados += 100;

            await _contexto.SaveChangesAsync();

            // 💰 comisiones (solo después de guardar)
            await GenerarComisiones(referido.UsuarioId, 100);

            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    public async Task<string> CrearPreferencia(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null)
            throw new Exception("Referido no encontrado");

        var accessToken = _configuration["MercadoPago:AccessToken"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            items = new[]
            {
                new {
                    title = referido.Producto.Nombre,
                    quantity = 1,
                    unit_price = referido.Producto.Precio
                }
            },
            back_urls = new
            {
                success = "https://carpometacarpal-tabitha-timocratical.ngrok-free.dev/Pagos/Exito",
                failure = "https://carpometacarpal-tabitha-timocratical.ngrok-free.dev/Pagos/Error",
                pending = "https://carpometacarpal-tabitha-timocratical.ngrok-free.dev/Pagos/Pendiente"
            },
            auto_return = "approved",
            notification_url = "https://carpometacarpal-tabitha-timocratical.ngrok-free.dev/Pagos/Webhook",
            external_reference = referidoId.ToString()
        };

        var json = JsonSerializer.Serialize(body);

        var response = await _http.PostAsync(
            "https://api.mercadopago.com/checkout/preferences",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Error MercadoPago: " + content);
        }

        var result = JsonDocument.Parse(content);

        if (!result.RootElement.TryGetProperty("init_point", out var initPoint))
        {
            throw new Exception("Respuesta inválida MP: " + content);
        }

        return initPoint.GetString()!;
    }

    public async Task<bool> ProcesarWebhookPagoAsync(string idPago)
    {
        // 🔹 Evitar duplicados
        var yaProcesado = await _contexto.RegistrosWebhook
            .AnyAsync(x => x.IdPago == idPago);

        if (yaProcesado)
            return false;

        var accessToken = _configuration["MercadoPago:AccessToken"];

        var cliente = new HttpClient();
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

        if (status != "approved")
            return false;

        var externalReference = paymentJson.RootElement
            .GetProperty("external_reference")
            .GetString();

        if (string.IsNullOrEmpty(externalReference))
            return false;

        int referidoId = int.Parse(externalReference);

        // 🔹 Confirmar pago
        await ConfirmarPago(referidoId);
        // 🔥 convertir referido a usuario
        await _servicioReferidos.ConvertirReferidoAUsuarioAsync(referidoId);
        // 🔹 Guardar log (idempotencia)
        _contexto.RegistrosWebhook.Add(new RegistroWebhook
        {
            IdPago = idPago,
            Estado = status!,
            FechaRegistro = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync();

        return true;
    }
}