using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.ViewModels;

namespace RedGenealogica.Web.Services;

public class ServicioReferidos
{
    private readonly ContextoAplicacion _contexto;
    private readonly UserManager<Usuario> _userManager;

    public ServicioReferidos(ContextoAplicacion contexto, UserManager<Usuario> userManager)
    {
        _contexto = contexto;
        _userManager = userManager;
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

        if (referido == null)
            return;

        referido.Estado = EstadoUsuario.Activo;
        referido.FechaActivacion = DateTime.UtcNow;

        referido.Usuario!.PuntosAcumulados += 100;

        var nuevoRango = await ObtenerRangoAsync(referido.Usuario.PuntosAcumulados);
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

    public async Task ConvertirReferidoAUsuarioAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null)
            return;

        if (referido.UsuarioConvertidoId != null)
            return;

        var usuarioExistente = await _contexto.Users
            .FirstOrDefaultAsync(u => u.Email == referido.CorreoElectronico);

        if (usuarioExistente != null)
        {
            referido.UsuarioConvertidoId = usuarioExistente.Id;
            referido.Estado = EstadoUsuario.Activo;
            referido.FechaActivacion = DateTime.UtcNow;

            await _contexto.SaveChangesAsync();
            return;
        }

        var nuevoUsuario = new Usuario
        {
            UserName = referido.CorreoElectronico,
            Email = referido.CorreoElectronico,
            Nombres = referido.NombreCompleto,
            Apellidos = string.Empty,
            CodigoReferido = Guid.NewGuid().ToString("N")[..8],
            EstadoUsuario = EstadoUsuario.Activo,
            FechaRegistro = DateTime.UtcNow,
            FechaActivacion = DateTime.UtcNow,
            IdUsuarioPadre = referido.UsuarioId,
            PuntosAcumulados = 0,
            TipoRangoActual = TipoRango.Cobre
        };

        var resultado = await _userManager.CreateAsync(nuevoUsuario, "Temporal123!");

        if (!resultado.Succeeded)
        {
            var errores = string.Join(" | ", resultado.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"No se pudo convertir el referido a usuario: {errores}");
        }

        referido.UsuarioConvertidoId = nuevoUsuario.Id;
        referido.Estado = EstadoUsuario.Activo;
        referido.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();
    }

    private async Task<TipoRango> ObtenerRangoAsync(int puntos)
    {
        var rango = await _contexto.RangosUsuario
            .Where(r => puntos >= r.PuntosMinimos && puntos <= r.PuntosMaximos)
            .OrderBy(r => r.Orden)
            .FirstOrDefaultAsync();

        return rango?.TipoRango ?? TipoRango.Cobre;
    }
}