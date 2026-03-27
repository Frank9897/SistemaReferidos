using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Services;

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

        var json = System.Text.Json.JsonDocument.Parse(body);

        if (!json.RootElement.TryGetProperty("data", out var data))
            return Ok();

        if (!data.TryGetProperty("id", out var idProperty))
            return Ok();

        var paymentId = idProperty.GetString();

        var accessToken = _configuration["MercadoPago:AccessToken"];

        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}");

        var content = await response.Content.ReadAsStringAsync();
        var paymentJson = System.Text.Json.JsonDocument.Parse(content);

        var status = paymentJson.RootElement.GetProperty("status").GetString();

        if (status == "approved")
        {
            var externalReference = paymentJson.RootElement
                .GetProperty("external_reference")
                .GetString();

            int referidoId = int.Parse(externalReference!);

            await _servicioPagos.ConfirmarPago(referidoId);
        }

        return Ok();
    }
}