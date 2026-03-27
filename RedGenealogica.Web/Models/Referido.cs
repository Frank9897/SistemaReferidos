using System.ComponentModel.DataAnnotations;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class Referido
{
    public int Id { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    [Required]
    public int ProductoId { get; set; }

    public Producto? Producto { get; set; }

    [Required]
    [StringLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? CorreoElectronico { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    public int? PagoUsuarioId { get; set; }

    public Pago? PagoUsuario { get; set; }

    public int? PagoReferidoId { get; set; }

    public Pago? PagoReferido { get; set; }

    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Pendiente;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime? FechaActivacion { get; set; }

    public int? UsuarioConvertidoId { get; set; }
    public Usuario? UsuarioConvertido { get; set; }
}