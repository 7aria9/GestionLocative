using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Import;

/// <summary>
/// Fenêtre « Importer (CSV) » : choix du type (clients ou locations) et du fichier, aperçu ligne par ligne
/// (à importer / déjà présente / rejetée, avec la raison), puis import en une seule fois.
/// </summary>
public sealed partial class ImportViewModel(BaseDeDonnees baseDeDonnees) : ObservableObject
{
    private TableauCsv? _tableau;

    /// <summary>Vrai : fichier de clients ; faux : fichier de locations.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportLocations), nameof(Consigne))]
    private bool _importClients = true;

    public bool ImportLocations
    {
        get => !ImportClients;
        set => ImportClients = !value;
    }

    partial void OnImportClientsChanged(bool value)
    {
        // Le même fichier relu avec l'autre type (l'utilisateur s'est trompé de bouton).
        if (_tableau is not null) Analyser();
    }

    public TypeImport Type => ImportClients ? TypeImport.Clients : TypeImport.Locations;

    public string Consigne => ImportClients
        ? "Colonnes attendues : Nom, Prénom, Téléphone, Email (obligatoires), et si possible le N° client de l'ancien programme, Adresse, Code postal, Ville, Plaque. Un client dont l'email existe déjà n'est pas recréé."
        : "Colonnes attendues : Arrivée, Départ, Tarif TTC, et le N° client de l'ancien programme ou l'email du client ; facultatives : heures, Adultes, Enfants, Bébés, Airbnb, N° de réservation. Importez les clients d'abord.";

    [ObservableProperty] private string? _cheminFichier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resume), nameof(DescriptionFichier), nameof(Colonnes), nameof(PeutImporter), nameof(AUnRapport))]
    [NotifyCanExecuteChangedFor(nameof(ImporterCommand))]
    private RapportImport? _rapport;

    public bool AUnRapport => Rapport is not null;
    public string Resume => Rapport?.Resume ?? "";
    public string DescriptionFichier => Rapport?.DescriptionFichier ?? "";

    /// <summary>Colonnes reconnues et ignorées, sur deux lignes.</summary>
    public string Colonnes => Rapport is null ? "" :
        "Colonnes reconnues : " + (Rapport.ColonnesReconnues.Count == 0 ? "aucune" : string.Join(" · ", Rapport.ColonnesReconnues)) +
        (Rapport.ColonnesIgnorees.Count == 0 ? "" : Environment.NewLine + "Colonnes ignorées : " + string.Join(", ", Rapport.ColonnesIgnorees));

    public bool PeutImporter => Rapport is { ErreurFichier: null, Enregistre: false, NombreAImporter: > 0 };

    /// <summary>Lignes affichées, selon le filtre (rejetées en premier par défaut).</summary>
    public ObservableCollection<LigneRapport> Lignes { get; } = [];

    /// <summary>« Rejetées », « Déjà présentes », « À importer » ou « Toutes ».</summary>
    public IReadOnlyList<string> Filtres { get; } = ["Toutes", "Rejetées", "Déjà présentes", "À importer"];

    [ObservableProperty] private string _filtre = "Toutes";

    partial void OnFiltreChanged(string value) => RemplirLignes();

    /// <summary>Problème de lecture du fichier ou de la base (null si tout va bien).</summary>
    [ObservableProperty] private string? _erreurs;

    /// <summary>Vrai dès qu'un import a été enregistré (le calendrier est à redessiner).</summary>
    public bool ImportFait { get; private set; }

    /// <summary>Lit le fichier et prépare l'aperçu.</summary>
    public void ChargerFichier(string chemin)
    {
        CheminFichier = chemin;
        _tableau = null;
        Rapport = null;
        Lignes.Clear();
        Erreurs = null;
        try
        {
            _tableau = LecteurCsv.LireFichier(chemin);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Erreurs = $"Impossible de lire le fichier (ouvert dans Excel ?) :\n{ex.Message}";
            return;
        }
        catch (FormatException ex)
        {
            Erreurs = ex.Message;
            return;
        }

        // Fichier de locations choisi alors que « Clients » est coché (ou l'inverse) : on bascule tout seul.
        var devine = DevinerType(_tableau);
        if (devine is not null && devine != Type) ImportClients = devine == TypeImport.Clients; // relance l'analyse
        else Analyser();
    }

    /// <summary>Recalcule l'aperçu (après un changement de type, ou d'autres modifications de la base).</summary>
    public void Analyser()
    {
        if (_tableau is null) return;
        Erreurs = null;
        try
        {
            Afficher(ImportCsv.Analyser(baseDeDonnees, Type, _tableau));
        }
        catch (Exception ex)
        {
            Rapport = null;
            Lignes.Clear();
            Erreurs = $"Impossible de lire la base de données :\n{ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(PeutImporter))]
    private void Importer()
    {
        if (_tableau is null) return;
        Erreurs = null;
        try
        {
            Afficher(ImportCsv.Importer(baseDeDonnees, Type, _tableau));
            ImportFait = true;
        }
        catch (Exception ex)
        {
            Erreurs = $"L'import a échoué, rien n'a été enregistré :\n{ex.Message}";
        }
    }

    private void Afficher(RapportImport rapport)
    {
        Rapport = rapport;
        Filtre = rapport.NombreRejetees > 0 && !rapport.Enregistre ? "Rejetées" : "Toutes";
        RemplirLignes();
    }

    private void RemplirLignes()
    {
        Lignes.Clear();
        if (Rapport is null) return;
        var statut = Filtre switch
        {
            "Rejetées" => StatutLigne.Rejetee,
            "Déjà présentes" => StatutLigne.DejaPresente,
            "À importer" => StatutLigne.AImporter,
            _ => (StatutLigne?)null,
        };
        foreach (var ligne in Rapport.Lignes.Where(l => statut is null || l.Statut == statut)
                     .OrderBy(l => statut is null ? (int)l.Statut switch { 2 => 0, 1 => 1, _ => 2 } : 0)
                     .ThenBy(l => l.NumeroLigne))
            Lignes.Add(ligne);
    }

    /// <summary>Clients ou locations, d'après les colonnes (null si on ne peut pas trancher).</summary>
    internal static TypeImport? DevinerType(TableauCsv tableau)
    {
        var locations = CorrespondanceColonnes.Etablir(tableau.Entetes, ImportCsvLocations.Champs);
        var clients = CorrespondanceColonnes.Etablir(tableau.Entetes, ImportCsvClients.Champs);
        var estLocations = locations.Contient(ImportCsvLocations.DateDebut) && locations.Contient(ImportCsvLocations.DateFin);
        var estClients = clients.Contient(ImportCsvClients.Nom) && clients.Contient(ImportCsvClients.Telephone);
        return estLocations == estClients ? null : estLocations ? TypeImport.Locations : TypeImport.Clients;
    }
}
