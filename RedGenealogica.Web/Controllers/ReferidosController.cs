// ============================================================
// ReferidosController.cs
// Ubicación: Controllers/ReferidosController.cs
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class ReferidosController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioReferidos _servicioReferidos;
    private readonly ContextoAplicacion _contexto;

    public ReferidosController(
        UserManager<Usuario> userManager,
        ServicioReferidos servicioReferidos,
        ContextoAplicacion contexto)
    {
        _userManager = userManager;
        _servicioReferidos = servicioReferidos;
        _contexto = contexto;
    }

    // ----------------------------------------------------------------
    // GET /Referidos/Crear
    // Muestra el formulario para registrar un nuevo referido.
    // Solo llega aquí si el usuario está autenticado (guard [Authorize]).
    // La vista debe mostrar un error si el usuario no está activo aún.
    // ----------------------------------------------------------------
    [HttpGet]
    public IActionResult Crear()
    {
        return View();
    }

    // ----------------------------------------------------------------
    // POST /Referidos/Crear
    // Registra el referido en estado Pendiente.
    // El flujo de activación es exclusivamente:
    //   Usuario referido paga → Webhook MP → ConfirmarPago → activo
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear(RegistrarReferidoViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _servicioReferidos.RegistrarReferidoAsync(usuario.Id, modelo);

        if (referido == null)
        {
            // El usuario no está activo aún (no pagó su activación)
            ModelState.AddModelError("", "Debés estar activo para referir. Completá tu pago de activación primero.");
            return View(modelo);
        }

        TempData["Exito"] = "Referido registrado. Se le enviará el enlace de pago.";
        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Referidos/MisReferidos
    // Lista todos los referidos del usuario con su estado actual.
    // Permite al usuario ver quiénes pagaron y quiénes están pendientes.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> MisReferidos()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuario.Id)
            .Include(r => r.Producto)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();

        return View(referidos);
    }

    // ----------------------------------------------------------------
    // NOTA: El endpoint POST /Referidos/Activar fue eliminado intencionalmente.
    //
    // Activar un referido manualmente (sin pago real) causaba:
    //   1. Activación sin cobro → fraude
    //   2. Doble suma de puntos cuando después llegaba el webhook de MP
    //   3. Comisiones generadas sin transacción monetaria real
    //
    // Si necesitás activar manualmente para testing, usá el panel de
    // administrador (cuando esté implementado) con auditoría completa.
    // ----------------------------------------------------------------
}
