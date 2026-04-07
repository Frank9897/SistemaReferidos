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

    [HttpGet]
    public async Task<IActionResult> MiContenido()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var esAdmin   = User.IsInRole("Admin");

        List<RedGenealogica.Web.Models.Producto> productos;

        if (esAdmin)
        {
            productos = await _contexto.Productos
                .Where(p => p.Activo)
                .Include(p => p.Pdfs)
                .ToListAsync();
        }
        else
        {
            productos = await _contexto.Pagos
                .Where(p => p.UsuarioId == usuarioId && p.Confirmado)
                .Select(p => p.Producto!)
                .Where(p => p != null)
                .Distinct()
                .Include(p => p.Pdfs)
                .ToListAsync();
        }

        return View(productos);
    }

    [HttpGet]
    public async Task<IActionResult> VerProducto(int id)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var esAdmin   = User.IsInRole("Admin");

        var producto = await _contexto.Productos
            .Include(p => p.Pdfs.OrderBy(pdf => pdf.Orden))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (producto == null) return NotFound();

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

    [HttpGet]
    public async Task<IActionResult> DescargarPdf(int pdfId)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var esAdmin   = User.IsInRole("Admin");

        var pdf = await _contexto.ProductoPdfs
            .Include(p => p.Producto)
            .FirstOrDefaultAsync(p => p.Id == pdfId);

        if (pdf == null) return NotFound();

        if (!esAdmin)
        {
            bool tieneAcceso = await _contexto.Pagos
                .AnyAsync(p => p.UsuarioId == usuarioId
                            && p.ProductoId == pdf.ProductoId
                            && p.Confirmado);

            if (!tieneAcceso) return Forbid();
        }

        var rutaAbsoluta = Path.Combine(
            _env.WebRootPath,
            pdf.Url.Replace('/', Path.DirectorySeparatorChar));

        if (!System.IO.File.Exists(rutaAbsoluta)) return NotFound();

        var bytes        = await System.IO.File.ReadAllBytesAsync(rutaAbsoluta);
        var nombreLimpio = pdf.Nombre.Replace("\"", "") + ".pdf";

        Response.Headers["Content-Disposition"] = "attachment; filename=\"" + nombreLimpio + "\"";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(bytes, "application/pdf", nombreLimpio);
    }
}
