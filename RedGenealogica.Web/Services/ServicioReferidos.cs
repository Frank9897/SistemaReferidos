using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.ViewModels;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Services;
namespace RedGenealogica.Web.Services;

public class ServicioReferidos
{
    private readonly ContextoAplicacion _contexto;

    public ServicioReferidos(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    public async Task<bool> PuedeReferirAsync(int usuarioId)
    {
        var usuario = await _contexto.Users.FindAsync(usuarioId);
        return usuario != null && usuario.EstadoUsuario == EstadoUsuario.Activo;
    }

    public async Task<Referido?> RegistrarReferidoAsync(int usuarioId, RegistrarReferidoViewModel modelo)
    {
        var puede = await PuedeReferirAsync(usuarioId);

        if (!puede)
            return null;

        var referido = new Referido
        {
            UsuarioId = usuarioId,
            ProductoId = modelo.ProductoId,
            NombreCompleto = modelo.NombreCompleto,
            CorreoElectronico = modelo.CorreoElectronico,
            Telefono = modelo.Telefono,
            Estado = EstadoUsuario.Pendiente
        };

        _contexto.Referidos.Add(referido);
        await _contexto.SaveChangesAsync();

        return referido;
    }

    public async Task ActivarReferidoAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null) return;

        referido.Estado = EstadoUsuario.Activo;
        referido.FechaActivacion = DateTime.UtcNow;

        // 🎯 SUMAR PUNTOS
        referido.Usuario!.PuntosAcumulados += 100;

        // 🔥 RECALCULAR RANGO
        var servicioRangos = new ServicioRangos(_contexto);
        var nuevoRango = await servicioRangos.ObtenerRangoAsync(referido.Usuario.PuntosAcumulados);

        referido.Usuario.TipoRangoActual = nuevoRango;

        _contexto.MovimientosPuntos.Add(new MovimientoPuntos
        {
            UsuarioId = referido.UsuarioId,
            CantidadPuntos = 100,
            Motivo = "Referido activado",
            ReferidoId = referido.Id
        });

        await _contexto.SaveChangesAsync();
    }
}