using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.Models;

public class Configuracion
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Valor { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
}