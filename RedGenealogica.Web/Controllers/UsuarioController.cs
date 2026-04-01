// ============================================================
// UsuarioController.cs
// Ubicación: Controllers/UsuarioController.cs
//
// CORRECCIÓN: reemplazadas las 2 comparaciones contra EstadoUsuario
// por EstadoReferido, que es el tipo correcto del campo Referido.Estado
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.ViewModels;
using RedGenealogica.Web.Enumeraciones;
using System.Security.Claims;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class UsuarioController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioPagos _servicioPagos;
    private readonly ContextoAplicacion _contexto;

    public UsuarioController(
        UserManager<Usuario> userManager,
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
            
        ViewBag.Activado = Request.Query["activado"] == "1";

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuario.Id)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();

        // [CORREGIDO] Era EstadoUsuario.Activo → ahora EstadoReferido.Convertido
        // Un referido "activo" en el nuevo modelo es uno que fue Convertido a usuario
        var totalReferidosActivos = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == usuario.Id && r.Estado == EstadoReferido.Convertido);

        var totalComisiones = await _contexto.MovimientosPuntos
            .Where(m => m.UsuarioId == usuario.Id)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        var ultimosMovimientos = await _contexto.MovimientosPuntos
            .Where(m => m.UsuarioId == usuario.Id)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(5)
            .ToListAsync();

        var todosLosUsuarios = await _contexto.Users
            .AsNoTracking()
            .ToListAsync();

        var hijosDirectos = todosLosUsuarios
            .Where(u => u.IdUsuarioPadre == usuario.Id)
            .ToList();

        int totalDescendientes = ContarDescendientes(usuario.Id, todosLosUsuarios);

        int referidosIndirectos = totalDescendientes > hijosDirectos.Count 
            ? totalDescendientes - hijosDirectos.Count 
            : 0;

        var rangoActual = await _contexto.RangosUsuario
            .FirstOrDefaultAsync(r => r.TipoRango == usuario.TipoRangoActual);

        var siguienteRango = await _contexto.RangosUsuario
            .Where(r => r.Orden > (rangoActual != null ? rangoActual.Orden : 0))
            .OrderBy(r => r.Orden)
            .FirstOrDefaultAsync();

        int puntosFaltantes = siguienteRango != null
            ? Math.Max(siguienteRango.PuntosMinimos - usuario.PuntosAcumulados, 0)
            : 0;

        int progreso = 100;
        if (rangoActual != null && siguienteRango != null)
        {
            var baseRango = siguienteRango.PuntosMinimos - rangoActual.PuntosMinimos;
            var avanzados = usuario.PuntosAcumulados - rangoActual.PuntosMinimos;

            if (baseRango > 0)
                progreso = (int)Math.Clamp((avanzados * 100m) / baseRango, 0, 100);
        }

        var modelo = new PanelUsuarioViewModel
        {
            Usuario = usuario,
            Referidos = referidos,
            TotalReferidosDirectos = hijosDirectos.Count,
            TotalReferidosRegistrados = referidos.Count,
            TotalReferidosIndirectos = referidosIndirectos,
            TotalReferidosActivos = totalReferidosActivos,
            TotalComisiones = totalComisiones,
            SiguienteRango = siguienteRango?.NombreVisible,
            PuntosFaltantesParaSiguienteRango = puntosFaltantes,
            ProgresoRangoPorcentaje = progreso,
            UltimosMovimientos = ultimosMovimientos
        };

        return View(modelo);
    }

    private static int ContarDescendientes(int usuarioId, List<Usuario> usuarios)
    {
        var hijos = usuarios.Where(u => u.IdUsuarioPadre == usuarioId).ToList();

        int total = 0;

        foreach (var hijo in hijos)
        {
            total += 1;
            total += ContarDescendientes(hijo.Id, usuarios);
        }

        return total;
    }

    [HttpPost]
    public async Task<IActionResult> ActivarCuenta()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .Where(r => r.UsuarioId == usuario.Id)
            .OrderBy(r => r.FechaRegistro)
            .FirstOrDefaultAsync();

        if (referido == null)
        {
            TempData["Error"] = "Primero tenés que registrar un referido para activar tu cuenta.";
            return RedirectToAction("Panel");
        }

        if (referido.Estado == EstadoReferido.Convertido || referido.Estado == EstadoReferido.Pagado)
        {
            TempData["Error"] = "Tu cuenta ya está activada.";
            return RedirectToAction("Panel");
        }

        if (referido.Estado != EstadoReferido.Pendiente)
        {
            TempData["Error"] = "Este referido ya fue procesado.";
            return RedirectToAction("Panel");
        }

        if (referido.Producto == null)
        {
            TempData["Error"] = "El referido no tiene producto asignado.";
            return RedirectToAction("Panel");
        }

        var urlPago = await _servicioPagos.CrearPreferencia(referido.Id);
        return Redirect(urlPago);
    }

    // GET /Usuario/SolicitarRetiro
    [HttpGet]
    public async Task<IActionResult> SolicitarRetiro()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        // Cargar los últimos 5 retiros para mostrar historial
        var historial = await _contexto.SolicitudesRetiro
            .Where(s => s.UsuarioId == usuario.Id)
            .OrderByDescending(s => s.FechaSolicitud)
            .Take(5)
            .ToListAsync();

        ViewBag.Usuario  = usuario;
        ViewBag.Historial = historial;
        return View();
    }

    // POST /Usuario/SolicitarRetiro
    [HttpPost]
    public async Task<IActionResult> SolicitarRetiro(decimal monto, string cbuAlias)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var servicioRetiros = HttpContext.RequestServices
            .GetRequiredService<ServicioRetiros>();

        var (exito, mensaje) = await servicioRetiros
            .SolicitarRetiroAsync(usuario.Id, monto, cbuAlias);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        return RedirectToAction("SolicitarRetiro");
    }
}
