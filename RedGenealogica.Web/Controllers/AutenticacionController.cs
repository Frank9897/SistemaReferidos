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
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
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
        // Redirigir si debe cambiar password temporal
        if (usuario.DebeambiarPassword)
            return RedirectToAction("CambiarPasswordTemporal");

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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
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
            lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError("", "Contraseña incorrecta");
            return View(modelo);
        }

        if (usuario.DebeambiarPassword)
            return RedirectToAction("CambiarPasswordTemporal");

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

    // ----------------------------------------------------------------
    // GET /Autenticacion/LoginGoogle
    // Redirige a Google para autenticación
    // ----------------------------------------------------------------
    [HttpGet]
    public IActionResult LoginGoogle()
    {
        var propiedades = new AuthenticationProperties
        {
            RedirectUri = Url.Action("CallbackGoogle", "Autenticacion")
        };
        return Challenge(propiedades, "Google");
    }

    // ----------------------------------------------------------------
    // GET /Autenticacion/CallbackGoogle
    // Google redirige acá con el resultado
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> CallbackGoogle()
    {
        var resultado = await HttpContext.AuthenticateAsync("Google");

        if (!resultado.Succeeded)
        {
            TempData["Error"] = "No se pudo iniciar sesión con Google.";
            return RedirectToAction("Login");
        }

        var email  = resultado.Principal.FindFirstValue(ClaimTypes.Email);
        var nombre = resultado.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var apellido = resultado.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

        if (string.IsNullOrEmpty(email))
        {
            TempData["Error"] = "No se pudo obtener el email de Google.";
            return RedirectToAction("Login");
        }

        // Buscar si ya existe la cuenta
        var usuario = await _userManager.FindByEmailAsync(email);

        if (usuario == null)
        {
            // Crear cuenta automáticamente
            usuario = new Models.Usuario
            {
                UserName       = email,
                Email          = email,
                Nombres        = nombre,
                Apellidos      = apellido,
                CodigoReferido = Guid.NewGuid().ToString("N")[..8],
                EstadoUsuario  = RedGenealogica.Web.Enumeraciones.EstadoUsuario.Pendiente,
                FechaRegistro  = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var crear = await _userManager.CreateAsync(usuario);
            if (!crear.Succeeded)
            {
                TempData["Error"] = "No se pudo crear la cuenta. Intentá con email y contraseña.";
                return RedirectToAction("Login");
            }

            // Bienvenida por email
            var correos = HttpContext.RequestServices.GetRequiredService<Services.ServicioCorreos>();
            await correos.EnviarBienvenidaAsync(email, $"{nombre} {apellido}".Trim());
        }

        // Verificar que no esté suspendido
        if (usuario.EstadoUsuario == RedGenealogica.Web.Enumeraciones.EstadoUsuario.Suspendido ||
            usuario.EstadoUsuario == RedGenealogica.Web.Enumeraciones.EstadoUsuario.Inactivo)
        {
            TempData["Error"] = "Tu cuenta está suspendida. Contactá con soporte.";
            return RedirectToAction("Login");
        }

        await _signInManager.SignInAsync(usuario, isPersistent: true);

        if (usuario.DebeambiarPassword)
            return RedirectToAction("CambiarPasswordTemporal");

        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Autenticacion/CambiarPasswordTemporal
    // ----------------------------------------------------------------
    [HttpGet]
    [Authorize]
    public IActionResult CambiarPasswordTemporal()
    {
        return View();
    }

    // ----------------------------------------------------------------
    // POST /Autenticacion/CambiarPasswordTemporal
    // ----------------------------------------------------------------
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPasswordTemporal(string passwordActual, string passwordNuevo, string passwordConfirmar)
    {
        if (passwordNuevo != passwordConfirmar)
        {
            TempData["Error"] = "Las contraseñas no coinciden.";
            return View();
        }

        if (passwordNuevo.Length < 8)
        {
            TempData["Error"] = "La contraseña debe tener al menos 8 caracteres.";
            return View();
        }

        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null) return RedirectToAction("Login");

        var resultado = await _userManager.ChangePasswordAsync(usuario, passwordActual, passwordNuevo);

        if (!resultado.Succeeded)
        {
            TempData["Error"] = resultado.Errors.FirstOrDefault()?.Description ?? "Error al cambiar contraseña.";
            return View();
        }

        usuario.DebeambiarPassword = false;
        await _userManager.UpdateAsync(usuario);

        // Generar token de verificación de email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);
        var urlVerificacion = Url.Action("VerificarEmail", "Autenticacion",
            new { userId = usuario.Id, token = token }, Request.Scheme)!
            .Replace("http://", "https://");

        var correos = HttpContext.RequestServices.GetRequiredService<ServicioCorreos>();
        await correos.EnviarVerificacionEmailAsync(usuario.Email!, $"{usuario.Nombres} {usuario.Apellidos}", urlVerificacion);

        TempData["Exito"] = "Contraseña cambiada. Te enviamos un email para verificar tu cuenta.";
        return RedirectToAction("Panel", "Usuario");
    }

    // ----------------------------------------------------------------
    // GET /Autenticacion/VerificarEmail
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> VerificarEmail(int userId, string token)
    {
        var usuario = await _userManager.FindByIdAsync(userId.ToString());
        if (usuario == null) return RedirectToAction("Login");

        var resultado = await _userManager.ConfirmEmailAsync(usuario, token);

        if (resultado.Succeeded)
            TempData["Exito"] = "✅ Email verificado correctamente. ¡Bienvenido!";
        else
            TempData["Error"] = "El link de verificación no es válido o ya fue usado.";

        return RedirectToAction("Panel", "Usuario");
    }
}