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
    private readonly HttpClient _http;

    public ServicioPagos(ContextoAplicacion contexto, IConfiguration configuration)
    {
        _contexto = contexto;
        _configuration = configuration;
        _http = new HttpClient();
    }

    public async Task<Pago> CrearPagoYActivarUsuarioAsync(int usuarioId, int productoId, decimal monto)
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
        var referido = await _contexto.Referidos
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null)
            return;

        if (referido.Estado == EstadoUsuario.Activo)
            return;

        // 🟢 activar referido
        referido.Estado = EstadoUsuario.Activo;
        referido.FechaActivacion = DateTime.UtcNow;

        // 🎯 sumar puntos
        referido.Usuario!.PuntosAcumulados += 100;

        // 💰 generar comisiones
        await GenerarComisiones(referido.UsuarioId, 100);

        await _contexto.SaveChangesAsync();
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
                success = "https://localhost:5185/Pagos/Exito",
                failure = "https://localhost:5185/Pagos/Error",
                pending = "https://localhost:5185/Pagos/Pendiente"
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

        var result = JsonDocument.Parse(content);

        return result.RootElement.GetProperty("init_point").GetString()!;
    }
}