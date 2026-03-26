using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.ViewModels;

public class RegistroUsuarioViewModel
{
    [Required]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    public string Apellidos { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Compare("Password")]
    public string ConfirmarPassword { get; set; } = string.Empty;

    public string? CodigoReferidoPadre { get; set; }
}