using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.ViewModels;

public class PanelUsuarioViewModel
{
    public Usuario Usuario { get; set; } = new Usuario();

    public List<Referido> Referidos { get; set; } = new List<Referido>();
}