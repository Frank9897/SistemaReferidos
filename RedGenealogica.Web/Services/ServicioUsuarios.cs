// ============================================================
// ServicioUsuarios.cs
// Ubicación: Services/ServicioUsuarios.cs
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.ViewModels;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Services;

public class ServicioUsuarios
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ContextoAplicacion _contexto;
    private readonly ServicioCorreos _servicioCorreos;

    public ServicioUsuarios(UserManager<Usuario> userManager, ContextoAplicacion contexto, ServicioCorreos servicioCorreos)
    {
        _userManager = userManager;
        _contexto = contexto;
        _servicioCorreos = servicioCorreos;
    }

    // ----------------------------------------------------------------
    // [BUG-7 CORREGIDO] Registra un nuevo usuario.
    //
    // Si se provee CodigoReferidoPadre y corresponde a un usuario activo,
    // se vincula como hijo en el árbol genealógico (IdUsuarioPadre).
    // Sin ese vínculo, el árbol no se construye y las comisiones no fluyen.
    //
    // El usuario empieza en estado Pendiente hasta que realice su pago
    // de activación a través de MercadoPago.
    // ----------------------------------------------------------------
    public async Task<(Usuario? usuario, IEnumerable<string> errores)> RegistrarAsync(RegistroUsuarioViewModel modelo)
    {
        // Genera un código único de 8 caracteres para que este usuario
        // pueda referir a otros en el futuro
        var codigoPropio = Guid.NewGuid().ToString("N")[..8];

        // [BUG-7] Busca el padre por código de referido si fue provisto
        int? idPadre = null;
        if (!string.IsNullOrWhiteSpace(modelo.CodigoReferidoPadre))
        {
            var padre = await _contexto.Users
                .FirstOrDefaultAsync(u =>
                    u.CodigoReferido == modelo.CodigoReferidoPadre &&
                    u.EstadoUsuario != EstadoUsuario.Suspendido &&
                    u.EstadoUsuario != EstadoUsuario.Inactivo); // acepta Activo Y Pendiente

            if (padre != null)
                idPadre = padre.Id;
            // Si el código no existe o el padre no está activo, se ignora silenciosamente
            // Considerá mostrar un warning en la vista si preferís informar al usuario
        }

        var usuario = new Usuario
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            Nombres = modelo.Nombres,
            Apellidos = modelo.Apellidos,
            CodigoReferido = codigoPropio,
            EstadoUsuario = EstadoUsuario.Pendiente,  // pendiente hasta pago de activación
            FechaRegistro = DateTime.UtcNow,
            IdUsuarioPadre = idPadre                  // vínculo al árbol genealógico
        };

        var resultado = await _userManager.CreateAsync(usuario, modelo.Password);

        if (!resultado.Succeeded)
            return (null, resultado.Errors.Select(e => e.Description));

        // Crear el Referido en la tabla para que aparezca en el panel del sponsor
        if (idPadre.HasValue && idPadre.Value != usuario.Id)
        {
            var producto = await _contexto.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.FechaCreacion)
                .FirstOrDefaultAsync();

            if (producto != null)
            {
                var referido = new Referido
                {
                    UsuarioId         = idPadre.Value,
                    NombreCompleto    = $"{usuario.Nombres} {usuario.Apellidos}",
                    CorreoElectronico = usuario.Email!,
                    ProductoId        = producto.Id,
                    FechaRegistro     = DateTime.UtcNow,
                    Estado            = EstadoReferido.Pendiente,
                    EsAutoPago        = false,
                };
                _contexto.Referidos.Add(referido);
                await _contexto.SaveChangesAsync();
            }
        }


        await _servicioCorreos.EnviarBienvenidaAsync(usuario.Email!, $"{usuario.Nombres} {usuario.Apellidos}");
        
        return (usuario, Enumerable.Empty<string>());
    }
}
