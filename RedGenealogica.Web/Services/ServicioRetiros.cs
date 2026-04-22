// ============================================================
// ServicioRetiros.cs
// Ubicación: Services/ServicioRetiros.cs
//
// CAMBIO: notificaciones al aprobar y rechazar retiros.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Services;

public class ServicioRetiros
{
    private readonly ContextoAplicacion _contexto;
    private readonly ServicioNotificaciones _servicioNotificaciones;
    private readonly ServicioCorreos _servicioCorreos;
    public ServicioRetiros(
        ContextoAplicacion contexto,
        ServicioCorreos servicioCorreos,
        ServicioNotificaciones servicioNotificaciones)
    {
        _contexto = contexto;
        _servicioNotificaciones = servicioNotificaciones;
        _servicioCorreos = servicioCorreos;
    }

    public async Task<(bool exito, string mensaje)> SolicitarRetiroAsync(
        int usuarioId, decimal monto, string cbuAlias)
    {
        var configMonto = await _contexto.Configuraciones
            .FirstOrDefaultAsync(c => c.Clave == "retiros_monto_minimo");
        var montoMinimo = decimal.TryParse(configMonto?.Valor, out var m) ? m : 500m;
        if (monto <= 0)
            return (false, "El monto debe ser mayor a cero");
        if (monto < montoMinimo)
            return (false, $"El monto mínimo de retiro es ${montoMinimo:N0}");
        if (!ValidarCbuAlias(cbuAlias))
        return (false, "CBU o alias inválido. El CBU debe tener 22 dígitos numéricos, o el alias entre 6 y 20 caracteres sin espacios.");

        // Si no envía CBU, intentar usar el del perfil
        if (string.IsNullOrWhiteSpace(cbuAlias))
        {
            var usuarioExistente = await _contexto.Users.FindAsync(usuarioId);

            if (usuarioExistente == null)
                return (false, "Usuario no encontrado");

            if (string.IsNullOrWhiteSpace(usuarioExistente.CbuAlias))
                return (false, "Debés ingresar tu CBU o alias de MercadoPago");

            cbuAlias = usuarioExistente.CbuAlias;
        }
        else
        {
            // Si lo envía manualmente, actualizar el perfil
            var usuarioExistente = await _contexto.Users.FindAsync(usuarioId);
            if (usuarioExistente != null)
            {
                usuarioExistente.CbuAlias = cbuAlias;
            }
        }

        var usuario = await _contexto.Users.FindAsync(usuarioId);
        if (usuario == null) return (false, "Usuario no encontrado");
        if (usuario.EstadoUsuario != EstadoUsuario.Activo)
            return (false, "Tu cuenta no está activa");
        if (monto > usuario.SaldoDisponible)
            return (false, $"Saldo insuficiente. Disponible: ${usuario.SaldoDisponible:F2}");

        var tienePendiente = await _contexto.SolicitudesRetiro
            .AnyAsync(s => s.UsuarioId == usuarioId && s.Estado == EstadoRetiro.Pendiente);

        if (tienePendiente)
            return (false, "Ya tenés una solicitud pendiente. Esperá a que el admin la resuelva.");

        usuario.SaldoDisponible      -= monto;
        usuario.SaldoPendienteRetiro += monto;

        _contexto.SolicitudesRetiro.Add(new SolicitudRetiro
        {
            UsuarioId     = usuarioId,
            Monto         = monto,
            CbuAlias      = cbuAlias,
            Estado        = EstadoRetiro.Pendiente,
            FechaSolicitud = DateTime.UtcNow
        });

        await _contexto.SaveChangesAsync();
        return (true, "Solicitud enviada. El admin la revisará a la brevedad.");
    }

    public async Task<(bool exito, string mensaje)> AprobarRetiroAsync(
        int solicitudId, int adminId, string referenciaTransferencia, string? nota = null)
    {
        var solicitud = await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);

        if (solicitud == null) return (false, "Solicitud no encontrada");
        if (solicitud.Estado != EstadoRetiro.Pendiente)
            return (false, "Solo se pueden aprobar solicitudes pendientes");

        solicitud.Estado          = EstadoRetiro.Aprobado;
        solicitud.NotaAdmin       = nota;
        solicitud.AdminResolvidoId = adminId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        // Notificación al usuario
        await _servicioNotificaciones.CrearAsync(
            solicitud.UsuarioId,
            TipoNotificacion.RetiroAprobado,
            "✅ Retiro aprobado",
            $"Tu solicitud de retiro por ${solicitud.Monto:N2} fue aprobada. El dinero está en camino a {solicitud.CbuAlias}.",
            "/Usuario/SolicitarRetiro"
        );

        // Email al usuario
        if (!string.IsNullOrEmpty(solicitud.Usuario?.Email))
            await _servicioCorreos.EnviarRetiroAprobadoAsync(
                solicitud.Usuario.Email,
                $"{solicitud.Usuario.Nombres} {solicitud.Usuario.Apellidos}",
                solicitud.Monto);

        return (true, "Retiro aprobado correctamente");
    }

    public async Task<(bool exito, string mensaje)> CompletarRetiroAsync(
        int solicitudId, string referenciaTransferencia)
    {
        var solicitud = await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);

        if (solicitud == null) return (false, "Solicitud no encontrada");
        if (solicitud.Estado != EstadoRetiro.Aprobado)
            return (false, "Solo se pueden completar solicitudes aprobadas");

        solicitud.Usuario!.SaldoPendienteRetiro -= solicitud.Monto;
        solicitud.Estado                  = EstadoRetiro.Completado;
        solicitud.ReferenciaTransferencia = referenciaTransferencia;
        solicitud.FechaResolucion         = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();
        return (true, "Retiro completado correctamente");
    }

    public async Task<(bool exito, string mensaje)> RechazarRetiroAsync(
        int solicitudId, int adminId, string motivo)
    {
        var solicitud = await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);

        if (solicitud == null) return (false, "Solicitud no encontrada");
        if (solicitud.Estado != EstadoRetiro.Pendiente)
            return (false, "Solo se pueden rechazar solicitudes pendientes");

        solicitud.Usuario!.SaldoDisponible      += solicitud.Monto;
        solicitud.Usuario.SaldoPendienteRetiro  -= solicitud.Monto;
        solicitud.Estado           = EstadoRetiro.Rechazado;
        solicitud.NotaAdmin        = motivo;
        solicitud.AdminResolvidoId = adminId;
        solicitud.FechaResolucion  = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        // Notificación al usuario
        await _servicioNotificaciones.CrearAsync(
            solicitud.UsuarioId,
            TipoNotificacion.RetiroRechazado,
            "❌ Retiro rechazado",
            $"Tu solicitud de retiro por ${solicitud.Monto:N2} fue rechazada. Motivo: {motivo}. El saldo fue devuelto a tu cuenta.",
            "/Usuario/SolicitarRetiro"
        );
        // Email al usuario
        if (!string.IsNullOrEmpty(solicitud.Usuario?.Email))
            await _servicioCorreos.EnviarRetiroRechazadoAsync(
            solicitud.Usuario.Email,
            $"{solicitud.Usuario.Nombres} {solicitud.Usuario.Apellidos}",
            solicitud.Monto,
            motivo);

        return (true, "Solicitud rechazada y saldo devuelto al usuario");
    }

    public async Task<List<SolicitudRetiro>> ObtenerPendientesAsync()
    {
        return await _contexto.SolicitudesRetiro
            .Include(s => s.Usuario)
            .Where(s => s.Estado == EstadoRetiro.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();
    }

    public async Task<List<SolicitudRetiro>> ObtenerHistorialAsync(int usuarioId)
    {
        return await _contexto.SolicitudesRetiro
            .Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();
    }

    private static bool ValidarCbuAlias(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;

        valor = valor.Trim();

        // CBU: exactamente 22 dígitos
        if (valor.All(char.IsDigit))
            return valor.Length == 22;

        // Alias: 6-20 caracteres, solo letras, números, puntos y guiones
        if (valor.Length < 6 || valor.Length > 20) return false;
        return valor.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
    }
}
