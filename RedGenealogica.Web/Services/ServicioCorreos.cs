// ============================================================
// ServicioCorreos.cs
// Ubicación: Services/ServicioCorreos.cs
//
// Envía correos transaccionales via SendGrid API (HTTP).
// Reemplaza MailKit/SMTP que Railway bloquea en plan Hobby.
//
// Variable de entorno requerida: Email__SendGridApiKey
// Remitente verificado: referidossistema00@gmail.com
// ============================================================

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RedGenealogica.Web.Services;

public class ServicioCorreos
{
    private readonly IConfiguration _config;
    private readonly ILogger<ServicioCorreos> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string REMITENTE_EMAIL  = "referidossistema00@gmail.com";
    private const string REMITENTE_NOMBRE = "RedGenealogica";
    private const string SENDGRID_API_URL = "https://api.sendgrid.com/v3/mail/send";

    public ServicioCorreos(
        IConfiguration config,
        ILogger<ServicioCorreos> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config            = config;
        _logger            = logger;
        _httpClientFactory = httpClientFactory;
    }

    // ----------------------------------------------------------------
    // Método base — envía cualquier email HTML via SendGrid API
    // ----------------------------------------------------------------
    private async Task EnviarAsync(string destinatario, string nombreDestinatario, string asunto, string cuerpoHtml)
    {
        try
        {
            var apiKey = _config["Email:SendGridApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Email:SendGridApiKey no configurado. Email a {Destinatario} omitido.", destinatario);
                return;
            }

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = destinatario, name = nombreDestinatario } }
                    }
                },
                from    = new { email = REMITENTE_EMAIL, name = REMITENTE_NOMBRE },
                subject = asunto,
                content = new[]
                {
                    new { type = "text/html", value = cuerpoHtml }
                }
            };

            var json    = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, SENDGRID_API_URL)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var http     = _httpClientFactory.CreateClient();
            var response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("SendGrid API error {Status} al enviar a {Destinatario}: {Body}",
                    (int)response.StatusCode, destinatario, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Destinatario}", destinatario);
            // No relanzamos — un error de email nunca debe romper el flujo de negocio
        }
    }

    // ----------------------------------------------------------------
    // Bienvenida — registro manual por el usuario
    // ----------------------------------------------------------------
    public async Task EnviarBienvenidaAsync(string email, string nombre)
    {
        var html = PlantillaBase("¡Bienvenido a RedGenealogica! 🎉", $"""
            <p>Hola <strong>{nombre}</strong>,</p>
            <p>Tu cuenta fue creada exitosamente en <strong>RedGenealogica</strong>.</p>
            <p>Para activar tu cuenta y empezar a ganar, tu primer referido debe completar el pago.</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="https://redgenealogico.up.railway.app/Usuario/Panel"
                   style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                          text-decoration:none; font-weight:600; font-size:15px;">
                    Ir a mi panel
                </a>
            </div>
            <p style="color:#94a3b8; font-size:13px;">Si no creaste esta cuenta, ignorá este mensaje.</p>
        """);

        await EnviarAsync(email, nombre, "◆ Bienvenido a RedGenealogica", html);
    }

    // ----------------------------------------------------------------
    // Credenciales — cuenta creada automáticamente post-pago
    // ----------------------------------------------------------------
    public async Task EnviarCredencialesAsync(string email, string nombre, string password)
    {
        var html = PlantillaBase("Tu cuenta fue creada ✅", $"""
            <p>Hola <strong>{nombre}</strong>,</p>
            <p>Tu pago fue confirmado y tu cuenta en <strong>RedGenealogica</strong> fue creada automáticamente.</p>
            <div style="background:#1e293b; border-radius:8px; padding:20px; margin:20px 0;">
                <p style="margin:0 0 8px; color:#94a3b8; font-size:13px;">Tus credenciales de acceso:</p>
                <p style="margin:0 0 6px;"><strong>Email:</strong> {email}</p>
                <p style="margin:0;"><strong>Contraseña temporal:</strong> <span style="color:#22c55e; font-family:monospace; font-size:15px;">{password}</span></p>
            </div>
            <p style="color:#f59e0b; font-size:13px;">⚠️ Por tu seguridad, cambiá tu contraseña después de iniciar sesión.</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="https://redgenealogico.up.railway.app/Autenticacion/Login"
                   style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                          text-decoration:none; font-weight:600; font-size:15px;">
                    Iniciar sesión
                </a>
            </div>
        """);

        await EnviarAsync(email, nombre, "◆ Tu cuenta en RedGenealogica está lista", html);
    }

    // ----------------------------------------------------------------
    // Link de pago — se manda cuando el sponsor registra el referido
    // ----------------------------------------------------------------
    public async Task EnviarLinkPagoAsync(string email, string nombreReferido, string nombreSponsor, string urlPago)
    {
        var html = PlantillaBase("Tenés una invitación 🎉", $"""
            <p>Hola <strong>{nombreReferido}</strong>,</p>
            <p><strong>{nombreSponsor}</strong> te invitó a unirte a <strong>RedGenealogica</strong>, una red de referidos con productos digitales.</p>
            <p>Para activar tu cuenta y acceder al contenido, completá tu pago haciendo clic en el botón:</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="{urlPago}"
                style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                        text-decoration:none; font-weight:600; font-size:15px;">
                    💳 Completar pago y activar cuenta
                </a>
            </div>
            <p style="color:#94a3b8; font-size:13px;">Una vez confirmado el pago, recibirás tus credenciales de acceso por este mismo correo.</p>
            <p style="color:#94a3b8; font-size:13px;">Si no esperabas este mensaje, podés ignorarlo.</p>
        """);

        await EnviarAsync(email, nombreReferido, "◆ Tu invitación a RedGenealogica", html);
    }

    // ----------------------------------------------------------------
    // Notificación al sponsor — su referido pagó
    // ----------------------------------------------------------------
    public async Task EnviarReferidoPagoAsync(string emailSponsor, string nombreSponsor, string nombreReferido)
    {
        var html = PlantillaBase("¡Tu referido pagó! 💰", $"""
            <p>Hola <strong>{nombreSponsor}</strong>,</p>
            <p><strong>{nombreReferido}</strong> completó su pago y ya está activo en tu red.</p>
            <p>Revisá tu panel para ver tu progreso de ciclo y saldo actualizado.</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="https://redgenealogico.up.railway.app/Usuario/Panel"
                   style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                          text-decoration:none; font-weight:600; font-size:15px;">
                    Ver mi panel
                </a>
            </div>
        """);

        await EnviarAsync(emailSponsor, nombreSponsor, "◆ Tu referido completó el pago", html);
    }

    // ----------------------------------------------------------------
    // Notificación — retiro aprobado
    // ----------------------------------------------------------------
    public async Task EnviarRetiroAprobadoAsync(string email, string nombre, decimal monto)
    {
        var html = PlantillaBase("Retiro aprobado ✅", $"""
            <p>Hola <strong>{nombre}</strong>,</p>
            <p>Tu solicitud de retiro de <strong style="color:#22c55e;">${monto:N0}</strong> fue aprobada.</p>
            <p>El dinero será transferido a tu cuenta en las próximas horas.</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="https://redgenealogico.up.railway.app/Usuario/Panel"
                   style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                          text-decoration:none; font-weight:600; font-size:15px;">
                    Ver mi saldo
                </a>
            </div>
        """);

        await EnviarAsync(email, nombre, "◆ Tu retiro fue aprobado", html);
    }

    // ----------------------------------------------------------------
    // Notificación — retiro rechazado
    // ----------------------------------------------------------------
    public async Task EnviarRetiroRechazadoAsync(string email, string nombre, decimal monto, string motivo)
    {
        var html = PlantillaBase("Retiro rechazado ❌", $"""
            <p>Hola <strong>{nombre}</strong>,</p>
            <p>Tu solicitud de retiro de <strong>${monto:N0}</strong> fue rechazada.</p>
            <div style="background:#1e293b; border-radius:8px; padding:16px; margin:16px 0;">
                <p style="margin:0; color:#94a3b8; font-size:13px;">Motivo:</p>
                <p style="margin:6px 0 0; color:#f87171;">{motivo}</p>
            </div>
            <p>Tu saldo fue reintegrado. Si tenés dudas, contactá con soporte.</p>
        """);

        await EnviarAsync(email, nombre, "◆ Tu retiro fue rechazado", html);
    }

    // ----------------------------------------------------------------
    // Verificación de email
    // ----------------------------------------------------------------
    public async Task EnviarVerificacionEmailAsync(string email, string nombre, string urlVerificacion)
    {
        var html = PlantillaBase("Verificá tu email ✉️", $"""
            <p>Hola <strong>{nombre}</strong>,</p>
            <p>Ya cambiaste tu contraseña. Solo falta verificar tu email para completar la activación de tu cuenta.</p>
            <div style="text-align:center; margin:28px 0;">
                <a href="{urlVerificacion}"
                style="background:#22c55e; color:#fff; padding:13px 32px; border-radius:8px;
                        text-decoration:none; font-weight:600; font-size:15px;">
                    ✅ Verificar mi email
                </a>
            </div>
            <p style="color:#94a3b8; font-size:13px;">Si no creaste esta cuenta, ignorá este mensaje.</p>
        """);

        await EnviarAsync(email, nombre, "◆ Verificá tu email en RedGenealogica", html);
    }

    // ----------------------------------------------------------------
    // Plantilla HTML base — dark mode consistente con el sistema
    // ----------------------------------------------------------------
    private static string PlantillaBase(string titulo, string contenido) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0; padding:0; background:#0f172a; font-family:'Segoe UI',sans-serif; color:#e2e8f0;">
            <table width="100%" cellpadding="0" cellspacing="0" style="background:#0f172a; padding:40px 0;">
                <tr><td align="center">
                    <table width="560" cellpadding="0" cellspacing="0"
                           style="background:#1e293b; border-radius:12px; overflow:hidden; max-width:90vw;">
                        <tr>
                            <td style="background:#0f172a; padding:24px 32px; border-bottom:1px solid #334155;">
                                <span style="font-size:22px; font-weight:700; color:#e2e8f0;">◆ RedGenealogica</span>
                            </td>
                        </tr>
                        <tr>
                            <td style="padding:28px 32px 0;">
                                <h2 style="margin:0; font-size:20px; color:#f1f5f9;">{titulo}</h2>
                            </td>
                        </tr>
                        <tr>
                            <td style="padding:20px 32px 32px; font-size:14px; line-height:1.7; color:#cbd5e1;">
                                {contenido}
                            </td>
                        </tr>
                        <tr>
                            <td style="background:#0f172a; padding:16px 32px; border-top:1px solid #334155;
                                       font-size:12px; color:#475569; text-align:center;">
                                © 2026 RedGenealogica · Este es un mensaje automático, no respondas este correo.
                            </td>
                        </tr>
                    </table>
                </td></tr>
            </table>
        </body>
        </html>
        """;
}