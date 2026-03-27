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
            rango = usuario.TipoRangoActual.ToString()
        });

        // 🔍 traer referidos
        var referidos = await _contexto.Referidos
            .Where(r => r.UsuarioId == usuarioId)
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
                    rango = r.Estado.ToString()
                });
            }
        }
    }
}