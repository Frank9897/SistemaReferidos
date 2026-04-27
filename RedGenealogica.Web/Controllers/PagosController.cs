using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.Enumeraciones;
using System.Text.Json;
using System.IO;

namespace RedGenealogica.Web.Controllers;

public class PagosController : Controller
{
    private readonly ServicioPagos _servicioPagos;
    private readonly IConfiguration _configuration;
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public PagosController(
        ServicioPagos servicioPagos,
        IConfiguration configuration,
        ContextoAplicacion contexto,
        UserManager<Usuario> userManager)
    {
        _servicioPagos   = servicioPagos;
        _configuration   = configuration;
        _contexto        = contexto;
        _userManager     = userManager;
    }

    public async Task<IActionResult> Pagar(int referidoId)
    {
        var url = await _servicioPagos.CrearPreferencia(referidoId);
        return Redirect(url);
    }

    public IActionResult Exito() => View();
    public IActionResult Error() => View();
    public IActionResult Pendiente() => View();

    [HttpPost]
    public async Task<IActionResult> Webhook()
    {
        // Leer headers de firma que manda MercadoPago
        Request.Headers.TryGetValue("x-signature", out var xSignature);
        Request.Headers.TryGetValue("x-request-id", out var xRequestId);

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(body))
            return Ok();

        // Verificar firma antes de procesar nada
        var secreto = _configuration["MercadoPago:WebhookSecret"];
        if (!string.IsNullOrEmpty(secreto))
        {
            if (!VerificarFirmaMP(xSignature!, xRequestId!, body, secreto))
                return Unauthorized();
        }

        var json = JsonDocument.Parse(body);

        if (!json.RootElement.TryGetProperty("data", out var data))
            return Ok();

        if (!data.TryGetProperty("id", out var idProperty))
            return Ok();

        var paymentId = idProperty.GetString();

        if (string.IsNullOrEmpty(paymentId))
            return Ok();

        try
        {
            await _servicioPagos.ProcesarWebhookPagoAsync(paymentId);
        }
        catch { }

        return Ok();
    }

    private bool VerificarFirmaMP(string xSignature, string xRequestId, string body, string secreto)
    {
        try
        {
            // Extraer ts y v1 del header x-signature
            // Formato: "ts=1234567890,v1=abcdef..."
            var partes = xSignature.Split(',');
            string? ts = null, v1 = null;

            foreach (var parte in partes)
            {
                if (parte.StartsWith("ts=")) ts = parte[3..];
                if (parte.StartsWith("v1=")) v1 = parte[3..];
            }

            if (ts == null || v1 == null) return false;

            // Extraer data.id del body para el manifest
            using var doc = JsonDocument.Parse(body);
            var dataId = doc.RootElement
                .GetProperty("data")
                .GetProperty("id")
                .GetString() ?? "";

            // Construir el manifest según la doc de MP
            var manifest = $"id:{dataId};request-id:{xRequestId};ts:{ts};";

            // HMAC-SHA256
            var keyBytes  = System.Text.Encoding.UTF8.GetBytes(secreto);
            var msgBytes  = System.Text.Encoding.UTF8.GetBytes(manifest);
            using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(msgBytes);
            var hashHex = Convert.ToHexString(hash).ToLower();

            return hashHex == v1;
        }
        catch
        {
            return false;
        }
    }

    // ----------------------------------------------------------------
    // GET /Pagos/PagarActivacion
    // El usuario paga el producto para activar su propia cuenta
    // ----------------------------------------------------------------
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> PagarActivacion()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null) return RedirectToAction("Login", "Autenticacion");

        if (usuario.EstadoUsuario == EstadoUsuario.Activo)
        {
            TempData["Exito"] = "Tu cuenta ya está activa.";
            return RedirectToAction("Panel", "Usuario");
        }

        // Buscar el producto activo
        var producto = await _contexto.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.FechaCreacion)
            .FirstOrDefaultAsync();

        if (producto == null)
        {
            TempData["Error"] = "No hay productos disponibles.";
            return RedirectToAction("Panel", "Usuario");
        }

        // Crear un referido propio si no existe
        var referidoPropio = await _contexto.Referidos
            .FirstOrDefaultAsync(r => r.UsuarioId == usuario.Id && r.EsAutoPago);

        if (referidoPropio == null)
        {
            referidoPropio = new Referido
            {
                UsuarioId        = usuario.Id,
                NombreCompleto   = $"{usuario.Nombres} {usuario.Apellidos}",
                CorreoElectronico = usuario.Email!,
                ProductoId       = producto.Id,
                FechaRegistro    = DateTime.UtcNow,
                Estado           = EstadoReferido.Pendiente,
                EsAutoPago       = true
            };
            _contexto.Referidos.Add(referidoPropio);
            await _contexto.SaveChangesAsync();
        }

        var urlPago = await _servicioPagos.CrearPreferencia(referidoPropio.Id);
        return Redirect(urlPago);
    }
    
}