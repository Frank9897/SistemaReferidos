// ============================================================
// ServicioRangos.cs
// Ubicación: Services/ServicioRangos.cs
//
//   [MEJORA] Método único compartido por toda la aplicación.
//            ServicioReferidos ya no tiene su propia copia privada.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Services;

public class ServicioRangos
{
    private readonly ContextoAplicacion _contexto;

    public ServicioRangos(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    // ----------------------------------------------------------------
    // Devuelve el TipoRango correspondiente a los puntos dados.
    // Usa OrderByDescending para obtener el rango más alto que aplica
    // cuando los puntos están en el límite exacto entre dos rangos.
    // Si no hay coincidencia en la tabla, devuelve Cobre por defecto.
    // ----------------------------------------------------------------
    public async Task<TipoRango> ObtenerRangoAsync(int referidosPagados)
    {
        var rangos = await _contexto.RangosUsuario
            .Where(r => r.Activo)
            .OrderByDescending(r => r.Orden)
            .ToListAsync();

        if (!rangos.Any())
            return TipoRango.Cobre;

        // PuntosMinimos se reutiliza como mínimo de referidos pagados para ese rango
        var rango = rangos.FirstOrDefault(r => referidosPagados >= r.PuntosMinimos);

        return rango?.TipoRango ?? TipoRango.Cobre;
    }

    // ----------------------------------------------------------------
    // Devuelve todos los rangos activos ordenados de menor a mayor.
    // Útil para mostrar la tabla de rangos en el panel del usuario.
    // ----------------------------------------------------------------
    public async Task<List<Models.RangoUsuario>> ObtenerTodosAsync()
    {
        return await _contexto.RangosUsuario
            .Where(r => r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();
    }
}
