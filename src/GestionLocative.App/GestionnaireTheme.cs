using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GestionLocative.Core.Parametres;

namespace GestionLocative.App;

/// <summary>
/// Change le thème (clair ou sombre) de toute l'application, sans redémarrer : on remplace le dictionnaire
/// de couleurs (Themes/Clair.xaml ou Themes/Sombre.xaml), lu partout en DynamicResource, et on accorde la
/// barre de titre Windows.
/// </summary>
public static class GestionnaireTheme
{
    private static bool _enregistre;

    /// <summary>Thème affiché actuellement.</summary>
    public static ThemeInterface Actuel { get; private set; } = ThemeInterface.Clair;

    /// <summary>Déclenché après un changement de thème (le calendrier redessine alors ses locations).</summary>
    public static event EventHandler? Change;

    public static void Appliquer(ThemeInterface theme)
    {
        var application = Application.Current;
        if (application is null) return;

        if (!_enregistre)
        {
            // Chaque nouvelle fenêtre reçoit la barre de titre assortie au thème dès son affichage.
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => AccorderBarreDeTitre((Window)s)));
            _enregistre = true;
        }

        var nom = theme == ThemeInterface.Sombre ? "Sombre" : "Clair";
        var couleurs = new ResourceDictionary { Source = new Uri($"pack://application:,,,/GestionLocative;component/Themes/{nom}.xaml") };
        var dictionnaires = application.Resources.MergedDictionaries;
        var ancien = dictionnaires.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) == true);
        if (ancien is null) dictionnaires.Insert(0, couleurs);
        else dictionnaires[dictionnaires.IndexOf(ancien)] = couleurs;

        var aChange = Actuel != theme;
        Actuel = theme;
        foreach (Window fenetre in application.Windows) AccorderBarreDeTitre(fenetre);
        if (aChange) Change?.Invoke(null, EventArgs.Empty);
    }

    // Barre de titre sombre de Windows 10 (20H1+) et 11.
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeAncien = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static void AccorderBarreDeTitre(Window fenetre)
    {
        try
        {
            var hwnd = new WindowInteropHelper(fenetre).Handle;
            if (hwnd == IntPtr.Zero) return;
            var sombre = Actuel == ThemeInterface.Sombre ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref sombre, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeAncien, ref sombre, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Windows trop ancien : barre de titre normale.
        }
    }
}
