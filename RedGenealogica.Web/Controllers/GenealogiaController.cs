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

        if (usuario == null)
            return Unauthorized();

        var referidos = await _contexto.Users
            .Where(x => x.IdUsuarioPadre == usuario.Id)
            .ToListAsync();

        return View(referidos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerArbol()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return Unauthorized();

        var usuarios = await _contexto.Users
            .Select(u => new
            {
                id = "U_" + u.Id,
                nombre = u.Nombres,
                padreId = u.IdUsuarioPadre != null ? "U_" + u.IdUsuarioPadre : null
            })
            .ToListAsync();

        var referidos = await _contexto.Referidos
            .Select(r => new
            {
                id = "R_" + r.Id,
                nombre = r.NombreCompleto,
                padreId = (string?)("U_" + r.UsuarioId) // 🔥 clave
            })
            .ToListAsync();

        var nodos = usuarios.Concat(referidos);

        return Json(nodos);
    }
}