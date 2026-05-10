// ============================================================
// UsuarioController.cs
// Ubicación: Controllers/UsuarioController.cs
//
// RESPONSABILIDAD:
// - Mostrar el panel del usuario.
// - Manejar activación de cuenta mediante pago del primer referido.
// - Gestionar retiros.
// - Editar perfil.
//
// NOTA:
// Se eliminó cualquier referencia a comisiones multinivel.
// El panel ahora muestra premios, ciclos y progreso de rango.
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using RedGenealogica.Web.ViewModels;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class UsuarioController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ServicioPagos _servicioPagos;
    private readonly ContextoAplicacion _contexto;

    public UsuarioController(
        UserManager<Usuario> userManager,
        ServicioPagos servicioPagos,
        ContextoAplicacion contexto)
    {
        _userManager = userManager;
        _servicioPagos = servicioPagos;
        _contexto = contexto;
    }

    // ----------------------------------------------------------------
    // GET /Usuario/Panel
    // Muestra el panel principal del usuario con métricas de red,
    // saldo disponible, rango actual y progreso de ciclo.
    // ----------------------------------------------------------------
    public async Task<IActionResult> Panel()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        ViewBag.Activado = Request.Query["activado"] == "1";

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuario.Id && !r.EsAutoPago)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();

        // Referidos "activos" en el nuevo modelo: ya convertidos a usuario.
        var totalReferidosActivos = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == usuario.Id && r.Estado == EstadoReferido.Convertido && !r.EsAutoPago);

        // Cantidad de referidos pagos directos para el ciclo actual.
        var referidosPagadosDirectos = await _contexto.Referidos
            .CountAsync(r => r.UsuarioId == usuario.Id && r.PagoConfirmado && !r.EsAutoPago);

        var referidosActuales = referidosPagadosDirectos % 3;

        var ultimosMovimientos = await _contexto.MovimientosPuntos
            .Where(m => m.UsuarioId == usuario.Id)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(5)
            .ToListAsync();

        var hijosDirectos = await _contexto.Users
            .AsNoTracking()
            .Where(u => u.IdUsuarioPadre == usuario.Id)
            .ToListAsync();

        // Solo cargar Id e IdUsuarioPadre para calcular descendientes,
        // evitando traer todas las columnas de todos los usuarios a memoria.
        var mapaArbol = await _contexto.Users
            .AsNoTracking()
            .Select(u => new Usuario { Id = u.Id, IdUsuarioPadre = u.IdUsuarioPadre })
            .ToListAsync();

        int totalDescendientes = ContarDescendientes(usuario.Id, mapaArbol);

        int referidosIndirectos = totalDescendientes > hijosDirectos.Count
            ? totalDescendientes - hijosDirectos.Count
            : 0;

        var rangoActual = await _contexto.RangosUsuario
            .FirstOrDefaultAsync(r => r.TipoRango == usuario.TipoRangoActual);

        var siguienteRango = await _contexto.RangosUsuario
            .Where(r => r.Orden > (rangoActual != null ? rangoActual.Orden : 0))
            .OrderBy(r => r.Orden)
            .FirstOrDefaultAsync();

        int puntosFaltantes = siguienteRango != null
            ? Math.Max(siguienteRango.PuntosMinimos - usuario.PuntosAcumulados, 0)
            : 0;

        int progreso = 100;
        if (rangoActual != null && siguienteRango != null)
        {
            var baseRango = siguienteRango.PuntosMinimos - rangoActual.PuntosMinimos;
            var avanzados = usuario.PuntosAcumulados - rangoActual.PuntosMinimos;

            if (baseRango > 0)
                progreso = (int)Math.Clamp((avanzados * 100m) / baseRango, 0, 100);
        }

        var modelo = new PanelUsuarioViewModel
        {
            Usuario = usuario,
            Referidos = referidos,
            TotalReferidosDirectos = hijosDirectos.Count,
            TotalReferidosRegistrados = referidos.Count,
            TotalReferidosIndirectos = referidosIndirectos,
            TotalReferidosActivos = totalReferidosActivos,
            SiguienteRango = siguienteRango?.NombreVisible,
            PuntosFaltantesParaSiguienteRango = puntosFaltantes,
            ProgresoRangoPorcentaje = progreso,
            ReferidosActuales = referidosActuales,
            UltimosMovimientos = ultimosMovimientos
        };

        return View(modelo);
    }

    // ----------------------------------------------------------------
    // Recorre el árbol de usuarios para contar descendientes.
    // ----------------------------------------------------------------
    private static int ContarDescendientes(int usuarioId, List<Usuario> usuarios)
    {
        var hijos = usuarios.Where(u => u.IdUsuarioPadre == usuarioId).ToList();

        int total = 0;

        foreach (var hijo in hijos)
        {
            total += 1;
            total += ContarDescendientes(hijo.Id, usuarios);
        }

        return total;
    }

    // ----------------------------------------------------------------
    // POST /Usuario/ActivarCuenta
    // Inicia el pago del primer referido para activar la cuenta.
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> ActivarCuenta()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var referido = await _contexto.Referidos
            .Include(r => r.Producto)
            .Where(r => r.UsuarioId == usuario.Id)
            .OrderBy(r => r.FechaRegistro)
            .FirstOrDefaultAsync();

        if (referido == null)
        {
            TempData["Error"] = "Primero tenés que registrar un referido para activar tu cuenta.";
            return RedirectToAction("Panel");
        }

        if (referido.Estado == EstadoReferido.Convertido || referido.Estado == EstadoReferido.Pagado)
        {
            TempData["Error"] = "Tu cuenta ya está activada.";
            return RedirectToAction("Panel");
        }

        if (referido.Estado != EstadoReferido.Pendiente)
        {
            TempData["Error"] = "Este referido ya fue procesado.";
            return RedirectToAction("Panel");
        }

        if (referido.Producto == null)
        {
            TempData["Error"] = "El referido no tiene producto asignado.";
            return RedirectToAction("Panel");
        }

        var urlPago = await _servicioPagos.CrearPreferencia(referido.Id);
        return Redirect(urlPago);
    }

    // ----------------------------------------------------------------
    // GET /Usuario/SolicitarRetiro
    // Muestra saldo e historial de solicitudes de retiro.
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> SolicitarRetiro()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var historial = await _contexto.SolicitudesRetiro
            .Where(s => s.UsuarioId == usuario.Id)
            .OrderByDescending(s => s.FechaSolicitud)
            .Take(5)
            .ToListAsync();

        ViewBag.Usuario = usuario;
        ViewBag.Historial = historial;
        return View();
    }

    // ----------------------------------------------------------------
    // POST /Usuario/SolicitarRetiro
    // Procesa la solicitud de retiro.
    // ----------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SolicitarRetiro(decimal monto, string cbuAlias)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var servicioRetiros = HttpContext.RequestServices
            .GetRequiredService<ServicioRetiros>();

        var (exito, mensaje) = await servicioRetiros
            .SolicitarRetiroAsync(usuario.Id, monto, cbuAlias);

        if (exito)
            TempData["Exito"] = mensaje;
        else
            TempData["Error"] = mensaje;

        return RedirectToAction("SolicitarRetiro");
    }

    // ----------------------------------------------------------------
    // GET /Usuario/EditarPerfil
    // ----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> EditarPerfil()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        var vm = new EditarPerfilViewModel
        {
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
            CbuAlias = usuario.CbuAlias
        };

        return View(vm);
    }

    // ----------------------------------------------------------------
    // POST /Usuario/EditarPerfil
    // ----------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> EditarPerfil(EditarPerfilViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var usuario = await _userManager.GetUserAsync(User);
        if (usuario == null)
            return RedirectToAction("Login", "Autenticacion");

        usuario.Nombres = model.Nombres;
        usuario.Apellidos = model.Apellidos;
        usuario.CbuAlias = model.CbuAlias;

        await _contexto.SaveChangesAsync();

        TempData["Exito"] = "Perfil actualizado correctamente";

        return RedirectToAction("Panel");
    }
}