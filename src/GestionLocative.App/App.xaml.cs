using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using GestionLocative.App.Views;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Parametres;

namespace GestionLocative.App;

public partial class App : Application
{
    /// <summary>Réglages de ce poste (%APPDATA%\GestionLocative\parametres.json).</summary>
    public static MagasinParametres Parametres { get; } = new(MagasinParametres.CheminParDefaut);

    /// <summary>Base de données en cours d'utilisation.</summary>
    public static BaseDeDonnees Base { get; set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Interface en français, quelle que soit la langue de Windows.
        var francais = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = francais;
        CultureInfo.DefaultThreadCurrentUICulture = francais;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(francais.IetfLanguageTag)));

        base.OnStartup(e);

        // Thème clair ou sombre choisi sur ce poste, avant d'ouvrir la moindre fenêtre.
        GestionnaireTheme.Appliquer(Parametres.Charger().Theme);

        // Tant que la fenêtre principale n'est pas affichée, fermer une boîte de dialogue ne doit pas quitter le programme.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (!OuvrirBase())
        {
            Shutdown();
            return;
        }

        MainWindow = new Views.MainWindow();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow.Show();
    }

    /// <summary>Ouvre la base du chemin enregistré ; en cas d'échec, propose d'en choisir un autre.</summary>
    private static bool OuvrirBase()
    {
        var chemin = Parametres.Charger().CheminBase;
        try
        {
            var baseDeDonnees = new BaseDeDonnees(chemin);
            baseDeDonnees.Initialiser();
            Base = baseDeDonnees;
            return true;
        }
        catch (Exception ex)
        {
            var reponse = MessageBox.Show(
                $"Impossible d'ouvrir la base de données :\n{chemin}\n\n{ex.Message}\n\n" +
                "Si elle est sur le réseau, vérifiez que l'ordinateur qui la partage est allumé.\n\n" +
                "Voulez-vous choisir un autre emplacement ?",
                "Gestion locative", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (reponse != MessageBoxResult.Yes) return false;

            var fenetre = new ParametresWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true };
            if (fenetre.ShowDialog() != true || fenetre.Base is null) return false;
            Base = fenetre.Base;
            return true;
        }
    }
}
