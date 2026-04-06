using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Controllers;

[AllowAnonymous]
public class InicioController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public InicioController(ContextoAplicacion contexto, UserManager<Usuario> userManager)
    {
        _contexto = contexto;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        // Pasar el primer producto activo para mostrar en el landing
        var producto = await _contexto.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.FechaCreacion)
            .FirstOrDefaultAsync();

        ViewBag.Producto = producto;

        // Si el usuario está logueado, calcular su progreso de ciclo
        if (User.Identity?.IsAuthenticated == true)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario != null)
            {
                var referidosPagados = await _contexto.Referidos
                    .CountAsync(r => r.UsuarioId == usuario.Id && r.PagoConfirmado);
                ViewBag.ReferidosEnCicloActual = referidosPagados % 3;
                ViewBag.FaltanParaCiclo = 3 - (referidosPagados % 3);
            }
        }

        return View();
    }
}
