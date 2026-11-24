using System.Windows;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Recapitulatif;

namespace GestionLocative.App.Views;

/// <summary>Liste des présences d'un client : séjours, nuitées et personnes par nuit, à imprimer ou enregistrer en PDF.</summary>
public partial class PresencesWindow : Window
{
    private readonly PresencesViewModel _vm;

    public PresencesWindow(BaseDeDonnees baseDeDonnees, int annee)
    {
        InitializeComponent();
        _vm = new PresencesViewModel(baseDeDonnees, annee);
        DataContext = _vm;
    }

    private void Imprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Impression() is { } tableau) Impression.Imprimer(this, tableau);
    }
}
