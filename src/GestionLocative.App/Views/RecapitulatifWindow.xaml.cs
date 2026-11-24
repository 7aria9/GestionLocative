using System.Windows;
using System.Windows.Input;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Recapitulatif;

namespace GestionLocative.App.Views;

/// <summary>Toutes les locations d'une année, avec les totaux HT et TTC ; accès à la liste des présences d'un client.</summary>
public partial class RecapitulatifWindow : Window
{
    private readonly BaseDeDonnees _base;
    private readonly RecapitulatifViewModel _vm;

    public RecapitulatifWindow(BaseDeDonnees baseDeDonnees, int annee)
    {
        InitializeComponent();
        _base = baseDeDonnees;
        _vm = new RecapitulatifViewModel(baseDeDonnees, annee);
        DataContext = _vm;
    }

    /// <summary>Vrai si une location a été modifiée ou supprimée depuis cette fenêtre (le calendrier est alors à redessiner).</summary>
    public bool ModificationsFaites { get; private set; }

    private void Imprimer_Click(object sender, RoutedEventArgs e) => Impression.Imprimer(this, _vm.Impression());

    private void Presences_Click(object sender, RoutedEventArgs e) =>
        new PresencesWindow(_base, _vm.Annee) { Owner = this }.ShowDialog();

    /// <summary>Double-clic sur une ligne : modification (ou suppression) de la location.</summary>
    private void Ligne_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: LigneRecapitulatif ligne }) return;
        e.Handled = true;

        Location? location;
        try
        {
            location = new LocationRepository(_base).ObtenirParId(ligne.IdLocation);
        }
        catch (Exception ex)
        {
            Dialogue.Afficher(this, $"Impossible de lire la location :\n{ex.Message}", "Récapitulatif",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (location is null)
        {
            Dialogue.Afficher(this, "Cette location n'existe plus (supprimée depuis un autre poste ?).", "Récapitulatif");
            _vm.Actualiser();
            return;
        }

        if (new LocationWindow(_base, location) { Owner = this }.ShowDialog() == true)
        {
            ModificationsFaites = true;
            _vm.Actualiser();
        }
    }
}
