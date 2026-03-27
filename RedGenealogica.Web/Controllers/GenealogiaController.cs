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
}