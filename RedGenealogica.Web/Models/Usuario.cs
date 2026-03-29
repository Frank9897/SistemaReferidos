// ============================================================
// Usuario.cs
// Ubicación: Models/Usuario.cs
//
// CAMBIOS:
//   - SaldoDisponible: acumula el dinero de comisiones retirable.
//     Se suma con cada comisión recibida y se descuenta al retirar.
//   - SaldoPendienteRetiro: monto en proceso de retiro (aprobado
//     por el admin pero aún no transferido). Evita que el usuario
//     solicite dos retiros del mismo saldo.
// ============================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class Usuario : IdentityUser<int>
{
    [Required]
    [StringLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string CodigoReferido { get; set; } = string.Empty;

    [StringLength(50)]
    public string? DocumentoIdentidad { get; set; }

    [StringLength(250)]
    public string? FotoPerfilUrl { get; set; }

    // Puntos de ranking (no son dinero, no se retiran)
    public int PuntosAcumulados { get; set; } = 0;

    public TipoRango TipoRangoActual { get; set; } = TipoRango.Cobre;

    public EstadoUsuario EstadoUsuario { get; set; } = EstadoUsuario.Pendiente;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime? FechaActivacion { get; set; }

    // Saldo en dinero real disponible para retirar (suma de comisiones recibidas)
    public decimal SaldoDisponible { get; set; } = 0m;

    // Saldo bloqueado mientras hay un retiro en proceso
    public decimal SaldoPendienteRetiro { get; set; } = 0m;

    // CBU o alias de MercadoPago para recibir transferencias
    [StringLength(100)]
    public string? CbuAlias { get; set; }

    public int? IdUsuarioPadre { get; set; }
    public Usuario? UsuarioPadre { get; set; }

    public ICollection<Usuario> ReferidosDirectos { get; set; } = new List<Usuario>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<MovimientoPuntos> MovimientosPuntos { get; set; } = new List<MovimientoPuntos>();
    public ICollection<Referido> ReferidosRegistrados { get; set; } = new List<Referido>();
    public ICollection<SolicitudRetiro> SolicitudesRetiro { get; set; } = new List<SolicitudRetiro>();
}
