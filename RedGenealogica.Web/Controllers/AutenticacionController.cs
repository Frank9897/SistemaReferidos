using Microsoft.AspNetCore.Mvc;
public class AutenticacionController : Controller
{
    public IActionResult Login()
    {
        return View();
    }

    public IActionResult Registro()
    {
        return View();
    }
}