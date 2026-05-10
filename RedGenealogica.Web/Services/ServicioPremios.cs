// ============================================================
// ServicioPremios.cs
//
// RESPONSABILIDAD:
// - Procesar premios cuando un sponsor completa 3 referidos pagos.
// - Premio al sponsor: 100% del precio del producto.
// - Bono al abuelo: PorcentajeAbueloComision del producto,
//   amplificado por el BonusComisionPorcentaje del rango del abuelo.
//
// FÓRMULA BONO ABUELO:
//   bonoBase  = Precio × (PorcentajeAbueloComision / 100)
//   bonoFinal = bonoBase × (1 + BonusRangoAbuelo / 100)
//
// EJEMPLO (precio $100, base 10%, abuelo en Diamante bonus 80%):
//   bonoBase  = $10
//   bonoFinal = $10 × 1.80 = $18
//
// LÍMITES DE SEGURIDAD (aplicados en el controller admin):
//   - PorcentajeAbueloComision máximo: 66%
//   - BonusRangoAbuelo máximo configurado: 80% (Diamante)
//   - Bono máximo posible: 66% × 1.80 = 118.8% del precio
//   - Ingreso por ciclo: 3 × precio = 300%
//   - Premio sponsor: 100%
//   - Bono abuelo máximo: ~119%
//   - Margen mínimo garantizado: ~81% del precio
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

    public ServicioPremios(ContextoAplicacion contexto, ServicioNotificaciones notificaciones)
    {
        _contexto = contexto;
        _notificaciones = notificaciones;
    }

    // ── ProcesarPagoReferidoAsync ───────────────────────────────────────
    // Se ejecuta FUERA de la transacción de ConfirmarPago.
    // Tiene su propio SaveChangesAsync. Si falla, el pago ya está
    // confirmado y el webhook registrado — no se pierde el pago.
    public async Task ProcesarPagoReferidoAsync(int referidoId)
    {
        var referido = await _contexto.Referidos
            .Include(r => r.Usuario)
            .Include(r => r.Producto)
            .FirstOrDefaultAsync(r => r.Id == referidoId);

        if (referido == null) return;

        var sponsor = referido.Usuario;
        if (sponsor == null) return;

        var producto = referido.Producto;
        if (producto == null) return;

        var cantidadPagados = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == sponsor.Id && r.PagoConfirmado);

        int ciclosCalculados = cantidadPagados / REFERIDOS_POR_CICLO;

        if (ciclosCalculados <= sponsor.CiclosCompletados) return;

        int nuevosCiclos = ciclosCalculados - sponsor.CiclosCompletados;

        for (int i = 0; i < nuevosCiclos; i++)
            await OtorgarPremioYBonoAsync(sponsor, producto);

        sponsor.CiclosCompletados = ciclosCalculados;

        await _contexto.SaveChangesAsync();
    }

    private async Task OtorgarPremioYBonoAsync(Usuario sponsor, Producto producto)
    {
        // Premio principal al sponsor: 100% del precio
        sponsor.SaldoDisponible += producto.Precio;

        _contexto.MovimientosPuntos.Add(new MovimientoPuntos
        {
            UsuarioId = sponsor.Id,
            CantidadPuntos = 0,
            Monto = producto.Precio,
            Motivo = $"Premio ciclo completo — {producto.Nombre}",
            Nivel = 0,
            FechaMovimiento = DateTime.UtcNow
        });

        await _notificaciones.CrearAsync(
            sponsor.Id,
            TipoNotificacion.Sistema,
            "🎉 ¡Premio obtenido!",
            $"Ganaste ${producto.Precio:N0} por completar 3 referidos del producto «{producto.Nombre}».",
            "/Usuario/Panel"
        );

        // Bono al abuelo (padre del sponsor)
        if (!sponsor.IdUsuarioPadre.HasValue) return;

        var abuelo = await _contexto.Users
            .FirstOrDefaultAsync(u => u.Id == sponsor.IdUsuarioPadre.Value);

        if (abuelo == null) return;

        // Bono base desde el producto
        decimal porcentajeBase = producto.PorcentajeAbueloComision;
        decimal bonoBase = producto.Precio * (porcentajeBase / 100m);

        // Amplificar con bonus de rango del abuelo
        var rangoAbuelo = await _contexto.RangosUsuario
            .Where(r => r.Activo && r.TipoRango == abuelo.TipoRangoActual)
            .FirstOrDefaultAsync();

        decimal bonusRango = rangoAbuelo?.BonusComisionPorcentaje ?? 0m;
        decimal bonoFinal = Math.Round(bonoBase * (1m + bonusRango / 100m), 2);

        abuelo.SaldoDisponible += bonoFinal;

        _contexto.MovimientosPuntos.Add(new MovimientoPuntos
        {
            UsuarioId = abuelo.Id,
            CantidadPuntos = 0,
            Monto = bonoFinal,
            Motivo = $"Bono abuelo — ciclo de {sponsor.Nombres} {sponsor.Apellidos} ({producto.Nombre})",
            Nivel = 1,
            FechaMovimiento = DateTime.UtcNow
        });

        string detalleBono = bonusRango > 0
            ? $"base {porcentajeBase}% + bonus rango {bonusRango}%"
            : $"{porcentajeBase}% del precio";

        await _notificaciones.CrearAsync(
            abuelo.Id,
            TipoNotificacion.Sistema,
            "💰 Bono recibido",
            $"Recibiste ${bonoFinal:N0} porque tu referido {sponsor.Nombres} completó un ciclo. ({detalleBono})",
            "/Usuario/Panel"
        );
    }
}
