using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Services;
using System.Text.Json;

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
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(body))
            return Ok();

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
        catch
        {
            // puedes loggear aquí si quieres
        }

        return Ok();
    }

    
}