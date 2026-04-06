// ============================================================
// Producto.cs
// Ubicación: Models/Producto.cs
//
// CAMPOS NUEVOS:
//   - PorcentajeAbueloComision: % del precio que cobra el abuelo
//     cuando su referido (hijo) completa un ciclo de 3 referidos.
//     Fórmula: BonoPagado = Precio × (PorcentajeAbueloComision / 100)
//              × (1 + BonusRangoAbuelo / 100)
//     Límite duro: 66% máximo para garantizar margen al negocio.
//     Recomendado: 10–20%.
//   - PdfUrl1 / PdfNombre1: primer PDF del producto digital.
//   - PdfUrl2 / PdfNombre2: segundo PDF del producto digital.
//     Las URLs son rutas relativas dentro de wwwroot/pdfs/.
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

    // ── Comisión al abuelo ────────────────────────────────────
    // Porcentaje del precio que se paga al abuelo cuando su hijo
    // completa un ciclo de 3 referidos pagos.
    // El rango del abuelo puede incrementar este porcentaje.
    // Límite duro aplicado en el controller: máximo 66%.
    [Range(0, 66, ErrorMessage = "El porcentaje máximo permitido es 66% para garantizar sostenibilidad del negocio.")]
    public decimal PorcentajeAbueloComision { get; set; } = 10m;

    // ── Stock ─────────────────────────────────────────────────
    // Stock opcional. Null = ilimitado.
    public int? StockDisponible { get; set; }

    [StringLength(250)]
    public string? ImagenUrl { get; set; }

    // ── PDFs del producto digital ─────────────────────────────
    // Rutas relativas desde wwwroot/ (ej: "pdfs/42/modulo1.pdf")
    // El acceso se valida: usuario debe tener pago confirmado.
    [StringLength(350)]
    public string? PdfUrl1 { get; set; }

    [StringLength(120)]
    public string? PdfNombre1 { get; set; }

    [StringLength(350)]
    public string? PdfUrl2 { get; set; }

    [StringLength(120)]
    public string? PdfNombre2 { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<Referido> Referidos { get; set; } = new List<Referido>();
}
