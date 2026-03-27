using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.ViewModels;
namespace RedGenealogica.Web.Controllers;

[Authorize]
public class UsuarioController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioPagos _servicioPagos;
    private readonly ContextoAplicacion _contexto;

    public UsuarioController(UserManager<Usuario> userManager,
                            ServicioPagos servicioPagos,
                            ContextoAplicacion contexto)
    {
        _userManager = userManager;
        _servicioPagos = servicioPagos;
        _contexto = contexto;
    }

    public async Task<IActionResult> Panel()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuario.Id)
            .ToListAsync();

        var modelo = new PanelUsuarioViewModel
        {
            Usuario = usuario,
            Referidos = referidos
        };

        return View(modelo);
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