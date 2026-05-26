// ============================================================
// ArchivosController.cs
// Sirve PDFs desde el volumen persistente.
// Verifica que el usuario tenga pago confirmado para el producto.
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class ArchivosController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public ArchivosController(ContextoAplicacion contexto, UserManager<Usuario> userManager)
    {
        _contexto  = contexto;
        _userManager = userManager;
    }

    // ----------------------------------------------------------------
    // GET /Archivos/Pdf?id=13
    // Sirve un PDF verificando que el usuario tenga acceso al producto
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Pdf(int id)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null) return Challenge();

        // Buscar el PDF y su producto
        var pdf = await _contexto.ProductoPdfs
            .Include(p => p.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pdf == null) return NotFound();

        // Admin puede ver todo
        var esAdmin = await _userManager.IsInRoleAsync(usuario, "Admin");

        if (!esAdmin)
        {
            // Verificar que el usuario pagó el producto
            bool tienePago = await _contexto.Pagos.AnyAsync(p =>
                p.UsuarioId == usuario.Id &&
                p.ProductoId == pdf.ProductoId &&
                p.Confirmado);

            if (!tienePago) return Forbid();
        }

        // Construir ruta física
        var rutaLimpia = pdf.Url.TrimStart('/').Replace("..", string.Empty);
        var rutaFisica = Path.GetFullPath(Path.Combine("/app", rutaLimpia));

        if (!rutaFisica.StartsWith("/app/storage/", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound();

        // Inline para visor, attachment para descarga
        var disposition = Request.Query["download"] == "1" ? "attachment" : "inline";
        Response.Headers["Content-Disposition"] = $"{disposition}; filename=\"{pdf.Nombre}.pdf\"";

        return PhysicalFile(rutaFisica, "application/pdf");
    }
}