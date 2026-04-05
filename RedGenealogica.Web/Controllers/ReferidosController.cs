// ============================================================
// ReferidosController.cs
// Ubicación: Controllers/ReferidosController.cs
//
// RESPONSABILIDAD:
// - Registrar referidos.
// - Mostrar link de pago.
// - Listar referidos del usuario.
//
// NOTA:
// Se conserva la posibilidad de registrar referidos en estado
// pendiente o activo. El mensaje de error se adapta al nuevo flujo.
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
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
    // Registra el referido. Un usuario pendiente o activo puede
    // registrar referidos. La activación real ocurre cuando paga.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear(RegistrarReferidoViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Productos = await _contexto.Productos
                .Where(p => p.Activo)
                .ToListAsync();

            return View(modelo);
        }

        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _servicioReferidos.RegistrarReferidoAsync(usuario.Id, modelo);

        if (referido == null)
        {
            ModelState.AddModelError("", "No podés registrar referidos. Tu cuenta puede estar suspendida o sin permisos.");
            ViewBag.Productos = await _contexto.Productos
                .Where(p => p.Activo)
                .ToListAsync();

            return View(modelo);
        }

        TempData["Exito"] = "Referido registrado. Compartile el link de pago para que complete su activación.";
        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Referidos/LinkPago/{id}
    // Devuelve la URL de MercadoPago para compartir con el referido.
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

        if (referido.Estado != EstadoReferido.Pendiente)
        {
            TempData["Error"] = "Este referido ya completó el pago.";
            return RedirectToAction("Panel", "Usuario");
        }

        ViewBag.Referido = referido;
        ViewBag.UrlPago = Url.Action("Pagar", "Pagos", new { referidoId = id }, Request.Scheme);
        ViewBag.CuentaActiva = usuario.EstadoUsuario == EstadoUsuario.Activo;

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