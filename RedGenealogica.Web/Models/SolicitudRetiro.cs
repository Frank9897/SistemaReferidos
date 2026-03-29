// ============================================================
// SolicitudRetiro.cs
// Ubicación: Models/SolicitudRetiro.cs
//
// NUEVO MODELO
//
// Representa una solicitud de retiro de comisiones iniciada
// por el usuario. El flujo es:
//
//   Usuario solicita → estado Pendiente
//   Admin aprueba   → estado Aprobado → saldo se descuenta → MP Pay
//   Admin rechaza   → estado Rechazado → saldo se devuelve
//
// El saldo se bloquea en Usuario.SaldoPendienteRetiro apenas se
// crea la solicitud, para evitar doble retiro del mismo dinero.
// ============================================================

using System.ComponentModel.DataAnnotations;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class SolicitudRetiro
{
    public int Id { get; set; }

    [Required]
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Monto solicitado para retirar
    [Required]
    [Range(typeof(decimal), "0.01", "999999999.99")]
    public decimal Monto { get; set; }

    public EstadoRetiro Estado { get; set; } = EstadoRetiro.Pendiente;

    // CBU o alias al momento de la solicitud (snapshot, no depende del perfil)
    [Required]
    [StringLength(100)]
    public string CbuAlias { get; set; } = string.Empty;

    // Referencia de la transferencia una vez procesada (ej: ID de MP)
    [StringLength(150)]
    public string? ReferenciaTransferencia { get; set; }

    // Nota del admin al aprobar o rechazar
    [StringLength(300)]
    public string? NotaAdmin { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; set; }

    // ID del admin que resolvió la solicitud
    public int? AdminResolvidoId { get; set; }
}
