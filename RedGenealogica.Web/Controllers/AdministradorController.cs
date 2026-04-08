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
    // DASHBOARD (página principal del admin)
    // ================================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Usuarios
        var totalUsuarios   = await _contexto.Users.CountAsync();
        var totalActivos    = await _contexto.Users.CountAsync(u => u.EstadoUsuario == EstadoUsuario.Activo);
        var totalPendientes = await _contexto.Users.CountAsync(u => u.EstadoUsuario == EstadoUsuario.Pendiente);
        var totalSuspendidos= await _contexto.Users.CountAsync(u => u.EstadoUsuario == EstadoUsuario.Suspendido);

        // Referidos
        var totalReferidos  = await _contexto.Referidos.CountAsync();
        var referidosPagados= await _contexto.Referidos.CountAsync(r => r.PagoConfirmado);

        // Pagos
        var totalPagos      = await _contexto.Pagos.CountAsync(p => p.Confirmado);
        var ingresoBruto    = await _contexto.Pagos
            .Where(p => p.Confirmado)
            .SumAsync(p => (decimal?)p.Monto) ?? 0m;

        // Ciclos y premios
        var totalCiclos     = await _contexto.Users.SumAsync(u => u.CiclosCompletados);
        var totalPremios    = await _contexto.MovimientosPuntos
            .Where(m => m.Monto > 0 && m.Nivel == 0)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;
        var totalBonosAbuelo= await _contexto.MovimientosPuntos
            .Where(m => m.Monto > 0 && m.Nivel == 1)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        // Retiros
        var pendientesRetiro   = await _contexto.SolicitudesRetiro.CountAsync(s => s.Estado == EstadoRetiro.Pendiente);
        var totalRetirado      = await _contexto.SolicitudesRetiro
            .Where(s => s.Estado == EstadoRetiro.Completado)
            .SumAsync(s => (decimal?)s.Monto) ?? 0m;
        var saldoEnCirculacion = await _contexto.Users
            .SumAsync(u => u.SaldoDisponible + u.SaldoPendienteRetiro);

        // Productos
        var totalProductos     = await _contexto.Productos.CountAsync(p => p.Activo);

        // Distribución por rango
        var distribRangos = await _contexto.Users
            .GroupBy(u => u.TipoRangoActual)
            .Select(g => new { Rango = g.Key.ToString(), Cantidad = g.Count() })
            .ToListAsync();

        // Últimos 5 usuarios registrados
        var ultimosUsuarios = await _contexto.Users
            .OrderByDescending(u => u.FechaRegistro)
            .Take(5)
            .ToListAsync();

        // Últimos 5 pagos
        var ultimosPagos = await _contexto.Pagos
            .Where(p => p.Confirmado)
            .Include(p => p.Usuario)
            .Include(p => p.Producto)
            .OrderByDescending(p => p.FechaConfirmacion)
            .Take(5)
            .ToListAsync();

        ViewBag.TotalUsuarios      = totalUsuarios;
        ViewBag.TotalActivos       = totalActivos;
        ViewBag.TotalPendientes    = totalPendientes;
        ViewBag.TotalSuspendidos   = totalSuspendidos;
        ViewBag.TotalReferidos     = totalReferidos;
        ViewBag.ReferidosPagados   = referidosPagados;
        ViewBag.TotalPagos         = totalPagos;
        ViewBag.IngresoBruto       = ingresoBruto;
        ViewBag.TotalCiclos        = totalCiclos;
        ViewBag.TotalPremios       = totalPremios;
        ViewBag.TotalBonosAbuelo   = totalBonosAbuelo;
        ViewBag.PendientesRetiro   = pendientesRetiro;
        ViewBag.TotalRetirado      = totalRetirado;
        ViewBag.SaldoEnCirculacion = saldoEnCirculacion;
        ViewBag.TotalProductos     = totalProductos;
        ViewBag.DistribRangos      = distribRangos;
        ViewBag.UltimosUsuarios    = ultimosUsuarios;
        ViewBag.UltimosPagos       = ultimosPagos;

        return View();
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
    [ValidateAntiForgeryToken]
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
    [ValidateAntiForgeryToken]
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
            .Include(p => p.Pdfs)
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
        List<IFormFile>? archivosPdf,
        List<string>? nombresPdf)
    {
        if (modelo.PorcentajeAbueloComision > 66)
            ModelState.AddModelError("PorcentajeAbueloComision",
                "El porcentaje máximo permitido es 66%.");

        if (!ModelState.IsValid)
            return View(modelo);

        modelo.FechaCreacion = DateTime.UtcNow;
        modelo.Activo = true;

        _contexto.Productos.Add(modelo);
        await _contexto.SaveChangesAsync();

        if (archivosPdf != null && archivosPdf.Any())
            await GuardarPdfs(modelo.Id, archivosPdf, nombresPdf ?? []);

        TempData["Exito"] = $"Producto '{modelo.Nombre}' creado correctamente.";
        return RedirectToAction("Productos");
    }

    [HttpGet]
    public async Task<IActionResult> EditarProducto(int id)
    {
        var producto = await _contexto.Productos
            .Include(p => p.Pdfs.OrderBy(pdf => pdf.Orden))
            .FirstOrDefaultAsync(p => p.Id == id);
        if (producto == null) return NotFound();

        return View(producto);
    }

    [HttpPost]
    public async Task<IActionResult> EditarProducto(
        Producto modelo,
        List<IFormFile>? archivosPdf,
        List<string>? nombresPdf)
    {
        if (modelo.PorcentajeAbueloComision > 66)
            ModelState.AddModelError("PorcentajeAbueloComision",
                "El porcentaje máximo permitido es 66%.");

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

        await _contexto.SaveChangesAsync();

        if (archivosPdf != null && archivosPdf.Any())
            await GuardarPdfs(producto.Id, archivosPdf, nombresPdf ?? []);

        TempData["Exito"] = $"Producto '{producto.Nombre}' actualizado.";
        return RedirectToAction("EditarProducto", new { id = producto.Id });
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

    // ── Helper: guardar N archivos PDF ──────────────────────────────
    private static readonly string[] _extPdf = [".pdf"];

    private async Task GuardarPdfs(
        int productoId,
        List<IFormFile> archivos,
        List<string> nombres)
    {
        var carpeta = Path.Combine(_env.WebRootPath, "pdfs", productoId.ToString());
        Directory.CreateDirectory(carpeta);

        // Obtener el máximo orden actual
        var ordenActual = await _contexto.ProductoPdfs
            .Where(p => p.ProductoId == productoId)
            .MaxAsync(p => (int?)p.Orden) ?? 0;

        for (int i = 0; i < archivos.Count; i++)
        {
            var archivo = archivos[i];
            if (archivo == null || archivo.Length == 0) continue;

            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!_extPdf.Contains(ext)) continue;

            ordenActual++;
            var nombreArchivo = $"doc{ordenActual}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            var rutaFisica = Path.Combine(carpeta, nombreArchivo);

            await using var fs = new FileStream(rutaFisica, FileMode.Create);
            await archivo.CopyToAsync(fs);

            var nombreVisible = i < nombres.Count && !string.IsNullOrWhiteSpace(nombres[i])
                ? nombres[i]
                : Path.GetFileNameWithoutExtension(archivo.FileName);

            _contexto.ProductoPdfs.Add(new ProductoPdf
            {
                ProductoId  = productoId,
                Nombre      = nombreVisible,
                Url         = $"pdfs/{productoId}/{nombreArchivo}",
                Orden       = ordenActual,
                FechaSubida = DateTime.UtcNow
            });
        }

        await _contexto.SaveChangesAsync();
    }

    // ── Helper: eliminar un PDF específico ───────────────────────────
    [HttpPost]
    public async Task<IActionResult> EliminarPdf(int pdfId, int productoId)
    {
        var pdf = await _contexto.ProductoPdfs.FindAsync(pdfId);
        if (pdf == null || pdf.ProductoId != productoId) return NotFound();

        var ruta = Path.Combine(_env.WebRootPath, pdf.Url.Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);

        _contexto.ProductoPdfs.Remove(pdf);
        await _contexto.SaveChangesAsync();

        TempData["Exito"] = $"Documento '{pdf.Nombre}' eliminado.";
        return RedirectToAction("EditarProducto", new { id = productoId });
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
    [ValidateAntiForgeryToken]
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
    [ValidateAntiForgeryToken]
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
    [ValidateAntiForgeryToken]
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
    [ValidateAntiForgeryToken]
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

    // ================================================================
    // ÁRBOL GLOBAL
    // ================================================================

    [HttpGet]
    public async Task<IActionResult> Arbol()
    {
        // Estadísticas globales para el panel de control
        var totalUsuarios   = await _contexto.Users.CountAsync();
        var totalActivos    = await _contexto.Users
            .CountAsync(u => u.EstadoUsuario == EstadoUsuario.Activo);
        var totalPagos      = await _contexto.Pagos
            .CountAsync(p => p.Confirmado);
        var totalPremios    = await _contexto.Users
            .SumAsync(u => u.SaldoDisponible + u.SaldoPendienteRetiro);
        var totalCiclos     = await _contexto.Users
            .SumAsync(u => u.CiclosCompletados);
        var pendientesRetiro = await _contexto.SolicitudesRetiro
            .CountAsync(s => s.Estado == EstadoRetiro.Pendiente);

        ViewBag.TotalUsuarios     = totalUsuarios;
        ViewBag.TotalActivos      = totalActivos;
        ViewBag.TotalPagos        = totalPagos;
        ViewBag.TotalPremios      = totalPremios;
        ViewBag.TotalCiclos       = totalCiclos;
        ViewBag.PendientesRetiro  = pendientesRetiro;

        return View();
    }

}
