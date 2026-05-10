// ============================================================
// ProductoController.cs
// Página pública de un producto. Accesible sin login.
// Muestra imagen, descripción, precio, qué incluye y CTA.
// Lógica de acceso:
//   - Sin login         → ver info + botón "Comprar / Registrarse"
//   - Logueado sin pago → ver info + botón "Comprar ahora"
//   - Pagado            → botón "Ver mi contenido"
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using System.Security.Claims;
using RedGenealogica.Web.Enumeraciones;
namespace RedGenealogica.Web.Controllers;

[AllowAnonymous]
public class ProductoController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public ProductoController(ContextoAplicacion contexto, UserManager<Usuario> userManager)
    {
        _contexto = contexto;
        _userManager = userManager;
    }

    // GET /Producto/{id}  o  /Producto  (primer activo)
    [HttpGet]
    [Route("Producto/{id:int?}")]
    public async Task<IActionResult> Index(int? id)
    {
        Producto? producto;

        if (id.HasValue)
            producto = await _contexto.Productos
                .Include(p => p.Pdfs.OrderBy(pdf => pdf.Orden))
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo);
        else
            producto = await _contexto.Productos
                .Include(p => p.Pdfs.OrderBy(pdf => pdf.Orden))
                .Where(p => p.Activo)
                .OrderBy(p => p.FechaCreacion)
                .FirstOrDefaultAsync();

        if (producto == null) return NotFound();

        // Estado del usuario respecto a este producto
        ViewBag.EstadoAcceso = "anonimo"; // anonimo | sin_pago | pagado

        if (User.Identity?.IsAuthenticated == true)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario != null)
            {
                bool yaPago = await _contexto.Pagos
                    .AnyAsync(p => p.UsuarioId == usuario.Id
                               && p.ProductoId == producto.Id
                               && p.Confirmado);

                bool esPendiente = !yaPago && usuario.EstadoUsuario == EstadoUsuario.Pendiente;
                ViewBag.EstadoAcceso = yaPago ? "pagado" : esPendiente ? "pendiente_activacion" : "sin_pago";

                // Progreso de ciclo del usuario
                var referidosPagados = await _contexto.Referidos
                    .CountAsync(r => r.UsuarioId == usuario.Id && r.PagoConfirmado);
                ViewBag.ReferidosEnCiclo = referidosPagados % 3;
                ViewBag.FaltanParaCiclo  = 3 - (referidosPagados % 3);
                ViewBag.CiclosCompletados = usuario.CiclosCompletados;

                // Código para compartir
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                ViewBag.LinkRegistro = $"{baseUrl}/Autenticacion/Registro?codigo={usuario.CodigoReferido}";
            }
        }

        // URL pública del producto para compartir (calculada en el controller, no en la vista)
        ViewBag.UrlProducto = $"{Request.Scheme}://{Request.Host}/Producto/{producto.Id}";


        return View(producto);
    }
}
