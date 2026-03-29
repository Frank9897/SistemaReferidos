// ============================================================
// RangoUsuario.cs
// Ubicación: Models/RangoUsuario.cs
//
// CAMBIO:
//   - BonusComisionPorcentaje: bonus adicional que se aplica sobre
//     la comisión base del producto según el rango del receptor.
//     Ejemplo: Cobre = 0% bonus, Oro = 40% bonus.
//     Si el producto da 10% de comisión y el receptor es Oro (40% bonus):
//     comisión final = 10% * (1 + 0.40) = 14%
// ============================================================

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

    // Bonus porcentual sobre la comisión base del producto.
    // 0 = sin bonus, 20 = 20% más sobre la comisión base.
    [Range(0, 200)]
    public decimal BonusComisionPorcentaje { get; set; } = 0m;

    [StringLength(30)]
    public string? ColorPrincipal { get; set; }

    [StringLength(80)]
    public string? IconoCss { get; set; }

    public bool Activo { get; set; } = true;
}
