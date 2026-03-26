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
}