// ============================================================
// Producto.cs
// Ubicación: Models/Producto.cs
//
// CAMBIOS:
//   - ComisionPorcentajeN1/N2/N3: cada producto define sus propios
//     porcentajes de comisión por nivel, independiente de la tabla global.
//     Esto permite que un Switch dé 10/5/2% y otro producto dé 15/8/3%.
//   - StockDisponible: opcional, null = sin límite de stock.
//   - ImagenUrl: para mostrar foto del producto en la vista.
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

    // Porcentajes de comisión específicos de este producto por nivel del árbol.
    // Nivel 1 = referidor directo, Nivel 2 = abuelo, Nivel 3 = bisabuelo.
    // Estos valores se multiplican adicionalmente por el bonus de rango del receptor.
    [Range(0, 100)]
    public decimal ComisionNivel1Porcentaje { get; set; } = 10m;

    [Range(0, 100)]
    public decimal ComisionNivel2Porcentaje { get; set; } = 5m;

    [Range(0, 100)]
    public decimal ComisionNivel3Porcentaje { get; set; } = 2m;

    // Stock opcional. Null = ilimitado.
    public int? StockDisponible { get; set; }

    [StringLength(250)]
    public string? ImagenUrl { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Referido> Referidos { get; set; } = new List<Referido>();
}
