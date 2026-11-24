using System.Windows;
using System.Windows.Controls;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.App.Views;

/// <summary>Création d'un client (ouverte depuis la fenêtre « Nouvelle location »).</summary>
public partial class ClientWindow : Window
{
    public ClientWindow(ClientRepository clients)
    {
        InitializeComponent();
        var vm = new ClientEditionViewModel(clients);
        vm.Enregistre += (_, client) =>
        {
            Client = client;
            DialogResult = true;
        };
        DataContext = vm;
    }

    /// <summary>Client créé (renseigné si l'utilisateur a enregistré).</summary>
    public Client? Client { get; private set; }

    /// <summary>Majuscule en début de prénom (et après un tiret ou un espace) pendant la saisie.</summary>
    private void Prenom_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox champ) return;
        var corrige = MiseEnForme.PrenomSaisie(champ.Text);
        if (corrige == champ.Text) return;
        var curseur = champ.CaretIndex;
        champ.Text = corrige;
        champ.CaretIndex = curseur;
    }
}
