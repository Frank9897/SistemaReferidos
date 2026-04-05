using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Services;
public class ServicioPremios
{
    private readonly ContextoAplicacion _contexto;
    private readonly ServicioNotificaciones _notificaciones;

    private const int REFERIDOS_POR_CICLO = 3;
    private const decimal PREMIO = 100000m;
    private const decimal BONO_PADRE = 10000m;

    public ServicioPremios(ContextoAplicacion contexto, ServicioNotificaciones notificaciones)
    {
        _contexto = contexto;
        _notificaciones = notificaciones;
    }


    public async Task ProcesarPagoReferidoAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null) return;

        var sponsor = referido.Usuario;
        if (sponsor == null) return;

        // Contar referidos pagados directos
        var cantidadPagados = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == sponsor.Id && r.PagoConfirmado);

        int ciclos = cantidadPagados / REFERIDOS_POR_CICLO;

        if (ciclos <= sponsor.CiclosCompletados)
            return;

        int nuevosCiclos = ciclos - sponsor.CiclosCompletados;

        for (int i = 0; i < nuevosCiclos; i++)
        {
            await OtorgarPremioAsync(sponsor);
        }

        sponsor.CiclosCompletados = ciclos;

        await _contexto.SaveChangesAsync();
    }

    private async Task OtorgarPremioAsync(Usuario usuario)
    {
        // 🎁 Premio
        usuario.SaldoDisponible += PREMIO;

        await _notificaciones.CrearAsync(
            usuario.Id,
            TipoNotificacion.Sistema,
            "🎉 Premio obtenido",
            $"Ganaste ${PREMIO:N0} por completar 3 referidos.",
            "/Usuario/Panel"
        );

        // 💰 Bono al padre directo
        if (usuario.IdUsuarioPadre.HasValue)
        {
            var padre = await _contexto.Users.FindAsync(usuario.IdUsuarioPadre.Value);

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