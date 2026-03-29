// ============================================================
// EstadoRetiro.cs
// Ubicación: Enumeraciones/EstadoRetiro.cs
// ============================================================

namespace RedGenealogica.Web.Enumeraciones;

public enum EstadoRetiro
{
    // El usuario solicitó el retiro, esperando revisión del admin
    Pendiente = 0,

    // El admin aprobó — en proceso de transferencia MP
    Aprobado = 1,

    // La transferencia fue confirmada exitosamente
    Completado = 2,

    // El admin rechazó la solicitud — saldo devuelto al usuario
    Rechazado = 3
}
