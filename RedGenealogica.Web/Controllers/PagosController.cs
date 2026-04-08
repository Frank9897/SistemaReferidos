using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Services;
using System.Text.Json;
using System.IO;

namespace RedGenealogica.Web.Controllers;

public class PagosController : Controller
{
    private readonly ServicioPagos _servicioPagos;
    private readonly IConfiguration _configuration;

    public PagosController(ServicioPagos servicioPagos, IConfiguration configuration)
    {
        _servicioPagos = servicioPagos;
        _configuration = configuration;
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

    
}