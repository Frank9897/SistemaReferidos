using System.ComponentModel.DataAnnotations;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class RangoUsuario
{
    public int Id { get; set; }

    [Required]
    public TipoRango TipoRango { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PuntosMinimos { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int PuntosMaximos { get; set; }

    [Required]
    [Range(1, 100)]
    public int Orden { get; set; }

    [Required]
    [StringLength(50)]
    public string NombreVisible { get; set; } = string.Empty;

    [StringLength(30)]
    public string? ColorPrincipal { get; set; }

    [StringLength(80)]
    public string? IconoCss { get; set; }

    public bool Activo { get; set; } = true;
}