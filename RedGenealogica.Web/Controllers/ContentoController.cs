// ============================================================
// ContentoController.cs
// Ubicación: Controllers/ContentoController.cs
//
// RESPONSABILIDAD:
// Gestionar el acceso al contenido digital de los productos.
// Un usuario puede ver los PDFs de un producto si y solo si:
//   - Tiene un Pago confirmado (Confirmado = true) de ese producto, O
//   - Es administrador.
//
// RUTAS:
//   GET /Contento/MiContenido          → lista de productos desbloqueados
//   GET /Contento/VerProducto/{id}     → vista con los PDFs del producto
//   GET /Contento/DescargarPdf/{id}/{n}→ descarga protegida del PDF (n=1 o 2)
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using System.Security.Claims;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class ContentoController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly IWebHostEnvironment _env;

    public ContentoController(ContextoAplicacion contexto, IWebHostEnvironment env)
    {
        _contexto = contexto;
        _env = env;
    }

    // ── Lista todos los productos que el usuario ya pagó ────────
    [HttpGet]
    public async Task<IActionResult> MiContenido()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Productos que el usuario pagó y fueron confirmados
        var productosDesbloqueados = await _contexto.Pagos
            .Where(p => p.UsuarioId == usuarioId && p.Confirmado)
            .Select(p => p.Producto!)
            .Where(p => p != null)
            .Distinct()
            .ToListAsync();

        return View(productosDesbloqueados);
    }

    // ── Vista del producto con los PDFs disponibles ─────────────
    [HttpGet]
    public async Task<IActionResult> VerProducto(int id)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var esAdmin   = User.IsInRole("Admin");

        var producto = await _contexto.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        // Verificar acceso
        if (!esAdmin)
        {
            bool tieneAcceso = await _contexto.Pagos
                .AnyAsync(p => p.UsuarioId == usuarioId
                            && p.ProductoId == id
                            && p.Confirmado);

            if (!tieneAcceso)
            {
                TempData["Error"] = "Necesitás haber pagado este producto para acceder al contenido.";
                return RedirectToAction("MiContenido");
            }
        }

        return View(producto);
    }

    // ── Descarga protegida del PDF ───────────────────────────────
    // n = 1 (PdfUrl1) o n = 2 (PdfUrl2)
    [HttpGet]
    public async Task<IActionResult> DescargarPdf(int id, int n)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var esAdmin   = User.IsInRole("Admin");

        var producto = await _contexto.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        // Verificar acceso
        if (!esAdmin)
        {
            bool tieneAcceso = await _contexto.Pagos
                .AnyAsync(p => p.UsuarioId == usuarioId
                            && p.ProductoId == id
                            && p.Confirmado);

            if (!tieneAcceso) return Forbid();
        }

        // Resolver la URL del PDF
        string? pdfUrl = n == 1 ? producto.PdfUrl1 : producto.PdfUrl2;
        if (string.IsNullOrEmpty(pdfUrl)) return NotFound();

        var rutaAbsoluta = Path.Combine(_env.WebRootPath, pdfUrl.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(rutaAbsoluta)) return NotFound();

        var nombreDescarga = n == 1
            ? (producto.PdfNombre1 ?? "documento-1") + ".pdf"
            : (producto.PdfNombre2 ?? "documento-2") + ".pdf";

        var bytes = await System.IO.File.ReadAllBytesAsync(rutaAbsoluta);
        return File(bytes, "application/pdf", nombreDescarga);
    }
}
