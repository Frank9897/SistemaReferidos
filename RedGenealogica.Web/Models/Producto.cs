// ============================================================
// Producto.cs
// ============================================================

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
    [Range(0.01, 999999999.99)]
    public decimal Precio { get; set; }

    // % del precio que cobra el abuelo cuando su hijo completa ciclo.
    // Límite duro: 66%. Recomendado: 10–20%.
    [Range(0, 66, ErrorMessage = "El porcentaje máximo permitido es 66%.")]
    public decimal PorcentajeAbueloComision { get; set; } = 10m;

    public int? StockDisponible { get; set; }

    [StringLength(250)]
    public string? ImagenUrl { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Documentos digitales del producto (N partes/módulos)
    public ICollection<ProductoPdf> Pdfs { get; set; } = new List<ProductoPdf>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Referido> Referidos { get; set; } = new List<Referido>();
}
