namespace GreenTransit.Web.Services;

/// <summary>
/// Mantiene el estado de expansión/contracción del menú de navegación
/// durante toda la sesión del circuito Blazor Server.
/// Al ser Scoped, sobrevive a las navegaciones entre páginas.
/// </summary>
public sealed class NavMenuStateService
{
    private readonly HashSet<string> _collapsed =
    [
        "config", "operaciones", "economia", "declaraciones",
        "sostenibilidad", "reporting", "reporting-logistics", "reporting-mobility", "seguridad"
    ];

    public bool IsCollapsed(string key) => _collapsed.Contains(key);

    public void Toggle(string key)
    {
        if (!_collapsed.Add(key))
            _collapsed.Remove(key);
    }
}
