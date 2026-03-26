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

    public async Task<Pago> CrearPagoSimuladoAsync(int usuarioId, int productoId, decimal monto)
    {
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
        await _contexto.SaveChangesAsync();

        return pago;
    }
}