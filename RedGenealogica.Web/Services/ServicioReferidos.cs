// ============================================================
// ServicioReferidos.cs
// Ubicación: Services/ServicioReferidos.cs
//
// CAMBIO:
//   PuedeReferirAsync: un usuario Pendiente SÍ puede registrar
//   referidos. La restricción es que no puede enviar el link de
//   pago hasta activarse. Registrar el referido es libre.
//   Esto permite el flujo: A registra B → B paga → A se activa.
//
//   ConvertirReferidoAUsuarioAsync: ahora es llamado manualmente
//   desde el admin. Ya no se llama desde el webhook automáticamente.
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
    // Un usuario puede REGISTRAR referidos si no está Suspendido.
    // Tanto Pendiente como Activo pueden registrar referidos.
    // La diferencia: Pendiente no puede enviar el link de pago hasta
    // que al menos uno de sus referidos pague y lo active.
    // ----------------------------------------------------------------
    public async Task<bool> PuedeRegistrarReferidoAsync(int usuarioId)
    {
        var usuario = await _contexto.Users.FindAsync(usuarioId);
        return usuario != null &&
               usuario.EstadoUsuario != EstadoUsuario.Suspendido &&
               usuario.EstadoUsuario != EstadoUsuario.Inactivo;
    }

    // ----------------------------------------------------------------
    // Registra un nuevo referido. El referido queda Pendiente hasta pagar.
    // El usuario referidor puede estar Pendiente o Activo.
    // ----------------------------------------------------------------
    public async Task<Referido?> RegistrarReferidoAsync(int usuarioId, RegistrarReferidoViewModel modelo)
    {
        if (!await PuedeRegistrarReferidoAsync(usuarioId))
            return null;

        // Verificar que el producto exista y esté activo
        var producto = await _contexto.Productos
            .FirstOrDefaultAsync(p => p.Id == modelo.ProductoId && p.Activo);

        if (producto == null)
            return null;

        var referido = new Referido
        {
            UsuarioId = usuarioId,
            ProductoId = modelo.ProductoId,
            NombreCompleto = modelo.NombreCompleto,
            CorreoElectronico = modelo.CorreoElectronico,
            Telefono = modelo.Telefono,
            Estado = EstadoReferido.Pendiente
        };

        _contexto.Referidos.Add(referido);
        await _contexto.SaveChangesAsync();

        return referido;
    }

    // ----------------------------------------------------------------
    // Convierte un referido (Pagado) en usuario del sistema.
    // Se llama manualmente desde el panel admin cuando el referido
    // quiere tener sus propios referidos.
    //
    // Al convertirse, el nuevo usuario hereda:
    //   - IdUsuarioPadre → el usuario que lo refirió (para el árbol)
    //   - EstadoUsuario.Activo → puede referir desde el primer día
    //   - TipoRangoActual.Cobre → arranca desde el rango más bajo
    // ----------------------------------------------------------------
    public async Task<(bool exito, string mensaje)> ConvertirReferidoAUsuarioAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null)
            return (false, "Referido no encontrado");

        if (referido.UsuarioConvertidoId != null)
            return (false, "Este referido ya fue convertido a usuario");

        if (!referido.PagoConfirmado)
            return (false, "El referido no tiene pago confirmado aún");

        // Si el email ya existe como usuario, solo linkear
        if (!string.IsNullOrEmpty(referido.CorreoElectronico))
        {
            var usuarioExistente = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Email == referido.CorreoElectronico);

            if (usuarioExistente != null)
            {
                referido.UsuarioConvertidoId = usuarioExistente.Id;
                referido.Estado = EstadoReferido.Convertido;
                referido.FechaActivacion = DateTime.UtcNow;
                await _contexto.SaveChangesAsync();
                return (true, $"Referido vinculado al usuario existente: {usuarioExistente.Email}");
            }
        }

        // Crear nuevo usuario con contraseña aleatoria segura
        var passwordTemporal = $"Tmp_{Guid.NewGuid():N}!A1";

        var nuevoUsuario = new Usuario
        {
            UserName = referido.CorreoElectronico ?? $"usr_{Guid.NewGuid().ToString("N")[..8]}",
            Email = referido.CorreoElectronico,
            Nombres = referido.NombreCompleto,
            Apellidos = string.Empty,
            CodigoReferido = Guid.NewGuid().ToString("N")[..8],
            EstadoUsuario = EstadoUsuario.Activo,   // activo desde el primer día
            FechaRegistro = DateTime.UtcNow,
            FechaActivacion = DateTime.UtcNow,
            IdUsuarioPadre = referido.UsuarioId,    // vínculo al árbol genealógico
            PuntosAcumulados = 0,
            TipoRangoActual = TipoRango.Cobre
        };

        var resultado = await _userManager.CreateAsync(nuevoUsuario, passwordTemporal);

        if (!resultado.Succeeded)
        {
            var errores = string.Join(", ", resultado.Errors.Select(e => e.Description));
            return (false, $"Error al crear usuario: {errores}");
        }

        // Genera token de reset para que el usuario establezca su contraseña
        // TODO: enviar por email → /Autenticacion/EstablecerPassword?token=X&email=Y
        var tokenReset = await _userManager.GeneratePasswordResetTokenAsync(nuevoUsuario);
        _ = tokenReset; // pendiente: implementar envío de email

        referido.UsuarioConvertidoId = nuevoUsuario.Id;
        referido.Estado = EstadoReferido.Convertido;
        referido.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return (true, $"Usuario creado exitosamente: {nuevoUsuario.Email}");
    }
}
