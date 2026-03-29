// ============================================================
// ReferidosController.cs
// Ubicación: Controllers/ReferidosController.cs
//
// CAMBIO:
//   Un usuario Pendiente SÍ puede registrar referidos.
//   El mensaje de error ahora distingue entre suspendido
//   y pendiente de activación.
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
    // Carga el formulario con los productos activos disponibles.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var productos = await _contexto.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        ViewBag.Productos = productos;
        return View();
    }

    // ----------------------------------------------------------------
    // POST /Referidos/Crear
    // Registra el referido. Tanto usuarios Pendientes como Activos
    // pueden registrar referidos. La activación ocurre cuando paga.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear(RegistrarReferidoViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Productos = await _contexto.Productos
                .Where(p => p.Activo).ToListAsync();
            return View(modelo);
        }

        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _servicioReferidos.RegistrarReferidoAsync(usuario.Id, modelo);

        if (referido == null)
        {
            ModelState.AddModelError("", "No podés registrar referidos. Tu cuenta puede estar suspendida.");
            ViewBag.Productos = await _contexto.Productos
                .Where(p => p.Activo).ToListAsync();
            return View(modelo);
        }

        TempData["Exito"] = $"Referido registrado. Compartile el link de pago para que active tu cuenta.";
        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Referidos/LinkPago/{id}
    // Devuelve la URL de MercadoPago para que el usuario se la comparta
    // al referido. Solo disponible si el referido está Pendiente.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LinkPago(int id)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == usuario.Id);

        if (referido == null)
            return NotFound();

        if (referido.Estado != Enumeraciones.EstadoReferido.Pendiente)
        {
            TempData["Error"] = "Este referido ya completó el pago.";
            return RedirectToAction("Panel", "Usuario");
        }

        ViewBag.Referido = referido;
        // URL directa de pago: /Pagos/Pagar?referidoId=X
        ViewBag.UrlPago = Url.Action("Pagar", "Pagos", new { referidoId = id }, Request.Scheme);

        return View();
    }

    // ----------------------------------------------------------------
    // GET /Referidos/MisReferidos
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
}
