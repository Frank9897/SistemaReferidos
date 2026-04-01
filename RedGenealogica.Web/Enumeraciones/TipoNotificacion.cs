// ============================================================
// TipoNotificacion.cs
// Ubicación: Enumeraciones/TipoNotificacion.cs
// ============================================================

namespace RedGenealogica.Web.Enumeraciones;

public enum TipoNotificacion
{
    // Un referido registrado por este usuario completó el pago
    ReferidoPago = 1,

    // Este usuario recibió una comisión por un pago en su árbol
    ComisionRecibida = 2,

    // Este usuario subió de rango
    SubidaDeRango = 3,

    // El admin aprobó una solicitud de retiro
    RetiroAprobado = 4,

    // El admin rechazó una solicitud de retiro
    RetiroRechazado = 5,

    // Un referido fue convertido a usuario del sistema
    ReferidoConvertido = 6,

    // Mensaje general del sistema o del admin
    Sistema = 7
}
