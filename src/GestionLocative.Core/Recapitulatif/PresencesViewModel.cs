using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Recapitulatif;

/// <summary>Un séjour dans la liste des présences d'un client.</summary>
public sealed class LignePresence(Location location)
{
    public Location Location { get; } = location;
    public string Arrivee => Location.DateDebut.ToString("dd/MM/yyyy", LigneRecapitulatif.Francais);
    public string Depart => Location.DateFin.ToString("dd/MM/yyyy", LigneRecapitulatif.Francais);
    public int Nuitees => Location.NombreNuits;

    /// <summary>Nombre de personnes présentes chaque nuit du séjour.</summary>
    public int PersonnesParNuit => Location.NombrePersonnes;

    /// <summary>Ex. « 2 adultes, 1 enfant ».</summary>
    public string DetailPersonnes => LigneRecapitulatif.DetailVoyageurs(Location);

    /// <summary>Nuitées × personnes (base de la taxe de séjour, par ex.).</summary>
    public int NuiteesPersonnes => Nuitees * PersonnesParNuit;
}

/// <summary>
/// Fenêtre « Liste des présences » : pour un client choisi, ses séjours de l'année avec le nombre de nuitées
/// et le nombre de personnes par nuit, à imprimer ou enregistrer en PDF.
/// </summary>
public sealed partial class PresencesViewModel : ObservableObject
{
    private readonly ClientRepository _clients;
    private readonly LocationRepository _locations;

    public PresencesViewModel(BaseDeDonnees baseDeDonnees, int annee, long? idClient = null)
    {
        _clients = new ClientRepository(baseDeDonnees);
        _locations = new LocationRepository(baseDeDonnees);
        _annee = annee;
        ChargerClients();
        if (idClient is not null) ClientSelectionne = Clients.FirstOrDefault(c => c.IdClient == idClient);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titre))]
    private int _annee;

    [ObservableProperty] private string _rechercheClient = "";

    public ObservableCollection<Client> Clients { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titre), nameof(ClientChoisi))]
    private Client? _clientSelectionne;

    public bool ClientChoisi => ClientSelectionne is not null;

    public string Titre => ClientSelectionne is null
        ? $"Liste des présences {Annee}"
        : $"Liste des présences {Annee} · {ClientSelectionne.NomComplet}";

    public ObservableCollection<LignePresence> Lignes { get; } = [];

    [ObservableProperty] private int _totalNuitees;
    [ObservableProperty] private int _totalNuiteesPersonnes;

    /// <summary>Ex. « 2 séjours · 9 nuitées · 31 nuitées-personnes ».</summary>
    [ObservableProperty] private string _resume = "Choisissez un client dans la liste.";

    [ObservableProperty] private string? _erreurs;

    partial void OnRechercheClientChanged(string value) => ChargerClients();
    partial void OnAnneeChanged(int value) => ChargerSejours();
    partial void OnClientSelectionneChanged(Client? value) => ChargerSejours();

    [RelayCommand] private void AnneePrecedente() => Annee--;
    [RelayCommand] private void AnneeSuivante() => Annee++;

    private void ChargerClients()
    {
        IReadOnlyList<Client> resultat;
        try
        {
            resultat = _clients.Lister(RechercheClient);
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible de lire les clients : {ex.Message}";
            return;
        }
        var selection = ClientSelectionne;
        Clients.Clear();
        foreach (var client in resultat) Clients.Add(client);
        ClientSelectionne = Clients.FirstOrDefault(c => c.IdClient == selection?.IdClient)
            ?? (Clients.Count == 1 ? Clients[0] : null);
    }

    private void ChargerSejours()
    {
        Lignes.Clear();
        Erreurs = null;
        if (ClientSelectionne is { } client)
        {
            try
            {
                foreach (var location in _locations.ListerParAnneeArrivee(Annee, client.IdClient))
                    Lignes.Add(new LignePresence(location));
            }
            catch (Exception ex)
            {
                Erreurs = $"Impossible de lire les séjours : {ex.Message}";
            }
        }
        TotalNuitees = Lignes.Sum(l => l.Nuitees);
        TotalNuiteesPersonnes = Lignes.Sum(l => l.NuiteesPersonnes);
        Resume = ClientSelectionne is null ? "Choisissez un client dans la liste."
            : Lignes.Count == 0 ? $"Aucun séjour avec une arrivée en {Annee}."
            : $"{LigneRecapitulatif.Pluriel(Lignes.Count, "séjour")} · {LigneRecapitulatif.Pluriel(TotalNuitees, "nuitée")} · {LigneRecapitulatif.Pluriel(TotalNuiteesPersonnes, "nuitée")}-personnes";
    }

    /// <summary>La liste telle qu'elle sera imprimée (null tant qu'aucun client n'est choisi).</summary>
    public TableauImprimable? Impression()
    {
        if (ClientSelectionne is not { } client) return null;
        var coordonnees = string.Join(" · ", new[] { client.TelephoneAffiche, client.Email }.Where(t => !string.IsNullOrWhiteSpace(t)));
        return new(
            $"Liste des présences {Annee}",
            [client.NomComplet, coordonnees, Resume],
            [
                new("Arrivée", Largeur: 1.1), new("Départ", Largeur: 1.1), new("Nuitées", true, 0.9),
                new("Personnes par nuit", true, 1.2), new("Détail", Largeur: 2), new("Nuitées × pers.", true, 1.2),
            ],
            Lignes.Select(l => (IReadOnlyList<string>)[l.Arrivee, l.Depart, l.Nuitees.ToString(), l.PersonnesParNuit.ToString(), l.DetailPersonnes, l.NuiteesPersonnes.ToString()]).ToList(),
            ["Total", "", TotalNuitees.ToString(), "", "", TotalNuiteesPersonnes.ToString()]);
    }
}
