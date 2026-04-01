// ============================================================
// Notificacion.cs
// Ubicación: Models/Notificacion.cs
// ============================================================

using System.ComponentModel.DataAnnotations;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Models;

public class Notificacion
{
    public int Id { get; set; }

    // Usuario destinatario de la notificación
    [Required]
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public TipoNotificacion Tipo { get; set; }

    [Required]
    [StringLength(120)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Mensaje { get; set; } = string.Empty;

    // URL opcional — al hacer click en la notificación navega aquí
    [StringLength(200)]
    public string? UrlAccion { get; set; }

    // false = no leída (muestra badge), true = ya vista
    public bool Leida { get; set; } = false;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
