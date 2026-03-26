using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.Models;

public class Producto
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "999999999.99")]
    public decimal Precio { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();

    public ICollection<Referido> Referidos { get; set; } = new List<Referido>();
}