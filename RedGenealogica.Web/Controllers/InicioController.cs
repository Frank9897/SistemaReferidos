using Microsoft.AspNetCore.Mvc;

namespace RedGenealogica.Web.Controllers;

public class InicioController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}