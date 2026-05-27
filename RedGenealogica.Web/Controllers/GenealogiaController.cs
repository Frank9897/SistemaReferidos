using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using System.Security.Claims;

namespace RedGenealogica.Web.Controllers;

[Authorize]
public class GenealogiaController : Controller
{
    private readonly ContextoAplicacion _contexto;

    public GenealogiaController(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerArbol()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var nodos = new List<object>();

        // 🧠 AQUÍ creas el control de visitados
        var visitados = new HashSet<int>();

        await ConstruirArbol(usuarioId, null, nodos, visitados);

        return Json(nodos);
    }

    private async Task ConstruirArbol(int usuarioId, string? padreId, List<object> nodos, HashSet<int> visitados)
    {
        // 🛑 EVITA REPETIDOS
        if (visitados.Contains(usuarioId))
            return;

        visitados.Add(usuarioId);

        var usuario = await _contexto.Users
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario == null)
            return;

        string idActual = "U_" + usuario.Id;

        // 🟢 agregar nodo
        nodos.Add(new
        {
            id = idActual,
            nombre = usuario.Nombres,
            padreId = padreId,
            tipo = "usuario",
            rango = usuario.TipoRangoActual.ToString(),
            estado = usuario.EstadoUsuario.ToString(), // 🔥 FIX
            comision = usuario.PuntosAcumulados        // 🔥 EXTRA
        });

        // 🔍 traer referidos
        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuarioId && !r.EsAutoPago)
            .ToListAsync();

        foreach (var r in referidos)
        {
            if (r.UsuarioConvertidoId != null)
            {
                // 🔁 RECURSIVO
                await ConstruirArbol(r.UsuarioConvertidoId.Value, idActual, nodos, visitados);
            }
            else
            {
                nodos.Add(new
                {
                    id = "R_" + r.Id,
                    nombre = r.NombreCompleto,
                    padreId = idActual,
                    tipo = "referido",
                    rango = "Referido",
                    estado = r.Estado.ToString(),   // 🔥 FIX REAL
                    comision = 0                    // opcional
                });
            }
        }
    }

    // ── Árbol global para el admin ────────────────────────────────
    // Devuelve TODOS los usuarios y referidos del sistema.
    // Los usuarios sin padre aparecen como raíces independientes.
    // Para conectarlos visualmente se usa un nodo raíz virtual "SISTEMA".
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ObtenerArbolAdmin()
    {
        var todos = await _contexto.Users
            .OrderBy(u => u.FechaRegistro)
            .ToListAsync();

        var nodos = new List<object>();

        // Nodo raíz virtual que agrupa a todos
        nodos.Add(new
        {
            id       = "SISTEMA",
            nombre   = "Sistema",
            padreId  = (string?)null,
            tipo     = "sistema",
            rango    = "Sistema",
            estado   = "Activo",
            comision = 0,
            email    = "",
            ciclos   = 0,
            saldo    = 0m
        });

        var visitados = new HashSet<int>();
        foreach (var u in todos)
            await ConstruirNodoAdmin(u.Id, todos, nodos, visitados);

        return Json(nodos);
    }

    private async Task ConstruirNodoAdmin(
        int usuarioId,
        List<Usuario> todos,
        List<object> nodos,
        HashSet<int> visitados)
    {
        if (visitados.Contains(usuarioId)) return;
        visitados.Add(usuarioId);

        var usuario = todos.FirstOrDefault(u => u.Id == usuarioId);
        if (usuario == null) return;

        // El padre en el árbol: su IdUsuarioPadre, o "SISTEMA" si es raíz
        string padreId = usuario.IdUsuarioPadre.HasValue
            ? "U_" + usuario.IdUsuarioPadre.Value
            : "SISTEMA";

        nodos.Add(new
        {
            id       = "U_" + usuario.Id,
            nombre   = usuario.Nombres + " " + usuario.Apellidos,
            padreId  = padreId,
            tipo     = "usuario",
            rango    = usuario.TipoRangoActual.ToString(),
            estado   = usuario.EstadoUsuario.ToString(),
            comision = usuario.PuntosAcumulados,
            email    = usuario.Email ?? "",
            ciclos   = usuario.CiclosCompletados,
            saldo    = usuario.SaldoDisponible
        });

        // Referidos no convertidos y sin cuenta creada con ese email
        var emailsConCuenta = todos.Select(u => u.Email?.ToLower()).ToHashSet();

        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuarioId
                    && r.UsuarioConvertidoId == null
                    && !r.EsAutoPago
                    && (r.CorreoElectronico == null || !emailsConCuenta.Contains(r.CorreoElectronico.ToLower())))
            .ToListAsync();

        foreach (var r in referidos)
        {
            nodos.Add(new
            {
                id       = "R_" + r.Id,
                nombre   = r.NombreCompleto,
                padreId  = "U_" + usuarioId,
                tipo     = "referido",
                rango    = "Referido",
                estado   = r.Estado.ToString(),
                comision = 0,
                email    = r.CorreoElectronico ?? "",
                ciclos   = 0,
                saldo    = 0m
            });
        }
    }

}
