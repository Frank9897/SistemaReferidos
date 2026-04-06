using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.ViewModels;
public class EditarPerfilViewModel
{
    [Required]
    [StringLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CbuAlias { get; set; }
}