// ============================================================
// AdminUsuarioConReferidosViewModel.cs
// Ubicación: ViewModels/AdminUsuarioConReferidosViewModel.cs
//
// ViewModel para la vista Admin/Usuarios con acordeón de referidos.
// Agrupa cada usuario con su lista de referidos para evitar
// múltiples queries en la vista.
// ============================================================

using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.ViewModels;

public class AdminUsuarioConReferidosViewModel
{
    public Usuario Usuario { get; set; } = null!;
    public List<Referido> Referidos { get; set; } = new();
}