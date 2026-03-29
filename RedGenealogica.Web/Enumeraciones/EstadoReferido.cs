// ============================================================
// EstadoReferido.cs
// Ubicación: Enumeraciones/EstadoReferido.cs
// ============================================================

namespace RedGenealogica.Web.Enumeraciones;

public enum EstadoReferido
{
    // El referido fue registrado pero aún no pagó la activación
    Pendiente = 0,

    // El referido completó el pago (webhook confirmado)
    Pagado = 1,

    // El referido fue convertido en usuario del sistema
    Convertido = 2,

    // El referido fue cancelado manualmente por el admin
    Cancelado = 3
}
