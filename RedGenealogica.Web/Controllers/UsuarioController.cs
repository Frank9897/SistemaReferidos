using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class UsuarioController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioPagos _servicioPagos;

    public UsuarioController(
        UserManager<Usuario> userManager,
        ServicioPagos servicioPagos)
    {
        _userManager = userManager;
        _servicioPagos = servicioPagos;
    }

    public async Task<IActionResult> Panel()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        return View(usuario);
    }

    [HttpPost]
    public async Task<IActionResult> ActivarCuenta()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        // ⚠️ producto fijo por ahora (luego lo haremos dinámico)
        int productoId = 1;
        decimal monto = 100;

        await _servicioPagos.CrearPagoYActivarUsuarioAsync(usuario.Id, productoId, monto);

        return RedirectToAction("Panel");
    }
}