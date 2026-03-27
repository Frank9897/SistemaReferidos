using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using Microsoft.AspNetCore.Authorization;
namespace RedGenealogica.Web.Controllers;
[Authorize]
public class RankingController : Controller
{
    private readonly ContextoAplicacion _contexto;

    public RankingController(ContextoAplicacion contexto)
    {
        _contexto = contexto;
    }

    public async Task<IActionResult> Index()
    {
        var ranking = await _contexto.Users
            .OrderByDescending(x => x.PuntosAcumulados)
            .Take(10)
            .ToListAsync();

        return View(ranking);
    }
}