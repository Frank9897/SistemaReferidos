// ============================================================
// ServicioPremios.cs
//
// RESPONSABILIDAD:
// - Gestionar premios por ciclos (3 referidos pagos).
// - Otorgar premio al usuario.
// - Otorgar bono al padre directo.
// - Evitar duplicación de pagos.
// ============================================================

using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Services;

public class ServicioPremios
{
    private readonly ContextoAplicacion _contexto;
    private readonly ServicioNotificaciones _notificaciones;

    private const int REFERIDOS_POR_CICLO = 3;
    private const decimal BONO_PADRE = 10000m;

    public ServicioPremios(ContextoAplicacion contexto, ServicioNotificaciones notificaciones)
    {
        _contexto = contexto;
        _notificaciones = notificaciones;
    }

    // ============================================================
    // Procesa pago de referido y valida si corresponde premio
    // ============================================================
    public async Task ProcesarPagoReferidoAsync(int referidoId)
    {
        using var tx = await _contexto.Database.BeginTransactionAsync();

        try
        {
            var referido = await _contexto.Referidos
                .Include(r => r.Usuario)
                .Include(r => r.Producto)
                .FirstOrDefaultAsync(r => r.Id == referidoId);

            if (referido == null) return;

            var sponsor = referido.Usuario;
            if (sponsor == null) return;

            // 🔢 Contar referidos pagos
            var cantidadPagados = await _contexto.Referidos
                .CountAsync(r => r.UsuarioId == sponsor.Id && r.PagoConfirmado);

            int ciclosCalculados = cantidadPagados / REFERIDOS_POR_CICLO;

            if (ciclosCalculados <= sponsor.CiclosCompletados)
            {
                await tx.CommitAsync();
                return;
            }

            int nuevosCiclos = ciclosCalculados - sponsor.CiclosCompletados;

            for (int i = 0; i < nuevosCiclos; i++)
            {
                await OtorgarPremioAsync(sponsor, referido.Producto!.Precio);
            }

            sponsor.CiclosCompletados = ciclosCalculados;

            await _contexto.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // Otorga premio + bono al padre directo
    // ============================================================
    private async Task OtorgarPremioAsync(Usuario usuario, decimal montoPremio)
    {
        // 🎁 Premio principal
        usuario.SaldoDisponible += montoPremio;

        await _notificaciones.CrearAsync(
            usuario.Id,
            TipoNotificacion.Sistema,
            "🎉 Premio obtenido",
            $"Ganaste ${montoPremio:N0} por completar 3 referidos.",
            "/Usuario/Panel"
        );

        // 💰 Bono al padre directo
        if (usuario.IdUsuarioPadre.HasValue)
        {
            var padre = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuario.IdUsuarioPadre.Value);

            if (padre != null)
            {
                padre.SaldoDisponible += BONO_PADRE;

                await _notificaciones.CrearAsync(
                    padre.Id,
                    TipoNotificacion.Sistema,
                    "💰 Bono recibido",
                    $"Recibiste ${BONO_PADRE:N0} porque tu referido completó un ciclo.",
                    "/Usuario/Panel"
                );
            }
        }
    }
}