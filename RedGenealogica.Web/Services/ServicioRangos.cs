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

    public async Task<TipoRango> ObtenerRangoAsync(int puntos)
    {
        var rango = await _contexto.RangosUsuario
            .Where(r => puntos >= r.PuntosMinimos && puntos <= r.PuntosMaximos)
            .OrderByDescending(r => r.Orden)
            .FirstOrDefaultAsync();

        return rango?.TipoRango ?? TipoRango.Cobre;
    }
}