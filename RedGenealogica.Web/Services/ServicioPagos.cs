// ============================================================
// ServicioPagos.cs
// Ubicación: Services/ServicioPagos.cs
//
// RESPONSABILIDAD:
// - Crear preferencia de pago en MercadoPago.
// - Procesar webhook de pagos aprobados.
// - Confirmar el pago del referido.
// - Disparar la lógica de premios por ciclos.
// - Registrar webhook para evitar duplicados.
//
// NOTA:
// Se eliminó por completo la lógica de comisiones multinivel.
// Ahora el sistema funciona con:
//   - premio fijo por cada 3 referidos pagados
//   - bono fijo al padre directo
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
    // ----------------------------------------------------------------
    // Confirma el pago de un referido y dispara la lógica de premios.
    // ----------------------------------------------------------------
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

            if (referido == null)
            {
                await transaccion.RollbackAsync();
                return;
            }

            if (referido.PagoConfirmado)
            {
                await transaccion.RollbackAsync();
                return;
            }

            referido.PagoConfirmado = true;
            referido.Estado = EstadoReferido.Pagado;
            referido.FechaActivacion = DateTime.UtcNow;

            // Autopago: el usuario se activa a sí mismo.
            // Retornamos temprano para no correr la lógica de referidor/premios.
            if (referido.EsAutoPago)
            {
                var usuarioAPagar = await _contexto.Users.FindAsync(referido.UsuarioId);
                if (usuarioAPagar != null && usuarioAPagar.EstadoUsuario != EstadoUsuario.Activo)
                {
                    usuarioAPagar.EstadoUsuario = EstadoUsuario.Activo;
                    usuarioAPagar.FechaActivacion = DateTime.UtcNow;
                }
                await _contexto.SaveChangesAsync();
                // Commiteamos ANTES de procesar premios para que el webhook
                // quede guardado aunque los premios fallen.
                await transaccion.CommitAsync();
                return;
            }

            // Pago de referido normal: activar al referidor si estaba inactivo.
            var referidor = referido.Usuario!;
            var rangoAnterior = referidor.TipoRangoActual;
            var eraActivo = referidor.EstadoUsuario == EstadoUsuario.Activo;

            if (!eraActivo)
            {
                referidor.EstadoUsuario = EstadoUsuario.Activo;
                referidor.FechaActivacion = DateTime.UtcNow;
            }

            // ── Crear registro de Pago confirmado ────────────────────
            // Este registro es lo que desbloquea el acceso al contenido
            // digital del producto para el referidor (sponsor).
            // Verificar que el sponsor no tenga ya un pago para este producto.
            bool sponsorYaTienePago = await _contexto.Pagos.AnyAsync(p =>
                p.UsuarioId == referidor.Id &&
                p.ProductoId == referido.ProductoId &&
                p.Confirmado);

            if (!sponsorYaTienePago)
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

            // Pago para el usuario convertido — si el referido ya es usuario,
            // también desbloquea el contenido para él.
            // ── Crear cuenta automática si el referido no es usuario aún ──
            if (!referido.UsuarioConvertidoId.HasValue
                && !string.IsNullOrEmpty(referido.CorreoElectronico))
            {
                // Verificar que no exista ya una cuenta con ese email
                var usuarioExistente = await _userManager.FindByEmailAsync(referido.CorreoElectronico);
                if (usuarioExistente == null)
                {
                    // Generar contraseña temporal segura
                    var passwordTemporal = $"Rg{Guid.NewGuid().ToString("N")[..6]}!";

                    var nuevoUsuario = new Models.Usuario
                    {
                        UserName       = referido.CorreoElectronico,
                        Email          = referido.CorreoElectronico,
                        Nombres        = referido.NombreCompleto.Split(' ')[0],
                        Apellidos      = referido.NombreCompleto.Contains(' ')
                                            ? referido.NombreCompleto[(referido.NombreCompleto.IndexOf(' ') + 1)..]
                                            : "",
                        CodigoReferido = Guid.NewGuid().ToString("N")[..8],
                        EstadoUsuario  = Enumeraciones.EstadoUsuario.Activo,
                        FechaRegistro  = DateTime.UtcNow,
                        FechaActivacion = DateTime.UtcNow,
                        IdUsuarioPadre = referidor.Id,   // el sponsor es el padre
                        DebeambiarPassword = true,   // ← nuevo
                        EmailConfirmed  = false      // ← forzar verificación
                    };

                    var resultado = await _userManager.CreateAsync(nuevoUsuario, passwordTemporal);

                    if (resultado.Succeeded)
                    {
                        referido.UsuarioConvertidoId = nuevoUsuario.Id;

                        // Desbloquear contenido para el nuevo usuario
                        _contexto.Pagos.Add(new Pago
                        {
                            UsuarioId         = nuevoUsuario.Id,
                            ProductoId        = referido.ProductoId,
                            Monto             = referido.Producto!.Precio,
                            EstadoPago        = Enumeraciones.EstadoPago.Aprobado,
                            PlataformaPago    = "MercadoPago",
                            Confirmado        = true,
                            EsSimulado        = false,
                            FechaSolicitud    = DateTime.UtcNow,
                            FechaConfirmacion = DateTime.UtcNow
                        });

                        // Email con credenciales
                        await _servicioCorreos.EnviarCredencialesAsync(
                            referido.CorreoElectronico,
                            referido.NombreCompleto,
                            passwordTemporal);
                    }
                }
                else
                {
                    // Ya tiene cuenta — solo vinculamos y desbloqueamos contenido
                    referido.UsuarioConvertidoId = usuarioExistente.Id;

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
                            EstadoPago        = Enumeraciones.EstadoPago.Aprobado,
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
                // Ya estaba vinculado antes — solo asegurar que tenga el pago
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
                        EstadoPago        = Enumeraciones.EstadoPago.Aprobado,
                        PlataformaPago    = "MercadoPago",
                        Confirmado        = true,
                        EsSimulado        = false,
                        FechaSolicitud    = DateTime.UtcNow,
                        FechaConfirmacion = DateTime.UtcNow
                    });
                }
            }

            // Mantener la lógica de puntos del referidor.
            referidor.PuntosAcumulados += 100;
            var totalReferidosPagados = await _contexto.Referidos
                .CountAsync(r => r.UsuarioId == referidor.Id && r.PagoConfirmado);
            referidor.TipoRangoActual = await ObtenerRangoActualAsync(totalReferidosPagados);

            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = referidor.Id,
                CantidadPuntos = 100,
                Monto = 0m,
                Motivo = $"Referido activado — {referido.NombreCompleto}",
                ReferidoId = referido.Id,
                Nivel = 0,
                FechaMovimiento = DateTime.UtcNow
            });

            await _contexto.SaveChangesAsync();

            // Notificación: referido pagó.
            await _servicioNotificaciones.CrearAsync(
                referidor.Id,
                TipoNotificacion.ReferidoPago,
                "✅ Tu referido pagó",
                $"{referido.NombreCompleto} completó el pago de {referido.Producto?.Nombre}. Ganaste 100 puntos.",
                "/Referidos/MisReferidos"
            );

            // Notificación: cuenta activada.
            if (!eraActivo)
            {
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id,
                    TipoNotificacion.Sistema,
                    "🎉 ¡Tu cuenta está activa!",
                    "Tu cuenta fue activada. Ya podés registrar más referidos y ganar premios.",
                    "/Usuario/Panel"
                );
            }

            // Notificación: subida de rango.
            if (referidor.TipoRangoActual != rangoAnterior)
            {
                await _servicioNotificaciones.CrearAsync(
                    referidor.Id,
                    TipoNotificacion.SubidaDeRango,
                    "🏆 ¡Subiste de rango!",
                    $"Felicitaciones, ahora sos {referidor.TipoRangoActual}.",
                    "/Usuario/Panel"
                );
            }
            
            // Email al sponsor
            if (!string.IsNullOrEmpty(referidor.Email))
                await _servicioCorreos.EnviarReferidoPagoAsync(
                    referidor.Email,
                    $"{referidor.Nombres} {referidor.Apellidos}",
                    referido.NombreCompleto);

            referidoIdConfirmado = referido.Id; // ← guardar antes del commit
            // Commiteamos ANTES de procesar premios para que el webhook
            // quede guardado aunque los premios fallen.
            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        // Premios fuera de la transacción principal — tienen su propio SaveChanges.
        // Si fallan, el pago ya está confirmado y el webhook registrado.
        if (referidoIdConfirmado > 0)
            await _servicioPremios.ProcesarPagoReferidoAsync(referidoIdConfirmado);
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
                success = $"{baseUrl}/Pagos/Exito?ok=1",
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
            throw new Exception("Respuesta inválida de MercadoPago: " + content);

        return initPoint.GetString()!;
    }

    // ----------------------------------------------------------------
    // Procesa el webhook de MercadoPago.
    // Evita duplicados con la tabla RegistrosWebhook.
    // ----------------------------------------------------------------
    public async Task<bool> ProcesarWebhookPagoAsync(string idPago)
    {
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

        var status = paymentJson.RootElement.GetProperty("status").GetString();
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

        // Registrar el webhook ANTES de procesar para evitar duplicados
        // incluso si ConfirmarPago hace return anticipado (caso autopago).
        _contexto.RegistrosWebhook.Add(new RegistroWebhook
        {
            IdPago = idPago,
            Estado = status,
            FechaRegistro = DateTime.UtcNow
        });
        await _contexto.SaveChangesAsync();

        await ConfirmarPago(referidoId);
        return true;
    }

    // ----------------------------------------------------------------
    // Obtiene el rango actual del usuario según sus puntos.
    // Se conserva esta lógica porque sigue siendo útil para el panel.
    // ----------------------------------------------------------------
    private async Task<TipoRango> ObtenerRangoActualAsync(int referidosPagados)
    {
        var rangos = await _contexto.RangosUsuario
            .Where(r => r.Activo)
            .OrderByDescending(r => r.Orden)
            .ToListAsync();

        // Consistente con el default del modelo Usuario (TipoRango.Cobre)
        if (!rangos.Any())
            return TipoRango.Cobre;

        var rango = rangos.FirstOrDefault(r => referidosPagados >= r.PuntosMinimos);

        return rango?.TipoRango ?? rangos.Last().TipoRango;
    }
}