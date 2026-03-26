using Microsoft.AspNetCore.Mvc;

public class InicioController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}