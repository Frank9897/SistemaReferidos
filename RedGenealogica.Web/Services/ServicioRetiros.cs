// ============================================================
// ServicioRetiros.cs
// Ubicación: Services/ServicioRetiros.cs
//
// NUEVO SERVICIO
//
// Gestiona el ciclo completo de retiro de comisiones:
//   SolicitarRetiro → AprobarRetiro → CompletarRetiro / RechazarRetiro
//
// El saldo se bloquea en SaldoPendienteRetiro al solicitar,
// se descuenta de SaldoDisponible al completar,
// y se devuelve si es rechazado.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Services;

public class ServicioRetiros
{
    private readonly ContextoAplicacion _contexto;

    public ServicioRetiros(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    // ----------------------------------------------------------------
    // El usuario solicita un retiro desde su panel.
    // Bloquea el monto en SaldoPendienteRetiro para evitar doble retiro.
    // ----------------------------------------------------------------
    public async Task<(bool exito, string mensaje)> SolicitarRetiroAsync(
        int usuarioId, decimal monto, string cbuAlias)
    {
        if (monto <= 0)
            return (false, "El monto debe ser mayor a cero");

        if (string.IsNullOrWhiteSpace(cbuAlias))
            return (false, "Debés ingresar tu CBU o alias de MercadoPago");

        var usuario = await _contexto.Users.FindAsync(usuarioId);

        if (usuario == null)
            return (false, "Usuario no encontrado");

        if (usuario.EstadoUsuario != EstadoUsuario.Activo)
            return (false, "Tu cuenta no está activa");

        // Verificar que el saldo disponible alcance (sin contar el bloqueado)
        if (monto > usuario.SaldoDisponible)
            return (false, $"Saldo insuficiente. Disponible: ${usuario.SaldoDisponible:F2}");

        // Bloquear el monto: sale de Disponible y entra en PendienteRetiro
        usuario.SaldoDisponible -= monto;
        usuario.SaldoPendienteRetiro += monto;

        var solicitud = new SolicitudRetiro
        {
            UsuarioId = usuarioId,
            Monto = monto,
            CbuAlias = cbuAlias,
            Estado = EstadoRetiro.Pendiente,
            FechaSolicitud = DateTime.UtcNow
        };

        _contexto.SolicitudesRetiro.Add(solicitud);
        await _contexto.SaveChangesAsync();

        return (true, "Solicitud enviada. El admin la revisará a la brevedad.");
    }

    // ----------------------------------------------------------------
    // El admin aprueba la solicitud y registra la referencia de transferencia.
    // El saldo pasa de PendienteRetiro a Completado (se descuenta definitivamente).
    // ----------------------------------------------------------------
    public async Task<(bool exito, string mensaje)> AprobarRetiroAsync(
        int solicitudId, int adminId, string referenciaTransferencia, string? nota = null)
    {
        var solicitud = await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);

        if (solicitud == null)
            return (false, "Solicitud no encontrada");

        if (solicitud.Estado != EstadoRetiro.Pendiente)
            return (false, "Solo se pueden aprobar solicitudes pendientes");

        // Desbloquear el monto pendiente (ya fue descontado del disponible al solicitar)
        solicitud.Usuario!.SaldoPendienteRetiro -= solicitud.Monto;

        solicitud.Estado = EstadoRetiro.Completado;
        solicitud.ReferenciaTransferencia = referenciaTransferencia;
        solicitud.NotaAdmin = nota;
        solicitud.AdminResolvidoId = adminId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return (true, "Retiro completado y registrado");
    }

    // ----------------------------------------------------------------
    // El admin rechaza la solicitud. El saldo vuelve a SaldoDisponible.
    // ----------------------------------------------------------------
    public async Task<(bool exito, string mensaje)> RechazarRetiroAsync(
        int solicitudId, int adminId, string motivo)
    {
        var solicitud = await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);

        if (solicitud == null)
            return (false, "Solicitud no encontrada");

        if (solicitud.Estado != EstadoRetiro.Pendiente)
            return (false, "Solo se pueden rechazar solicitudes pendientes");

        // Devolver el saldo bloqueado al disponible
        solicitud.Usuario!.SaldoDisponible += solicitud.Monto;
        solicitud.Usuario.SaldoPendienteRetiro -= solicitud.Monto;

        solicitud.Estado = EstadoRetiro.Rechazado;
        solicitud.NotaAdmin = motivo;
        solicitud.AdminResolvidoId = adminId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return (true, "Solicitud rechazada y saldo devuelto al usuario");
    }

    // ----------------------------------------------------------------
    // Lista todas las solicitudes pendientes. Usado en el panel admin.
    // ----------------------------------------------------------------
    public async Task<List<SolicitudRetiro>> ObtenerPendientesAsync()
    {
        return await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .Where(s => s.Estado == EstadoRetiro.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();
    }

    // ----------------------------------------------------------------
    // Lista el historial de retiros de un usuario específico.
    // ----------------------------------------------------------------
    public async Task<List<SolicitudRetiro>> ObtenerHistorialAsync(int usuarioId)
    {
        return await _contexto.SolicitudesRetiro
            .Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();
    }
}
