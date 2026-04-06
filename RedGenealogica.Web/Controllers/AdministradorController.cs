// ============================================================
// AdministradorController.cs
// Ubicación: Controllers/AdministradorController.cs
//
// NUEVO CONTENIDO — panel admin completo con:
//   - Gestión de usuarios (listar, suspender, reactivar, detalle)
//   - Gestión de productos (listar, crear, editar, activar/desactivar)
//   - Gestión de retiros (listar pendientes, aprobar, rechazar)
//   - Conversión de referido a usuario (acción manual del admin)
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using System.Security.Claims;

namespace RedGenealogica.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdministradorController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioReferidos _servicioReferidos;
    private readonly ServicioRetiros _servicioRetiros;
    private readonly IWebHostEnvironment _env;

    public AdministradorController(
        ContextoAplicacion contexto,
        UserManager<Usuario> userManager,
        ServicioReferidos servicioReferidos,
        ServicioRetiros servicioRetiros,
        IWebHostEnvironment env)
    {
        _contexto = contexto;
        _userManager = userManager;
        _servicioReferidos = servicioReferidos;
        _servicioRetiros = servicioRetiros;
        _env = env;
    }

    // ================================================================
    // USUARIOS
    // ================================================================

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

    [HttpGet]
    public async Task<IActionResult> DetalleUsuario(int id)
    {
        var usuario = await _contexto.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();

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

    [HttpPost]
    public async Task<IActionResult> Suspender(int id)
    {
        var usuario = await _contexto.Users.FindAsync(id);
        if (usuario == null) return NotFound();

        if (await _userManager.IsInRoleAsync(usuario, "Admin"))
        {
            TempData["Error"] = "No podés suspender a otro administrador.";
            return RedirectToAction("Usuarios");
        }

        usuario.EstadoUsuario = EstadoUsuario.Suspendido;
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Usuario {usuario.Nombres} {usuario.Apellidos} suspendido.";
        return RedirectToAction("DetalleUsuario", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reactivar(int id)
    {
        var usuario = await _contexto.Users.FindAsync(id);
        if (usuario == null) return NotFound();

        usuario.EstadoUsuario = EstadoUsuario.Activo;
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Usuario {usuario.Nombres} {usuario.Apellidos} reactivado.";
        return RedirectToAction("DetalleUsuario", new { id });
    }

    // ================================================================
    // CONVERSIÓN DE REFERIDO A USUARIO
    // El admin decide manualmente si un referido (Pagado) quiere
    // convertirse en usuario para poder tener sus propios referidos.
    // ================================================================

    [HttpPost]
    public async Task<IActionResult> ConvertirReferido(int referidoId)
    {
        var (exito, mensaje) = await _servicioReferidos.ConvertirReferidoAUsuarioAsync(referidoId);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        var referido = await _contexto.Referidos.FindAsync(referidoId);
        return RedirectToAction("DetalleUsuario", "Administrador", new { id = referido?.UsuarioId });
    }

    // ================================================================
    // PRODUCTOS
    // ================================================================

    [HttpGet]
    public async Task<IActionResult> Productos()
    {
        var productos = await _contexto.Productos
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return View(productos);
    }

    [HttpGet]
    public IActionResult CrearProducto()
    {
        return View(new Producto());
    }

    [HttpPost]
    public async Task<IActionResult> CrearProducto(
        Producto modelo,
        IFormFile? archivoPdf1,
        IFormFile? archivoPdf2)
    {
        if (modelo.PorcentajeAbueloComision > 66)
        {
            ModelState.AddModelError("PorcentajeAbueloComision",
                "El porcentaje máximo permitido es 66% para garantizar sostenibilidad.");
        }

        if (!ModelState.IsValid)
            return View(modelo);

        modelo.FechaCreacion = DateTime.UtcNow;
        modelo.Activo = true;

        _contexto.Productos.Add(modelo);
        await _contexto.SaveChangesAsync();

        await GuardarPdfs(modelo, archivoPdf1, archivoPdf2);
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Producto '{modelo.Nombre}' creado correctamente.";
        return RedirectToAction("Productos");
    }

    [HttpGet]
    public async Task<IActionResult> EditarProducto(int id)
    {
        var producto = await _contexto.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        return View(producto);
    }

    [HttpPost]
    public async Task<IActionResult> EditarProducto(
        Producto modelo,
        IFormFile? archivoPdf1,
        IFormFile? archivoPdf2)
    {
        if (modelo.PorcentajeAbueloComision > 66)
        {
            ModelState.AddModelError("PorcentajeAbueloComision",
                "El porcentaje máximo permitido es 66% para garantizar sostenibilidad.");
        }

        if (!ModelState.IsValid)
            return View(modelo);

        var producto = await _contexto.Productos.FindAsync(modelo.Id);
        if (producto == null) return NotFound();

        producto.Nombre = modelo.Nombre;
        producto.Descripcion = modelo.Descripcion;
        producto.Precio = modelo.Precio;
        producto.StockDisponible = modelo.StockDisponible;
        producto.ImagenUrl = modelo.ImagenUrl;
        producto.PorcentajeAbueloComision = modelo.PorcentajeAbueloComision;

        // Solo pisar PDF si se subió uno nuevo
        if (archivoPdf1 != null) producto.PdfNombre1 = modelo.PdfNombre1;
        if (archivoPdf2 != null) producto.PdfNombre2 = modelo.PdfNombre2;

        await GuardarPdfs(producto, archivoPdf1, archivoPdf2);
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Producto '{producto.Nombre}' actualizado.";
        return RedirectToAction("Productos");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleProducto(int id)
    {
        var producto = await _contexto.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        producto.Activo = !producto.Activo;
        await _contexto.SaveChangesAsync();

        var estado = producto.Activo ? "activado" : "desactivado";
        TempData["Exito"] = $"Producto '{producto.Nombre}' {estado}.";
        return RedirectToAction("Productos");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarProducto(int id)
    {
        var producto = await _contexto.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        // Verificar que no tenga referidos o pagos asociados
        bool tieneReferidos = await _contexto.Referidos.AnyAsync(r => r.ProductoId == id);
        bool tienePagos     = await _contexto.Pagos.AnyAsync(p => p.ProductoId == id);

        if (tieneReferidos || tienePagos)
        {
            TempData["Error"] = $"No se puede eliminar '{producto.Nombre}' porque tiene referidos o pagos asociados. Podés desactivarlo en su lugar.";
            return RedirectToAction("Productos");
        }

        // Eliminar carpeta de PDFs si existe
        var carpetaPdfs = Path.Combine(_env.WebRootPath, "pdfs", id.ToString());
        if (Directory.Exists(carpetaPdfs))
            Directory.Delete(carpetaPdfs, recursive: true);

        _contexto.Productos.Remove(producto);
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Producto '{producto.Nombre}' eliminado correctamente.";
        return RedirectToAction("Productos");
    }

    // ── Helper: guardar archivos PDF subidos ────────────────────
    private static readonly string[] _extensionesPdfPermitidas = [".pdf"];

    private async Task GuardarPdfs(
        Producto producto,
        IFormFile? pdf1,
        IFormFile? pdf2)
    {
        var carpeta = Path.Combine(_env.WebRootPath, "pdfs", producto.Id.ToString());
        Directory.CreateDirectory(carpeta);

        if (pdf1 != null && pdf1.Length > 0)
        {
            var ext = Path.GetExtension(pdf1.FileName).ToLowerInvariant();
            if (_extensionesPdfPermitidas.Contains(ext))
            {
                var nombre = $"pdf1_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                var ruta   = Path.Combine(carpeta, nombre);
                await using var fs = new FileStream(ruta, FileMode.Create);
                await pdf1.CopyToAsync(fs);
                producto.PdfUrl1 = $"pdfs/{producto.Id}/{nombre}";
                if (string.IsNullOrWhiteSpace(producto.PdfNombre1))
                    producto.PdfNombre1 = Path.GetFileNameWithoutExtension(pdf1.FileName);
            }
        }

        if (pdf2 != null && pdf2.Length > 0)
        {
            var ext = Path.GetExtension(pdf2.FileName).ToLowerInvariant();
            if (_extensionesPdfPermitidas.Contains(ext))
            {
                var nombre = $"pdf2_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                var ruta   = Path.Combine(carpeta, nombre);
                await using var fs = new FileStream(ruta, FileMode.Create);
                await pdf2.CopyToAsync(fs);
                producto.PdfUrl2 = $"pdfs/{producto.Id}/{nombre}";
                if (string.IsNullOrWhiteSpace(producto.PdfNombre2))
                    producto.PdfNombre2 = Path.GetFileNameWithoutExtension(pdf2.FileName);
            }
        }
    }

    // ================================================================
    // RETIROS
    // ================================================================

    [HttpGet]
    public async Task<IActionResult> Retiros()
    {
        var retiros = await _contexto.SolicitudesRetiro
            .Include(r => r.Usuario)
            .OrderByDescending(r => r.FechaSolicitud)
            .ToListAsync();

        return View(retiros);
    }

    [HttpPost]
    public async Task<IActionResult> AprobarRetiro(
        int id, string referenciaTransferencia, string? nota)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (exito, mensaje) = await _servicioRetiros.AprobarRetiroAsync(
            id, adminId, referenciaTransferencia, nota);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        return RedirectToAction("Retiros");
    }

    [HttpPost]
    public async Task<IActionResult> CompletarRetiro(int id, string referenciaTransferencia)
    {
        var (exito, mensaje) = await _servicioRetiros.CompletarRetiroAsync(
            id, referenciaTransferencia);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        return RedirectToAction("Retiros");
    }

    [HttpPost]
    public async Task<IActionResult> RechazarRetiro(int id, string motivo)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (exito, mensaje) = await _servicioRetiros.RechazarRetiroAsync(
            id, adminId, motivo);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        return RedirectToAction("Retiros");
    }

    // ================================================================
    // RESET CONTRASEÑA
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> ResetearPassword(int id, string nuevaPassword)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, nuevaPassword);

        if (resultado.Succeeded)
            TempData["Exito"] = $"Contraseña reseteada para {usuario.Email}";
        else
            TempData["Error"] = string.Join(", ", resultado.Errors.Select(e => e.Description));

        return RedirectToAction("DetalleUsuario", "Administrador", new { id });
    }
}