using Microsoft.AspNetCore.Identity;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.ViewModels;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Services;

public class ServicioUsuarios
{
    private readonly UserManager<Usuario> _userManager;

    public ServicioUsuarios(UserManager<Usuario> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Usuario?> RegistrarAsync(RegistroUsuarioViewModel modelo)
    {
        var codigo = Guid.NewGuid().ToString("N").Substring(0, 8);

        var usuario = new Usuario
        {
            UserName = modelo.Email,
            Email = modelo.Email,
            Nombres = modelo.Nombres,
            Apellidos = modelo.Apellidos,
            CodigoReferido = codigo,
            EstadoUsuario = EstadoUsuario.Pendiente
        };

        var resultado = await _userManager.CreateAsync(usuario, modelo.Password);

        if (!resultado.Succeeded)
            return null;

        return usuario;
    }
}