using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;

namespace RedGenealogica.Web.Controllers;

public class AutenticacionController : Controller
{
    private readonly ServicioUsuarios _servicioUsuarios;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly UserManager<Usuario> _userManager;

    public AutenticacionController(
        ServicioUsuarios servicioUsuarios,
        SignInManager<Usuario> signInManager,
        UserManager<Usuario> userManager)
    {
        _servicioUsuarios = servicioUsuarios;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Registro()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Registro(RegistroUsuarioViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var usuario = await _servicioUsuarios.RegistrarAsync(modelo);

        if (usuario == null)
        {
            ModelState.AddModelError("", "Error al registrar");
            return View(modelo);
        }

        await _signInManager.SignInAsync(usuario, isPersistent: false);

        return RedirectToAction("Index", "Inicio");
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await _signInManager.PasswordSignInAsync(
            modelo.Email,
            modelo.Password,
            false,
            false);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError("", "Credenciales incorrectas");
            return View(modelo);
        }

        return RedirectToAction("Index", "Inicio");
    }

    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }
}