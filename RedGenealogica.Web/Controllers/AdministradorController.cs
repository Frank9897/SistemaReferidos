// ============================================================
// AdministradorController.cs
// Ubicación: Controllers/AdministradorController.cs
//
// NUEVO ARCHIVO - estaba vacío en el proyecto original.
//
// Implementa las funciones básicas de administración:
//   - Listar todos los usuarios con su estado
//   - Suspender / reactivar usuarios
//   - Ver el detalle de un usuario (referidos, pagos, comisiones)
//
// SEGURIDAD: este controller solo es accesible para usuarios con
// el rol "Admin". Ese rol debe asignarse manualmente en la BD o
// mediante una seed inicial en Program.cs.
//
// TODO: agregar seed de rol Admin en Program.cs
// TODO: agregar vistas para cada acción
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Controllers;

// Solo usuarios con rol "Admin" pueden acceder a este controller
[Authorize(Roles = "Admin")]
public class AdministradorController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public AdministradorController(
        ContextoAplicacion contexto,
        UserManager<Usuario> userManager)
    {
        _contexto = contexto;
        _userManager = userManager;
    }

    // ----------------------------------------------------------------
    // GET /Administrador/Usuarios
    // Lista todos los usuarios del sistema con su estado y estadísticas.
    // Permite buscar por nombre o email.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Usuarios(string? busqueda)
    {
        var query = _contexto.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.ToLower();
            query = query.Where(u =>
                u.Nombres.ToLower().Contains(b) ||
                u.Apellidos.ToLower().Contains(b) ||
                (u.Email != null && u.Email.ToLower().Contains(b)));
        }

        var usuarios = await query
            .OrderByDescending(u => u.FechaRegistro)
            .ToListAsync();

        ViewBag.Busqueda = busqueda;
        return View(usuarios);
    }

    // ----------------------------------------------------------------
    // GET /Administrador/DetalleUsuario/{id}
    // Muestra el detalle completo de un usuario: referidos, pagos,
    // movimientos de puntos y su posición en el árbol.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> DetalleUsuario(int id)
    {
        var usuario = await _contexto.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
            return NotFound();

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == id)
            .Include(r => r.Producto)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();

        var movimientos = await _contexto.MovimientosPuntos
            .Where(m => m.UsuarioId == id)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(20)
            .ToListAsync();

        var pagos = await _contexto.Pagos
            .Where(p => p.UsuarioId == id)
            .OrderByDescending(p => p.FechaSolicitud)
            .ToListAsync();

        ViewBag.Referidos = referidos;
        ViewBag.Movimientos = movimientos;
        ViewBag.Pagos = pagos;

        return View(usuario);
    }

    // ----------------------------------------------------------------
    // POST /Administrador/Suspender/{id}
    // Suspende un usuario. No puede loguearse mientras esté suspendido.
    // El admin debe proveer un motivo (para auditoría futura).
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Suspender(int id, string motivo = "Sin motivo especificado")
    {
        var usuario = await _contexto.Users.FindAsync(id);

        if (usuario == null)
            return NotFound();

        // No permitir auto-suspensión ni suspender a otro admin
        var esAdmin = await _userManager.IsInRoleAsync(usuario, "Admin");
        if (esAdmin)
        {
            TempData["Error"] = "No podés suspender a otro administrador.";
            return RedirectToAction("Usuarios");
        }

        usuario.EstadoUsuario = EstadoUsuario.Suspendido;

        await _contexto.SaveChangesAsync();

        // TODO: registrar en tabla de auditoría: quién suspendió, cuándo, por qué
        TempData["Exito"] = $"Usuario {usuario.Nombres} {usuario.Apellidos} suspendido.";

        return RedirectToAction("DetalleUsuario", new { id });
    }

    // ----------------------------------------------------------------
    // POST /Administrador/Reactivar/{id}
    // Reactiva un usuario suspendido. Vuelve a estado Activo.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Reactivar(int id)
    {
        var usuario = await _contexto.Users.FindAsync(id);

        if (usuario == null)
            return NotFound();

        usuario.EstadoUsuario = EstadoUsuario.Activo;

        await _contexto.SaveChangesAsync();

        // TODO: registrar en tabla de auditoría
        TempData["Exito"] = $"Usuario {usuario.Nombres} {usuario.Apellidos} reactivado.";

        return RedirectToAction("DetalleUsuario", new { id });
    }
}
