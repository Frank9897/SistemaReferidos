// ============================================================
// Usuario.cs
// Ubicación: Models/Usuario.cs
//
// RESPONSABILIDAD:
// Representa al usuario autenticado del sistema, extendiendo
// IdentityUser<int> con datos del negocio: árbol de referidos,
// puntos, rangos, saldo y ciclos de premios.
//
// NOTAS:
// - SaldoDisponible: dinero real disponible para retiro.
// - SaldoPendienteRetiro: dinero bloqueado mientras un retiro
//   está en proceso.
// - CiclosCompletados: cantidad de ciclos de 3 referidos pagos
//   que ya cobraron premio.
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

    // Puntos de ranking. No son dinero.
    public int PuntosAcumulados { get; set; } = 0;

    public TipoRango TipoRangoActual { get; set; } = TipoRango.Cobre;

    public EstadoUsuario EstadoUsuario { get; set; } = EstadoUsuario.Pendiente;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime? FechaActivacion { get; set; }

    // Dinero real disponible para retirar.
    public decimal SaldoDisponible { get; set; } = 0m;

    // Dinero bloqueado mientras hay un retiro en proceso.
    public decimal SaldoPendienteRetiro { get; set; } = 0m;

    [StringLength(100)]
    public string? CbuAlias { get; set; }

    public int? IdUsuarioPadre { get; set; }
    public Usuario? UsuarioPadre { get; set; }

    public ICollection<Usuario> ReferidosDirectos { get; set; } = new List<Usuario>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<MovimientoPuntos> MovimientosPuntos { get; set; } = new List<MovimientoPuntos>();
    public ICollection<Referido> ReferidosRegistrados { get; set; } = new List<Referido>();
    public ICollection<SolicitudRetiro> SolicitudesRetiro { get; set; } = new List<SolicitudRetiro>();

    // Cantidad de ciclos ya completados y cobrados.
    public int CiclosCompletados { get; set; } = 0;

    // true cuando la cuenta fue creada automáticamente por el sistema
    // y el usuario aún no cambió su contraseña temporal.
    public bool DebeambiarPassword { get; set; } = false;
}