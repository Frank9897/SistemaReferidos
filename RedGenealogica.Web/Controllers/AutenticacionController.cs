using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;
using RedGenealogica.Web.Enumeraciones;
using Microsoft.AspNetCore.Authorization;
namespace RedGenealogica.Web.Controllers;

[AllowAnonymous]
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

        var codigo = Guid.NewGuid().ToString("N").Substring(0, 8);

        var usuario = new Usuario
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            Nombres = modelo.Nombres,
            Apellidos = modelo.Apellidos,
            CodigoReferido = codigo,
            EstadoUsuario = EstadoUsuario.Pendiente
        };

        var resultado = await _userManager.CreateAsync(usuario, modelo.Password);

        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(modelo);
        }

        await _signInManager.SignInAsync(usuario, isPersistent: true);

        return RedirectToAction("Panel", "Usuario");
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

        var usuario = await _userManager.FindByEmailAsync(modelo.Email);

        if (usuario == null)
        {
            ModelState.AddModelError("", "Usuario no encontrado");
            return View(modelo);
        }

        var resultado = await _signInManager.PasswordSignInAsync(
            usuario.UserName!,
            modelo.Password,
            isPersistent: true,
            lockoutOnFailure: false);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError("", "Credenciales incorrectas");
            return View(modelo);
        }

        return RedirectToAction("Panel", "Usuario");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Autenticacion");
    }
}