// ============================================================
// MovimientoPuntos.cs
// Ubicación: Models/MovimientoPuntos.cs
//
// RESPONSABILIDAD:
// Guardar el historial de eventos asociados al usuario:
// puntos, premios, bonos y movimientos históricos.
//
// NOTA:
// El nombre de la clase se mantiene por compatibilidad con el
// sistema actual, aunque el negocio ya no use comisiones.
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace RedGenealogica.Web.Models;

public class MovimientoPuntos
{
    public int Id { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    [Required]
    public int CantidadPuntos { get; set; }

    [Required]
    [StringLength(150)]
    public string Motivo { get; set; } = string.Empty;

    public int? ReferidoId { get; set; }

    public Referido? Referido { get; set; }

    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;

    // Monto monetario asociado al movimiento, si aplica.
    public decimal Monto { get; set; }

    // Campo de compatibilidad histórica.
    // En el nuevo modelo ya no define el negocio de premios.
    public int Nivel { get; set; }
}