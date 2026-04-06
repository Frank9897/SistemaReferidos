// ============================================================
// RankingController.cs
// Ubicación: Controllers/RankingController.cs
//
// MEJORA: pasa la tabla completa de rangos con puntos y bonus,
// y el progreso del usuario actual hacia el siguiente rango.
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class RankingController : Controller
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioRangos _servicioRangos;

    public RankingController(
        ContextoAplicacion contexto,
        UserManager<Usuario> userManager,
        ServicioRangos servicioRangos)
    {
        _contexto = contexto;
        _userManager = userManager;
        _servicioRangos = servicioRangos;
    }

    public async Task<IActionResult> Index()
    {
        var ranking = await _contexto.Users
            .OrderByDescending(x => x.PuntosAcumulados)
            .Take(20)
            .ToListAsync();

        var rangos = await _servicioRangos.ObtenerTodosAsync();

        var usuario = await _userManager.GetUserAsync(User);

        // Calcular progreso dentro del rango actual
        RangoUsuario? rangoActual    = null;
        RangoUsuario? rangoSiguiente = null;
        int puntosFaltantes   = 0;
        int progresoEnRango   = 0;

        if (usuario != null)
        {
            rangoActual    = rangos.FirstOrDefault(r => r.TipoRango == usuario.TipoRangoActual);
            rangoSiguiente = rangos.FirstOrDefault(r => r.Orden == (rangoActual?.Orden ?? 0) + 1);

            if (rangoActual != null && rangoSiguiente != null)
            {
                int span              = rangoSiguiente.PuntosMinimos - rangoActual.PuntosMinimos;
                int puntosDentro      = usuario.PuntosAcumulados - rangoActual.PuntosMinimos;
                progresoEnRango       = span > 0
                    ? (int)Math.Clamp(puntosDentro * 100.0 / span, 0, 100)
                    : 100;
                puntosFaltantes = Math.Max(0, rangoSiguiente.PuntosMinimos - usuario.PuntosAcumulados);
            }
            else
            {
                // Rango máximo alcanzado
                progresoEnRango = 100;
                puntosFaltantes = 0;
            }
        }

        ViewBag.Rangos          = rangos;
        ViewBag.UsuarioActual   = usuario;
        ViewBag.RangoActual     = rangoActual;
        ViewBag.RangoSiguiente  = rangoSiguiente;
        ViewBag.PuntosFaltantes = puntosFaltantes;
        ViewBag.ProgresoEnRango = progresoEnRango;

        return View(ranking);
    }
}
