using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace RedGenealogica.Web.Controllers;
[Authorize]
public class InicioController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}