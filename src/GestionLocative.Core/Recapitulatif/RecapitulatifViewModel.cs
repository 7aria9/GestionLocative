using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Recapitulatif;

/// <summary>
/// Fenêtre « Récapitulatif » : toutes les locations d'une année (l'année en cours à l'ouverture), avec les totaux HT et TTC.
/// Une location à cheval sur deux années compte dans l'année de son arrivée, pour que chaque location n'apparaisse qu'une fois.
/// </summary>
public sealed partial class RecapitulatifViewModel : ObservableObject
{
    private readonly BaseDeDonnees _base;

    public RecapitulatifViewModel(BaseDeDonnees baseDeDonnees, int annee)
    {
        _base = baseDeDonnees;
        _annee = annee;
        Actualiser();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titre))]
    private int _annee;

    public string Titre => $"Récapitulatif {Annee}";

    public ObservableCollection<LigneRecapitulatif> Lignes { get; } = [];

    [ObservableProperty] private decimal _totalHt;
    [ObservableProperty] private decimal _totalTtc;
    [ObservableProperty] private int _totalNuits;
    [ObservableProperty] private decimal _tauxTva;

    /// <summary>Ex. « 12 locations · 84 nuits · TVA 10 % ».</summary>
    [ObservableProperty] private string _resume = "";

    /// <summary>Message d'erreur de lecture (base inaccessible...), null si tout va bien.</summary>
    [ObservableProperty] private string? _erreurs;

    public string TotalHtAffiche => LigneRecapitulatif.Montant(TotalHt);
    public string TotalTtcAffiche => LigneRecapitulatif.Montant(TotalTtc);

    partial void OnTotalHtChanged(decimal value) => OnPropertyChanged(nameof(TotalHtAffiche));
    partial void OnTotalTtcChanged(decimal value) => OnPropertyChanged(nameof(TotalTtcAffiche));
    partial void OnAnneeChanged(int value) => Actualiser();

    [RelayCommand] private void AnneePrecedente() => Annee--;
    [RelayCommand] private void AnneeSuivante() => Annee++;

    /// <summary>Relit les locations de l'année (après une modification, ou depuis un autre poste).</summary>
    public void Actualiser()
    {
        Lignes.Clear();
        Erreurs = null;
        try
        {
            TauxTva = new ReglagesRepository(_base).ObtenirTauxTva();
            var clients = new ClientRepository(_base).Lister().ToDictionary(c => c.IdClient, c => c.NomComplet);
            foreach (var location in new LocationRepository(_base).ListerParAnneeArrivee(Annee))
                Lignes.Add(new LigneRecapitulatif(location, clients.GetValueOrDefault(location.IdClient, "(client supprimé)"), TauxTva));
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible de lire les locations : {ex.Message}";
        }

        // Le total HT est la somme des HT de chaque ligne, pour retomber exactement sur le tableau.
        TotalHt = Lignes.Sum(l => l.Ht);
        TotalTtc = Lignes.Sum(l => l.Ttc);
        TotalNuits = Lignes.Sum(l => l.Nuits);
        Resume = Lignes.Count == 0
            ? $"Aucune location avec une arrivée en {Annee}."
            : $"{LigneRecapitulatif.Pluriel(Lignes.Count, "location")} · {LigneRecapitulatif.Pluriel(TotalNuits, "nuit")} · TVA {TauxTva:0.##} %";
    }

    /// <summary>Le tableau tel qu'il sera imprimé.</summary>
    public TableauImprimable Impression() => new(
        Titre,
        [Resume, "Une location à cheval sur deux années compte dans l'année de son arrivée."],
        [
            new("Arrivée", Largeur: 1.1), new("Départ", Largeur: 1.1), new("Client", Largeur: 2.4),
            new("Nuits", true, 0.7), new("Pers.", true, 0.7), new("Total HT", true, 1.3), new("Total TTC", true, 1.3),
        ],
        Lignes.Select(l => (IReadOnlyList<string>)[l.Arrivee, l.Depart, l.Client, l.Nuits.ToString(), l.Personnes.ToString(), l.HtAffiche, l.TtcAffiche]).ToList(),
        ["Total", "", "", TotalNuits.ToString(), "", TotalHtAffiche, TotalTtcAffiche]);
}
