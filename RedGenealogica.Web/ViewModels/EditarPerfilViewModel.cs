using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.ViewModels;
public class EditarPerfilViewModel
{
    [Required]
    [StringLength(100)]
    public string Nombres { get; set; }

    [Required]
    [StringLength(100)]
    public string Apellidos { get; set; }

    [StringLength(100)]
    public string? CbuAlias { get; set; }
}