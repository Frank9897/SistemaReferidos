using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.ViewModels;

public class PanelUsuarioViewModel
{
    public Usuario Usuario { get; set; } = new();
    public List<Referido> Referidos { get; set; } = new();

    public int TotalReferidosDirectos { get; set; }
    public int TotalReferidosIndirectos { get; set; }
    public int TotalReferidosActivos { get; set; }
    public int TotalReferidosRegistrados { get; set; }
    public string? SiguienteRango { get; set; }
    public int PuntosFaltantesParaSiguienteRango { get; set; }
    public int ProgresoRangoPorcentaje { get; set; }
    public int ReferidosActuales { get; set; }
    public int ProgresoCiclo => (ReferidosActuales * 100) / 3;
    public List<MovimientoPuntos> UltimosMovimientos { get; set; } = new();
}