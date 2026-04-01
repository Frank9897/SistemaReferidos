// ============================================================
// ServicioNotificaciones.cs
// Ubicación: Services/ServicioNotificaciones.cs
//
// Centraliza la creación y lectura de notificaciones.
// Se inyecta en ServicioPagos y ServicioRetiros para disparar
// notificaciones automáticas en los eventos clave del negocio.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Services;

public class ServicioNotificaciones
{
    private readonly ContextoAplicacion _contexto;

    public ServicioNotificaciones(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    // ----------------------------------------------------------------
    // Crea una notificación para un usuario específico.
    // Llamado desde ServicioPagos y ServicioRetiros.
    // ----------------------------------------------------------------
    public async Task CrearAsync(
        int usuarioId,
        TipoNotificacion tipo,
        string titulo,
        string mensaje,
        string? urlAccion = null)
    {
        _contexto.Notificaciones.Add(new Notificacion
        {
            UsuarioId   = usuarioId,
            Tipo        = tipo,
            Titulo      = titulo,
            Mensaje     = mensaje,
            UrlAccion   = urlAccion,
            Leida       = false,
            FechaCreacion = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    // Devuelve las últimas N notificaciones no leídas de un usuario.
    // Usado por el endpoint de polling del frontend.
    // ----------------------------------------------------------------
    public async Task<List<Notificacion>> ObtenerNoLeidasAsync(int usuarioId, int max = 10)
    {
        return await _contexto.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(max)
            .ToListAsync();
    }

    // ----------------------------------------------------------------
    // Devuelve el conteo de notificaciones no leídas.
    // Usado para el badge en el navbar.
    // ----------------------------------------------------------------
    public async Task<int> ContarNoLeidasAsync(int usuarioId)
    {
        return await _contexto.Notificaciones
            .CountAsync(n => n.UsuarioId == usuarioId && !n.Leida);
    }

    // ----------------------------------------------------------------
    // Historial completo paginado para la vista /Notificaciones
    // ----------------------------------------------------------------
    public async Task<List<Notificacion>> ObtenerHistorialAsync(int usuarioId, int pagina = 1, int porPagina = 20)
    {
        return await _contexto.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.FechaCreacion)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .ToListAsync();
    }

    // ----------------------------------------------------------------
    // Marca una notificación específica como leída.
    // Verifica que pertenezca al usuario para evitar accesos cruzados.
    // ----------------------------------------------------------------
    public async Task MarcarLeidaAsync(int notificacionId, int usuarioId)
    {
        var notif = await _contexto.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == notificacionId && n.UsuarioId == usuarioId);

        if (notif == null) return;

        notif.Leida = true;
        await _contexto.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    // Marca todas las notificaciones de un usuario como leídas.
    // ----------------------------------------------------------------
    public async Task MarcarTodasLeidasAsync(int usuarioId)
    {
        var noLeidas = await _contexto.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leida)
            .ToListAsync();

        foreach (var n in noLeidas)
            n.Leida = true;

        await _contexto.SaveChangesAsync();
    }
}
