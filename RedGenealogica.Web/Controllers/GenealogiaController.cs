using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class GenealogiaController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public GenealogiaController(ContextoAplicacion contexto, UserManager<Usuario> userManager)
    {
        _contexto = contexto;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var usuario = await _userManager.GetUserAsync(User);

        var referidos = await _contexto.Users
            .Where(x => x.IdUsuarioPadre == usuario.Id)
            .ToListAsync();

        return View(referidos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerArbol()
    {
        var usuario = await _userManager.GetUserAsync(User);

        var nodos = await _contexto.Users
            .Select(u => new
            {
                id = u.Id,
                nombre = u.Nombres,
                padreId = u.IdUsuarioPadre
            })
            .ToListAsync();

        return Json(nodos);
    }
}