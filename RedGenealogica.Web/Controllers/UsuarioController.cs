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

        var totalReferidosActivos = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == usuario.Id && r.Estado == EstadoUsuario.Activo);

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

        int referidosIndirectos = ContarReferidosIndirectos(usuario.Id, todosLosUsuarios);

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
            {
                progreso = (int)Math.Clamp((avanzados * 100m) / baseRango, 0, 100);
            }
        }

        var modelo = new PanelUsuarioViewModel
        {
            Usuario = usuario,
            Referidos = referidos,
            TotalReferidosDirectos = referidos.Count,
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

    private static int ContarReferidosIndirectos(int usuarioId, List<Usuario> usuarios)
    {
        var hijos = usuarios.Where(u => u.IdUsuarioPadre == usuarioId).ToList();
        if (hijos.Count == 0)
            return 0;

        int total = 0;

        foreach (var hijo in hijos)
        {
            total += 1;
            total += ContarReferidosIndirectos(hijo.Id, usuarios);
        }

        return total - hijos.Count;
    }

    [HttpPost]
    public async Task<IActionResult> ActivarCuenta()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        // 🔍 Buscar referido pendiente del usuario
        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r =>
                r.UsuarioId == usuario.Id &&
                r.Estado != EstadoUsuario.Activo);

        if (referido == null)
        {
            TempData["Error"] = "No tienes referidos pendientes para activar.";
            return RedirectToAction("Panel");
        }

        if (referido.Producto == null)
        {
            TempData["Error"] = "El referido no tiene producto asignado.";
            return RedirectToAction("Panel");
        }

        // 💳 REDIRIGIR A MERCADOPAGO (flujo real)
        var urlPago = await _servicioPagos.CrearPreferencia(referido.Id);

        return Redirect(urlPago);
    }
    
}