using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class ReferidosController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioReferidos _servicioReferidos;

    public ReferidosController(
        UserManager<Usuario> userManager,
        ServicioReferidos servicioReferidos)
    {
        _userManager = userManager;
        _servicioReferidos = servicioReferidos;
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Crear(RegistrarReferidoViewModel modelo)
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _servicioReferidos.RegistrarReferidoAsync(usuario.Id, modelo);

        if (referido == null)
        {
            ModelState.AddModelError("", "Debes estar activo para referir");
            return View(modelo);
        }

        return RedirectToAction("Panel", "Usuario");
    }

    [HttpPost]
    public async Task<IActionResult> Activar(int id)
    {
        await _servicioReferidos.ActivarReferidoAsync(id);

        // 🔥 convertir a usuario automáticamente
        await _servicioReferidos.ConvertirReferidoAUsuarioAsync(id);

        return RedirectToAction("Panel", "Usuario");
    }
}