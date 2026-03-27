using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Enumeraciones;

namespace RedGenealogica.Web.Services;

public class ServicioPagos
{
    private readonly ContextoAplicacion _contexto;

    public ServicioPagos(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    public async Task<Pago> CrearPagoYActivarUsuarioAsync(int usuarioId, int productoId, decimal monto)
    {
        var usuario = await _contexto.Users.FirstOrDefaultAsync(x => x.Id == usuarioId);

        if (usuario == null)
            throw new Exception("Usuario no encontrado");

        var pago = new Pago
        {
            UsuarioId = usuarioId,
            ProductoId = productoId,
            Monto = monto,
            EstadoPago = EstadoPago.Aprobado,
            EsSimulado = true,
            NombreCuentaEnmascarado = "CUENTA_TEST_****",
            FechaConfirmacion = DateTime.UtcNow
        };

        _contexto.Pagos.Add(pago);

        // 🔥 ACTIVAR USUARIO
        usuario.EstadoUsuario = EstadoUsuario.Activo;
        usuario.FechaActivacion = DateTime.UtcNow;

        await _contexto.SaveChangesAsync();

        return pago;
    }

    private readonly Dictionary<int, decimal> niveles = new()
    {
        { 1, 0.10m }, // 10%
        { 2, 0.05m }, // 5%
        { 3, 0.02m }  // 2%
    };

    public async Task GenerarComisiones(int usuarioId, decimal montoBase)
    {
        int nivelActual = 1;
        int? usuarioActualId = usuarioId;

        while (usuarioActualId != null && niveles.ContainsKey(nivelActual))
        {
            // 🔍 buscar padre
            var usuario = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioActualId);

            if (usuario?.IdUsuarioPadre == null)
                break;

            var padre = await _contexto.Users
                .FirstOrDefaultAsync(u => u.Id == usuario.IdUsuarioPadre);

            if (padre == null)
                break;

            var porcentaje = niveles[nivelActual];
            var comision = montoBase * porcentaje;

            // 💰 guardar comisión
            _contexto.MovimientosPuntos.Add(new MovimientoPuntos
            {
                UsuarioId = padre.Id,
                Monto = comision,
                Motivo = $"Comisión nivel {nivelActual}",
                Nivel = nivelActual,
                FechaMovimiento = DateTime.UtcNow
            });

            // subir nivel
            usuarioActualId = padre.Id;
            nivelActual++;
        }

        await _contexto.SaveChangesAsync();
    }
}