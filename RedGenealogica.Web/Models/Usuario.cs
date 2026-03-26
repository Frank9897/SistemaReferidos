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

    public int PuntosAcumulados { get; set; } = 0;

    public TipoRango TipoRangoActual { get; set; } = TipoRango.Cobre;

    public EstadoUsuario EstadoUsuario { get; set; } = EstadoUsuario.Pendiente;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime? FechaActivacion { get; set; }

    public int? IdUsuarioPadre { get; set; }

    public Usuario? UsuarioPadre { get; set; }

    public ICollection<Usuario> ReferidosDirectos { get; set; } = new List<Usuario>();

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();

    public ICollection<MovimientoPuntos> MovimientosPuntos { get; set; } = new List<MovimientoPuntos>();

    public ICollection<Referido> ReferidosRegistrados { get; set; } = new List<Referido>();
}