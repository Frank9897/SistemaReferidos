// ============================================================
// NotificacionesController.cs
// Ubicación: Controllers/NotificacionesController.cs
//
// Expone endpoints para:
//   - Polling de no leídas (JSON) → usado por el JS del navbar
//   - Marcar una como leída
//   - Marcar todas como leídas
//   - Vista historial completo
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using System.Security.Claims;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ServicioNotificaciones _servicioNotificaciones;
    private readonly UserManager<Usuario> _userManager;

    public NotificacionesController(
        ServicioNotificaciones servicioNotificaciones,
        UserManager<Usuario> userManager)
    {
        _servicioNotificaciones = servicioNotificaciones;
        _userManager = userManager;
    }

    // ----------------------------------------------------------------
    // GET /Notificaciones/NoLeidas
    // Endpoint de polling — devuelve JSON con las no leídas.
    // El JS del layout consulta este endpoint cada 30 segundos.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> NoLeidas()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var notifs = await _servicioNotificaciones.ObtenerNoLeidasAsync(usuarioId, max: 10);
        var count  = await _servicioNotificaciones.ContarNoLeidasAsync(usuarioId);

        return Json(new
        {
            count       = count,
            items       = notifs.Select(n => new
            {
                id           = n.Id,
                tipo         = n.Tipo.ToString(),
                titulo       = n.Titulo,
                mensaje      = n.Mensaje,
                urlAccion    = n.UrlAccion,
                fecha        = n.FechaCreacion.ToString("dd/MM HH:mm"),
                fechaIso     = n.FechaCreacion.ToString("o")
            })
        });
    }

    // ----------------------------------------------------------------
    // POST /Notificaciones/MarcarLeida/{id}
    // Marca una notificación como leída y redirige a su urlAccion.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> MarcarLeida(int id, string? urlRetorno = null)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _servicioNotificaciones.MarcarLeidaAsync(id, usuarioId);

        if (!string.IsNullOrEmpty(urlRetorno))
            return Redirect(urlRetorno);

        return RedirectToAction("Index");
    }

    // ----------------------------------------------------------------
    // POST /Notificaciones/MarcarTodasLeidas
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _servicioNotificaciones.MarcarTodasLeidasAsync(usuarioId);
        return Ok();
    }

    // ----------------------------------------------------------------
    // GET /Notificaciones
    // Vista con el historial completo de notificaciones.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index(int pagina = 1)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var notifs = await _servicioNotificaciones.ObtenerHistorialAsync(usuarioId, pagina);
        var total  = await _servicioNotificaciones.ContarNoLeidasAsync(usuarioId);

        // Marcar todas como leídas al entrar a la vista
        await _servicioNotificaciones.MarcarTodasLeidasAsync(usuarioId);

        ViewBag.Pagina       = pagina;
        ViewBag.NoLeidas     = total;
        return View(notifs);
    }
}
