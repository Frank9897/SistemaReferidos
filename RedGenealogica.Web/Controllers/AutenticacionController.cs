// ============================================================
// AutenticacionController.cs
// Ubicación: Controllers/AutenticacionController.cs
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;

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

    // ----------------------------------------------------------------
    // GET /Autenticacion/Registro
    // Muestra el formulario de registro.
    // Si la URL trae ?codigo=XXXX, pre-rellena el campo de código padre.
    // ----------------------------------------------------------------
    [HttpGet]
    public IActionResult Registro(string? codigo = null)
    {
        var modelo = new RegistroUsuarioViewModel
        {
            CodigoReferidoPadre = codigo
        };
        return View(modelo);
    }

    // ----------------------------------------------------------------
    // POST /Autenticacion/Registro
    // [BUG-6 + BUG-7 CORREGIDOS] Delega toda la creación al servicio.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Registro(RegistroUsuarioViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var (usuario, errores) = await _servicioUsuarios.RegistrarAsync(modelo);

        if (usuario == null)
        {
            foreach (var error in errores)
                ModelState.AddModelError("", error);

            return View(modelo);
        }

        // Loguear automáticamente después del registro
        await _signInManager.SignInAsync(usuario, isPersistent: true);

        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Autenticacion/Login
    // ----------------------------------------------------------------
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // ----------------------------------------------------------------
    // POST /Autenticacion/Login
    // [MEJORA] Verifica que el usuario no esté suspendido antes de
    // permitir el acceso, sin importar que las credenciales sean correctas.
    // ----------------------------------------------------------------
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

        // [MEJORA] Bloquear acceso a usuarios suspendidos o baneados
        // Esto es independiente de la contraseña
        if (usuario.EstadoUsuario == EstadoUsuario.Suspendido ||
            usuario.EstadoUsuario == EstadoUsuario.Inactivo)
        {
            ModelState.AddModelError("", "Tu cuenta está suspendida. Contactá con soporte.");
            return View(modelo);
        }

        var resultado = await _signInManager.PasswordSignInAsync(
            usuario.UserName!,
            modelo.Password,
            isPersistent: true,
            lockoutOnFailure: false);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError("", "Contraseña incorrecta");
            return View(modelo);
        }

        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // POST /Autenticacion/Logout
    // ----------------------------------------------------------------
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Autenticacion");
    }
}
