using System.Windows;
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
}
