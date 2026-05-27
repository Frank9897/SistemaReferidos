// ============================================================
// ServicioPagos.cs
// Ubicación: Services/ServicioPagos.cs
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
    private readonly ServicioNotificaciones _servicioNotificaciones;
    private readonly ServicioPremios _servicioPremios;
    private readonly ServicioCorreos _servicioCorreos;
    private readonly Microsoft.AspNetCore.Identity.UserManager<Models.Usuario> _userManager;

    public ServicioPagos(
        ContextoAplicacion contexto,
        IConfiguration configuration,
        ServicioNotificaciones servicioNotificaciones,
        ServicioPremios servicioPremios,
        ServicioCorreos servicioCorreos,
        Microsoft.AspNetCore.Identity.UserManager<Models.Usuario> userManager)
    {
        _contexto = contexto;
        _configuration = configuration;
        _servicioNotificaciones = servicioNotificaciones;
        _servicioPremios = servicioPremios;
        _servicioCorreos = servicioCorreos;
        _userManager = userManager;
    }

    public async Task ConfirmarPago(int referidoId)
    {
        using var transaccion = await _contexto.Database.BeginTransactionAsync();
        int referidoIdConfirmado = 0;

        try
        {
            var referido = await _contexto.Referidos
                .Include(r => r.Usuario)
                .Include(r => r.Producto)
                .FirstOrDefaultAsync(r => r.Id == referidoId);

            if (referido == null) { await transaccion.RollbackAsync(); return; }
            if (referido.PagoConfirmado) { await transaccion.RollbackAsync(); return; }

            referido.PagoConfirmado = true;
            referido.Estado = EstadoReferido.Pagado;
            referido.FechaActivacion = DateTime.UtcNow;

            // ── AUTOPAGO ────────────────────────────────────────────
            if (referido.EsAutoPago)
            {
                var usuarioAPagar = await _contexto.Users.FindAsync(referido.UsuarioId);
                if (usuarioAPagar != null)
                {
                    // Activar cuenta
                    if (usuarioAPagar.EstadoUsuario != EstadoUsuario.Activo)
                    {
                        usuarioAPagar.EstadoUsuario = EstadoUsuario.Activo;
                        usuarioAPagar.FechaActivacion = DateTime.UtcNow;
                    }

                    // Desbloquear contenido
                    bool yaTienePago = await _contexto.Pagos.AnyAsync(p =>
                        p.UsuarioId == usuarioAPagar.Id &&
                        p.ProductoId == referido.ProductoId &&
                        p.Confirmado);

                    if (!yaTienePago)
                    {
                        _contexto.Pagos.Add(new Pago
                        {
                            UsuarioId         = usuarioAPagar.Id,
                            ProductoId        = referido.ProductoId,
                            Monto             = referido.Producto!.Precio,
                            EstadoPago        = EstadoPago.Aprobado,
                            PlataformaPago    = "MercadoPago",
                            Confirmado        = true,
                            EsSimulado        = false,
                            FechaSolicitud    = DateTime.UtcNow,
                            FechaConfirmacion = DateTime.UtcNow
                        });
                    }

                    referido.Estado = EstadoReferido.Convertido;

                    // Confirmar referido del sponsor si existe
                    var referidoDelSponsor = await _contexto.Referidos
                        .FirstOrDefaultAsync(r =>
                            r.UsuarioConvertidoId == usuarioAPagar.Id &&
                            r.UsuarioId != usuarioAPagar.Id &&   // ← evita que se confirme a sí mismo
                            !r.EsAutoPago &&
                            !r.PagoConfirmado);

                    if (referidoDelSponsor != null)
                    {
                        referidoDelSponsor.PagoConfirmado = true;
                        referidoDelSponsor.Estado = EstadoReferido.Convertido;
                        referidoDelSponsor.FechaActivacion = DateTime.UtcNow;

                        var sponsor = await _contexto.Users.FindAsync(referidoDelSponsor.UsuarioId);
                        if (sponsor != null)
                        {
                            sponsor.PuntosAcumulados += 100;

                            if (sponsor.EstadoUsuario != EstadoUsuario.Activo)
                            {
                                sponsor.EstadoUsuario = EstadoUsuario.Activo;
                                sponsor.FechaActivacion = DateTime.UtcNow;
                            }

                            bool sponsorYaTienePago = await _contexto.Pagos.AnyAsync(p =>
                                p.UsuarioId == sponsor.Id &&
                                p.ProductoId == referido.ProductoId &&
                                p.Confirmado);

                            if (!sponsorYaTienePago)
                            {
                                _contexto.Pagos.Add(new Pago
                                {
                                    UsuarioId         = sponsor.Id,
                                    ProductoId        = referido.ProductoId,
                                    Monto             = referido.Producto!.Precio,
                                    EstadoPago        = EstadoPago.Aprobado,
                                    PlataformaPago    = "MercadoPago",
                                    Confirmado        = true,
                                    EsSimulado        = false,
                                    FechaSolicitud    = DateTime.UtcNow,
                                    FechaConfirmacion = DateTime.UtcNow
                                });
                            }

                            var totalRefs = await _contexto.Referidos
                                .CountAsync(r => r.UsuarioId == sponsor.Id && r.PagoConfirmado && !r.EsAutoPago);
                            sponsor.TipoRangoActual = await ObtenerRangoActualAsync(totalRefs);

                            await _servicioNotificaciones.CrearAsync(
                                sponsor.Id,
                                TipoNotificacion.ReferidoPago,
                                "✅ Tu referido pagó",
                                $"{usuarioAPagar.Nombres} {usuarioAPagar.Apellidos} completó el pago.",
                                "/Referidos/MisReferidos");

                            if (!string.IsNullOrEmpty(sponsor.Email))
                                await _servicioCorreos.EnviarReferidoPagoAsync(
                                    sponsor.Email,
                                    $"{sponsor.Nombres} {sponsor.Apellidos}",
                                    $"{usuarioAPagar.Nombres} {usuarioAPagar.Apellidos}");
                        }
                    }
                }

                await _contexto.SaveChangesAsync();
                await transaccion.CommitAsync();

                // Premios del sponsor
                var refSponsor = await _contexto.Referidos
                    .FirstOrDefaultAsync(r =>
                        r.UsuarioConvertidoId == referido.UsuarioId &&
                        !r.EsAutoPago);
                if (refSponsor != null)
                    await _servicioPremios.ProcesarPagoReferidoAsync(refSponsor.Id);

                return;
            }

            // ── PAGO NORMAL (referido creado por sponsor) ────────────
            var referidor = referido.Usuario!;
            var rangoAnterior = referidor.TipoRangoActual;
            var eraActivo = referidor.EstadoUsuario == EstadoUsuario.Activo;

            if (!eraActivo)
            {
                referidor.EstadoUsuario = EstadoUsuario.Activo;
                referidor.FechaActivacion = DateTime.UtcNow;
            }

            // Desbloquear contenido para el sponsor
            bool sponsorYaPago = await _contexto.Pagos.AnyAsync(p =>
                p.UsuarioId == referidor.Id &&
                p.ProductoId == referido.ProductoId &&
                p.Confirmado);

            if (!sponsorYaPago)
            {
                _contexto.Pagos.Add(new Pago
                {
                    UsuarioId         = referidor.Id,
                    ProductoId        = referido.ProductoId,
                    Monto             = referido.Producto!.Precio,
                    EstadoPago        = EstadoPago.Aprobado,
                    PlataformaPago    = "MercadoPago",
                    Confirmado        = true,
                    EsSimulado        = false,
                    FechaSolicitud    = DateTime.UtcNow,
                    FechaConfirmacion = DateTime.UtcNow
                });
            }

            // Crear cuenta automática o desbloquear contenido para el referido
            if (!referido.UsuarioConvertidoId.HasValue && !string.IsNullOrEmpty(referido.CorreoElectronico))
            {
                var usuarioExistente = await _userManager.FindByEmailAsync(referido.CorreoElectronico);
                if (usuarioExistente == null)
                {
                    var passwordTemporal = $"Rg{Guid.NewGuid().ToString("N")[..6]}!";
                    var nuevoUsuario = new Models.Usuario
                    {
                        UserName           = referido.CorreoElectronico,
                        Email              = referido.CorreoElectronico,
                        Nombres            = referido.NombreCompleto.Split(' ')[0],
                        Apellidos          = referido.NombreCompleto.Contains(' ')
                                                ? referido.NombreCompleto[(referido.NombreCompleto.IndexOf(' ') + 1)..]
                                                : "",
                        CodigoReferido     = Guid.NewGuid().ToString("N")[..8],
                        EstadoUsuario      = EstadoUsuario.Activo,
                        FechaRegistro      = DateTime.UtcNow,
                        FechaActivacion    = DateTime.UtcNow,
                        IdUsuarioPadre     = referidor.Id,
                        DebeambiarPassword = true,
                        EmailConfirmed     = false
                    };

                    var resultado = await _userManager.CreateAsync(nuevoUsuario, passwordTemporal);
                    if (resultado.Succeeded)
                    {
                        referido.UsuarioConvertidoId = nuevoUsuario.Id;
                        referido.Estado = EstadoReferido.Convertido;

                        _contexto.Pagos.Add(new Pago
                        {
                            UsuarioId         = nuevoUsuario.Id,
                            ProductoId        = referido.ProductoId,
                            Monto             = referido.Producto!.Precio,
                            EstadoPago        = EstadoPago.Aprobado,
                            PlataformaPago    = "MercadoPago",
                            Confirmado        = true,
                            EsSimulado        = false,
                            FechaSolicitud    = DateTime.UtcNow,
                            FechaConfirmacion = DateTime.UtcNow
                        });

                        await _servicioCorreos.EnviarCredencialesAsync(
                            referido.CorreoElectronico,
                            referido.NombreCompleto,
                            passwordTemporal);
                    }
                }
                else
                {
                    referido.UsuarioConvertidoId = usuarioExistente.Id;
                    referido.Estado = EstadoReferido.Convertido;

                    bool yaTimePago = await _contexto.Pagos.AnyAsync(p =>
                        p.UsuarioId == usuarioExistente.Id &&
                        p.ProductoId == referido.ProductoId && p.Confirmado);

                    if (!yaTimePago)
                    {
                        _contexto.Pagos.Add(new Pago
                        {
                            UsuarioId         = usuarioExistente.Id,
                            ProductoId        = referido.ProductoId,
                            Monto             = referido.Producto!.Precio,
                            EstadoPago        = EstadoPago.Aprobado,
                            PlataformaPago    = "MercadoPago",
                            Confirmado        = true,
                            EsSimulado        = false,
                            FechaSolicitud    = DateTime.UtcNow,
                            FechaConfirmacion = DateTime.UtcNow
                        });
                    }
                }
            }
            else if (referido.UsuarioConvertidoId.HasValue)
            {
                referido.Estado = EstadoReferido.Convertido;

                bool yaTimePago = await _contexto.Pagos.AnyAsync(p =>
                    p.UsuarioId == referido.UsuarioConvertidoId.Value &&
                    p.ProductoId == referido.ProductoId && p.Confirmado);

                if (!yaTimePago)
                {
                    _contexto.Pagos.Add(new Pago
                    {
                        UsuarioId         = referido.UsuarioConvertidoId.Value,
                        ProductoId        = referido.ProductoId,
                        Monto             = referido.Producto!.Precio,
                        EstadoPago        = EstadoPago.Aprobado,
                        PlataformaPago    = "MercadoPago",
                        Confirmado        = true,
                        EsSimulado        = false,
                        FechaSolicitud    = DateTime.UtcNow,
                        FechaConfirmacion = DateTime.UtcNow
                    });
                }
            }

            referidor.PuntosAcumulados += 100;
            var totalReferidosPagados = await _contexto.Referidos
                .CountAsync(r => r.UsuarioId == referidor.Id && r.PagoConfirmado && !r.EsAutoPago);
            referidor.TipoRangoActual = await ObtenerRangoActualAsync(totalReferidosPagados);

            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId       = referidor.Id,
                CantidadPuntos  = 100,
                Monto           = 0m,
                Motivo          = $"Referido activado — {referido.NombreCompleto}",
                ReferidoId      = referido.Id,
                Nivel           = 0,
                FechaMovimiento = DateTime.UtcNow
            });

            await _contexto.SaveChangesAsync();

            await _servicioNotificaciones.CrearAsync(
                referidor.Id, TipoNotificacion.ReferidoPago,
                "✅ Tu referido pagó",
                $"{referido.NombreCompleto} completó el pago de {referido.Producto?.Nombre}. Ganaste 100 puntos.",
                "/Referidos/MisReferidos");

            if (!eraActivo)
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id, TipoNotificacion.Sistema,
                    "🎉 ¡Tu cuenta está activa!",
                    "Tu cuenta fue activada. Ya podés registrar más referidos y ganar premios.",
                    "/Usuario/Panel");

            if (referidor.TipoRangoActual != rangoAnterior)
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id, TipoNotificacion.SubidaDeRango,
                    "🏆 ¡Subiste de rango!",
                    $"Felicitaciones, ahora sos {referidor.TipoRangoActual}.",
                    "/Usuario/Panel");

            if (!string.IsNullOrEmpty(referidor.Email))
                await _servicioCorreos.EnviarReferidoPagoAsync(
                    referidor.Email,
                    $"{referidor.Nombres} {referidor.Apellidos}",
                    referido.NombreCompleto);

            referidoIdConfirmado = referido.Id;
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        if (referidoIdConfirmado > 0)
            await _servicioPremios.ProcesarPagoReferidoAsync(referidoIdConfirmado);
    }

    public async Task<string> CrearPreferencia(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.Id == referidoId)
            ?? throw new Exception("Referido no encontrado");

        var accessToken = _configuration["MercadoPago:AccessToken"]
            ?? throw new Exception("Token de MercadoPago no configurado");

        var baseUrl = (_configuration["App:BaseUrl"]
            ?? throw new Exception("BaseUrl no configurado en appsettings"))
            .TrimEnd('/');

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            items = new[]
            {
                new
                {
                    title       = referido.Producto!.Nombre,
                    quantity    = 1,
                    unit_price  = referido.Producto.Precio
                }
            },
            payer = new
            {
                email = referido.CorreoElectronico ?? "referidossistema00@gmail.com"
            },
            back_urls = new
            {
                success = $"{baseUrl}/Pagos/Exito?ok=1",
                failure = $"{baseUrl}/Pagos/Error",
                pending = $"{baseUrl}/Pagos/Pendiente"
            },
            auto_return         = "approved",
            notification_url    = $"{baseUrl}/Pagos/Webhook",
            external_reference  = referidoId.ToString(),
            metadata            = new { referido_id = referidoId },
            statement_descriptor = "RedGenealogica"
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
            throw new Exception("Respuesta inválida de MercadoPago: " + content);

        return initPoint.GetString()!;
    }

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

        var status = paymentJson.RootElement.GetProperty("status").GetString();
        if (status != "approved") return false;

        var externalReference = paymentJson.RootElement
            .GetProperty("external_reference").GetString();
        if (string.IsNullOrEmpty(externalReference)) return false;

        int referidoId = int.Parse(externalReference);
        var referido   = await _contexto.Referidos.FindAsync(referidoId);
        if (referido == null) return false;

        _contexto.RegistrosWebhook.Add(new RegistroWebhook
        {
            IdPago        = idPago,
            Estado        = status,
            FechaRegistro = DateTime.UtcNow
        });
        await _contexto.SaveChangesAsync();

        await ConfirmarPago(referidoId);
        return true;
    }

    private async Task<TipoRango> ObtenerRangoActualAsync(int referidosPagados)
    {
        var rangos = await _contexto.RangosUsuario
            .Where(r => r.Activo)
            .OrderByDescending(r => r.Orden)
            .ToListAsync();

        if (!rangos.Any()) return TipoRango.Cobre;

        var rango = rangos.FirstOrDefault(r => referidosPagados >= r.PuntosMinimos);
        return rango?.TipoRango ?? rangos.Last().TipoRango;
    }
}
