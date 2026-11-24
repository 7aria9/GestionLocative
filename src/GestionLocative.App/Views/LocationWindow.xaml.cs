using System.Windows;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.App.Views;

/// <summary>Création, modification ou suppression d'une location, avec choix ou création du client.</summary>
public partial class LocationWindow : Window
{
    private readonly LocationEditionViewModel _vm;

    /// <summary>Nouvelle location commençant le jour <paramref name="arrivee"/>.</summary>
    public LocationWindow(BaseDeDonnees baseDeDonnees, DateOnly arrivee)
        : this(new LocationEditionViewModel(baseDeDonnees, arrivee))
    {
    }

    /// <summary>Modification (ou suppression) d'une location existante.</summary>
    public LocationWindow(BaseDeDonnees baseDeDonnees, Location existante)
        : this(new LocationEditionViewModel(baseDeDonnees, existante))
    {
    }

    private LocationWindow(LocationEditionViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.Confirmer = question => MessageBox.Show(this, question, _vm.Titre,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        _vm.Enregistree += (_, location) =>
        {
            Location = location;
            DialogResult = true;
        };
        _vm.Supprimee += (_, _) => DialogResult = true;
        DataContext = _vm;
    }

    /// <summary>Location créée ou modifiée (null si elle a été supprimée ou si l'utilisateur a annulé).</summary>
    public Location? Location { get; private set; }

    private void NouveauClient_Click(object sender, RoutedEventArgs e)
    {
        var fenetre = new ClientWindow(_vm.DepotClients) { Owner = this };
        if (fenetre.ShowDialog() == true && fenetre.Client is not null)
            _vm.SelectionnerClient(fenetre.Client);
    }
}
