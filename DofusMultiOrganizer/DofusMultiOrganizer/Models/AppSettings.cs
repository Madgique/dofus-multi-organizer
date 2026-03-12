namespace DofusOrganizer.Models;

/// <summary>
/// Paramètres persistés de l'application.
/// </summary>
public class AppSettings
{
    /// <summary>Raccourci clavier pour passer à la fenêtre suivante.</summary>
    public string NextWindowHotkey { get; set; } = "";

    /// <summary>Raccourci clavier pour passer à la fenêtre précédente.</summary>
    public string PrevWindowHotkey { get; set; } = "";

    /// <summary>Index de la langue sélectionnée (0=FR, 1=EN, 2=PT-BR, 3=ES).</summary>
    public int LanguageIndex { get; set; } = 0;

    /// <summary>Ordre des fenêtres Dofus par nom de personnage.</summary>
    public List<string> WindowOrder { get; set; } = [];

    /// <summary>Hotkeys directs par nom de personnage.</summary>
    public Dictionary<string, string> WindowHotkeys { get; set; } = [];

    /// <summary>Si true, les fenêtres minimisées sont ignorées lors du focus (pas de restauration).</summary>
    public bool IgnoreMinimizedWindows { get; set; } = true;

    /// <summary>Thème de l'interface : 0=Système, 1=Clair, 2=Sombre.</summary>
    public int AppTheme { get; set; } = 0;

    /// <summary>Dimensions de la fenêtre settings (sauvegardées à la fermeture).</summary>
    public int WindowWidth { get; set; } = 540;
    public int WindowHeight { get; set; } = 620;

    /// <summary>Mode de détection : Unity ou Rétro.</summary>
    public DofusMode DofusMode { get; set; } = DofusMode.Unity;

    /// <summary>
    /// Si true (mode Rétro uniquement), met automatiquement au premier plan la fenêtre
    /// du personnage dont c'est le tour, à la réception de la notification Windows.
    /// </summary>
    public bool AutoFocusOnTurn { get; set; } = false;
}
