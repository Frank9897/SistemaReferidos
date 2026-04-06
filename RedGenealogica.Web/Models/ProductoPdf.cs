// ============================================================
// ProductoPdf.cs
// Representa un documento PDF asociado a un producto digital.
// Un producto puede tener N documentos (partes, módulos, etc).
// El Orden define cómo se muestran al usuario (1, 2, 3...).
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.Models;

public class ProductoPdf
{
    public int Id { get; set; }

    [Required]
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    // Nombre visible para el usuario: "Parte 1 — Jabones", etc.
    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    // Ruta relativa desde wwwroot/: "pdfs/3/parte1.pdf"
    [Required]
    [StringLength(350)]
    public string Url { get; set; } = string.Empty;

    // Orden de presentación (1, 2, 3...)
    public int Orden { get; set; } = 1;

    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}
