using System.Windows;
using System.Windows.Controls;

namespace GestionLocative.App.Views;

/// <summary>
/// Boîte de message assortie au thème (clair ou sombre), utilisée à la place de <see cref="MessageBox"/>
/// dont l'apparence est imposée par Windows.
/// </summary>
public partial class Dialogue : Window
{
    private MessageBoxResult _resultat;

    private Dialogue() => InitializeComponent();

    /// <summary>Affiche le message et attend la réponse, comme <see cref="MessageBox.Show(string)"/>.</summary>
    /// <param name="proprietaire">Fenêtre au-dessus de laquelle s'ouvrir ; null : fenêtre principale, sinon centre de l'écran.</param>
    /// <param name="parDefaut">Bouton validé par Entrée (pour une suppression, « Non »).</param>
    public static MessageBoxResult Afficher(
        Window? proprietaire,
        string message,
        string titre,
        MessageBoxButton boutons = MessageBoxButton.OK,
        MessageBoxImage icone = MessageBoxImage.Information,
        MessageBoxResult parDefaut = MessageBoxResult.None)
    {
        var dialogue = new Dialogue { Title = titre };
        dialogue.Titre.Text = titre;
        dialogue.Message.Text = message;

        proprietaire ??= Application.Current?.MainWindow;
        if (proprietaire is { IsLoaded: true } && !ReferenceEquals(proprietaire, dialogue))
            dialogue.Owner = proprietaire;
        else
        {
            dialogue.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialogue.ShowInTaskbar = true;
        }

        (dialogue.Icone.Text, var couleur) = icone switch
        {
            MessageBoxImage.Warning => ("", "Accent"),
            MessageBoxImage.Error => ("", "Erreur"),
            MessageBoxImage.Question => ("", "Accent2"),
            _ => ("", "AccentDegrade"),
        };
        dialogue.Pastille.SetResourceReference(Border.BackgroundProperty, couleur);

        // Ordre Windows : bouton d'action à gauche, « Non » / « Annuler » à droite.
        var choix = boutons switch
        {
            MessageBoxButton.YesNo => new[] { ("Oui", MessageBoxResult.Yes), ("Non", MessageBoxResult.No) },
            MessageBoxButton.YesNoCancel => [("Oui", MessageBoxResult.Yes), ("Non", MessageBoxResult.No), ("Annuler", MessageBoxResult.Cancel)],
            MessageBoxButton.OKCancel => [("OK", MessageBoxResult.OK), ("Annuler", MessageBoxResult.Cancel)],
            _ => [("OK", MessageBoxResult.OK)],
        };
        var annulation = choix[^1].Item2;
        dialogue._resultat = annulation; // fermeture par la croix ou Échap
        if (parDefaut == MessageBoxResult.None || choix.All(c => c.Item2 != parDefaut)) parDefaut = choix[0].Item2;

        Button? boutonParDefaut = null;
        for (var i = 0; i < choix.Length; i++)
        {
            var (libelle, resultat) = choix[i];
            var bouton = new Button
            {
                Content = libelle,
                Style = (Style)dialogue.FindResource(i == 0 ? "BoutonPrincipal" : "Bouton"),
                IsDefault = resultat == parDefaut,
                IsCancel = resultat == annulation,
            };
            bouton.Click += (_, _) =>
            {
                dialogue._resultat = resultat;
                dialogue.Close();
            };
            if (bouton.IsDefault) boutonParDefaut = bouton;
            dialogue.Boutons.Children.Add(bouton);
        }
        dialogue.Loaded += (_, _) => boutonParDefaut?.Focus();

        dialogue.ShowDialog();
        return dialogue._resultat;
    }
}
