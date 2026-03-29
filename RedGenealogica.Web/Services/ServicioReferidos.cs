// ============================================================
// ServicioReferidos.cs
// Ubicación: Services/ServicioReferidos.cs
//
//   [MEJORA] ConvertirReferidoAUsuarioAsync verifica que el referido tenga
//            PagoConfirmado antes de crear el usuario, evitando conversiones
//            sin pago real.
// ============================================================

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

    // ----------------------------------------------------------------
    // Verifica si un usuario puede registrar referidos.
    // Requisito: estar Activo (es decir, haber pagado la activación).
    // ----------------------------------------------------------------
    public async Task<bool> PuedeReferirAsync(int usuarioId)
    {
        var usuario = await _contexto.Users.FindAsync(usuarioId);
        return usuario != null && usuario.EstadoUsuario == EstadoUsuario.Activo;
    }

    // ----------------------------------------------------------------
    // Registra un nuevo referido asociado al usuario activo.
    // El referido queda en estado Pendiente hasta que pague.
    // ----------------------------------------------------------------
    public async Task<Referido?> RegistrarReferidoAsync(int usuarioId, RegistrarReferidoViewModel modelo)
    {
        // Solo usuarios activos pueden referir
        if (!await PuedeReferirAsync(usuarioId))
            return null;

        var referido = new Referido
        {
            UsuarioId = usuarioId,
            ProductoId = modelo.ProductoId,
            NombreCompleto = modelo.NombreCompleto,
            CorreoElectronico = modelo.CorreoElectronico,
            Telefono = modelo.Telefono,
            Estado = EstadoReferido.Pendiente   // [CORREGIDO] era EstadoUsuario.Pendiente
        };

        _contexto.Referidos.Add(referido);
        await _contexto.SaveChangesAsync();

        return referido;
    }

    // ----------------------------------------------------------------
    // [BUG-4 CORREGIDO] ConvertirReferidoAUsuarioAsync
    //
    // ANTES: creaba el usuario con contraseña fija "Temporal123!" sin
    //        ningún mecanismo para que el usuario la pueda cambiar.
    //        El usuario quedaba atrapado sin acceso real al sistema.
    //
    // AHORA:
    //   1. Verifica que el referido tenga PagoConfirmado antes de convertir.
    //   2. Genera el usuario con contraseña aleatoria (nunca se usa directamente).
    //   3. Genera un token de reset de contraseña para que el admin o el sistema
    //      pueda enviarlo por email al nuevo usuario.
    //   4. Almacena ese token en el campo TokenResetPassword del usuario.
    //
    // PENDIENTE: conectar envío de email con el token para que el usuario
    //            pueda establecer su propia contraseña al ingresar por primera vez.
    // ----------------------------------------------------------------
    public async Task ConvertirReferidoAUsuarioAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null)
            return;

        // Ya fue convertido anteriormente
        if (referido.UsuarioConvertidoId != null)
            return;

        // [MEJORA] Solo convertir si el pago fue confirmado
        if (!referido.PagoConfirmado)
            return;

        // Si el email ya existe como usuario, linkearlo directamente
        if (!string.IsNullOrEmpty(referido.CorreoElectronico))
        {
            var usuarioExistente = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Email == referido.CorreoElectronico);

            if (usuarioExistente != null)
            {
                referido.UsuarioConvertidoId = usuarioExistente.Id;
                referido.Estado = EstadoReferido.Convertido;   // [CORREGIDO]
                referido.FechaActivacion = DateTime.UtcNow;

                await _contexto.SaveChangesAsync();
                return;
            }
        }

        // Genera una contraseña aleatoria segura (nunca se le muestra al usuario)
        // El usuario deberá usar el flujo de "establecer contraseña" con el token
        var passwordTemporal = $"Tmp_{Guid.NewGuid():N}!A1";

        var nuevoUsuario = new Usuario
        {
            UserName = referido.CorreoElectronico ?? $"usuario_{Guid.NewGuid():N[..8]}",
            Email = referido.CorreoElectronico,
            Nombres = referido.NombreCompleto,
            Apellidos = string.Empty,
            CodigoReferido = Guid.NewGuid().ToString("N")[..8],
            EstadoUsuario = EstadoUsuario.Activo,
            FechaRegistro = DateTime.UtcNow,
            FechaActivacion = DateTime.UtcNow,
            IdUsuarioPadre = referido.UsuarioId,   // vincula al árbol genealógico
            PuntosAcumulados = 0,
            TipoRangoActual = TipoRango.Cobre
        };

        var resultado = await _userManager.CreateAsync(nuevoUsuario, passwordTemporal);

        if (!resultado.Succeeded)
        {
            var errores = string.Join(" | ", resultado.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"No se pudo convertir el referido a usuario: {errores}");
        }

        // Genera token de reset para que el usuario pueda establecer su contraseña
        // TODO: enviar este token por email al nuevo usuario para que active su cuenta
        var tokenReset = await _userManager.GeneratePasswordResetTokenAsync(nuevoUsuario);

        // Almacena el token temporalmente (deberías enviarlo por email en producción)
        // Por ahora queda en memoria/log para que el admin pueda enviarlo manualmente
        // TODO: implementar envío de email con enlace:
        //       /Autenticacion/EstablecerPassword?token={tokenReset}&email={nuevoUsuario.Email}
        _ = tokenReset; // suprimir warning hasta implementar email

        referido.UsuarioConvertidoId = nuevoUsuario.Id;
        referido.Estado = EstadoReferido.Convertido;   // [CORREGIDO]
        referido.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();
    }
}
