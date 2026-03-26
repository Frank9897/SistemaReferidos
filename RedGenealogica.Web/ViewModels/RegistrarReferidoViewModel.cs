using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.ViewModels;

public class RegistrarReferidoViewModel
{
    [Required]
    public string NombreCompleto { get; set; } = string.Empty;

    [EmailAddress]
    public string? CorreoElectronico { get; set; }

    public string? Telefono { get; set; }

    [Required]
    public int ProductoId { get; set; }
}