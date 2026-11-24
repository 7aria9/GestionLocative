using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.ViewModels;

/// <summary>Fenêtre « Nouveau client ». Nom, prénom, téléphone et email sont obligatoires, l'email est unique.</summary>
public sealed partial class ClientEditionViewModel(ClientRepository clients) : ObservableObject
{
    [ObservableProperty] private string _nom = "";
    [ObservableProperty] private string _prenom = "";
    [ObservableProperty] private string _adresse = "";
    [ObservableProperty] private string _codePostal = "";
    [ObservableProperty] private string _ville = "";
    [ObservableProperty] private string _telephone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _plaqueImmatriculation = "";

    /// <summary>Erreurs de saisie à afficher (null si aucune).</summary>
    [ObservableProperty] private string? _erreurs;

    /// <summary>Client enregistré (renseigné après un enregistrement réussi).</summary>
    public Client? ClientEnregistre { get; private set; }

    /// <summary>Déclenché quand le client a été enregistré dans la base.</summary>
    public event EventHandler<Client>? Enregistre;

    [RelayCommand]
    private void Enregistrer()
    {
        var client = new Client
        {
            Nom = Nom,
            Prenom = Prenom,
            Adresse = Adresse,
            CodePostal = CodePostal,
            Ville = Ville,
            Telephone = Telephone,
            Email = Email.Trim(),
            PlaqueImmatriculation = PlaqueImmatriculation,
        };

        // Affiche la mise en forme appliquée (NOM, Prénom, +33..., email en minuscules).
        client.Normaliser();
        Nom = client.Nom;
        Prenom = client.Prenom;
        Telephone = client.Telephone;
        Email = client.Email;

        var erreurs = client.Valider();
        if (erreurs.Count > 0)
        {
            Erreurs = string.Join(Environment.NewLine, erreurs);
            return;
        }

        try
        {
            var existant = clients.ObtenirParEmail(client.Email);
            if (existant is not null)
            {
                Erreurs = $"Cet email est déjà utilisé par le client {existant.NomComplet}. " +
                          "Fermez cette fenêtre et recherchez-le dans la liste.";
                return;
            }
            clients.Ajouter(client);
        }
        catch (DonneesInvalidesException ex)
        {
            Erreurs = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible d'enregistrer le client : {ex.Message}";
            return;
        }

        Erreurs = null;
        ClientEnregistre = client;
        Enregistre?.Invoke(this, client);
    }
}
