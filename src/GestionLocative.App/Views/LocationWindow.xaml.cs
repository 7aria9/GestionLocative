using System.Windows;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.App.Views;

/// <summary>Création d'une location, avec choix ou création du client.</summary>
public partial class LocationWindow : Window
{
    private readonly LocationEditionViewModel _vm;

    public LocationWindow(BaseDeDonnees baseDeDonnees, DateOnly arrivee)
    {
        InitializeComponent();
        _vm = new LocationEditionViewModel(baseDeDonnees, arrivee)
        {
            Confirmer = question => MessageBox.Show(this, question, "Dates déjà occupées",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes,
        };
        _vm.Enregistree += (_, location) =>
        {
            Location = location;
            DialogResult = true;
        };
        DataContext = _vm;
    }

    /// <summary>Location créée (renseignée si l'utilisateur a enregistré).</summary>
    public Location? Location { get; private set; }

    private void NouveauClient_Click(object sender, RoutedEventArgs e)
    {
        var fenetre = new ClientWindow(_vm.DepotClients) { Owner = this };
        if (fenetre.ShowDialog() == true && fenetre.Client is not null)
            _vm.SelectionnerClient(fenetre.Client);
    }
}
