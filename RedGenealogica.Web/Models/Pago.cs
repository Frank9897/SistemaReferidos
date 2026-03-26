using System.ComponentModel.DataAnnotations;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class Pago
{
    public int Id { get; set; }

    [Required]
    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    [Required]
    public int ProductoId { get; set; }

    public Producto? Producto { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "999999999.99")]
    public decimal Monto { get; set; }

    public EstadoPago EstadoPago { get; set; } = EstadoPago.Pendiente;

    [StringLength(100)]
    public string PlataformaPago { get; set; } = "MercadoPago";

    [StringLength(150)]
    public string? ReferenciaExterna { get; set; }

    [StringLength(150)]
    public string? NombreCuentaEnmascarado { get; set; }

    public bool EsSimulado { get; set; } = true;

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public DateTime? FechaConfirmacion { get; set; }
}