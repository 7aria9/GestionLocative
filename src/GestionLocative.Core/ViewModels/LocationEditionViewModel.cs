using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.ViewModels;

/// <summary>
/// Fenêtre « Nouvelle location » / « Modifier la location » : dates (arrivée 16h00 / départ 10h00 par défaut), client obligatoire
/// choisi par recherche (ou créé depuis la fenêtre), tarif TTC, voyageurs, Airbnb et n° de réservation.
/// </summary>
public sealed partial class LocationEditionViewModel : ObservableObject
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ClientRepository _clients;
    private readonly LocationRepository _locations;
    private readonly decimal _tauxTva;

    /// <summary>Numéro de la location modifiée (0 pour une nouvelle location).</summary>
    private readonly long _idLocation;

    /// <summary>Ouvre la fenêtre pour modifier (ou supprimer) une location existante.</summary>
    public LocationEditionViewModel(BaseDeDonnees baseDeDonnees, Location existante)
        : this(baseDeDonnees, DateOnly.FromDateTime(existante.DateDebut))
    {
        _idLocation = existante.IdLocation;
        _dateDebut = existante.DateDebut.Date;
        _dateFin = existante.DateFin.Date;
        _heureDebut = existante.DateDebut.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        _heureFin = existante.DateFin.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        _tarifTtc = existante.TarifTtc.ToString("0.00", Francais);
        _nombreAdultes = existante.NombreAdultes.ToString(CultureInfo.InvariantCulture);
        _nombreEnfants = existante.NombreEnfants.ToString(CultureInfo.InvariantCulture);
        _nombreBebes = existante.NombreBebes.ToString(CultureInfo.InvariantCulture);
        _airbnb = existante.Airbnb;
        _numeroReservation = existante.NumeroReservation ?? "";

        try
        {
            if (_clients.ObtenirParId(existante.IdClient) is { } client) SelectionnerClient(client);
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible de lire le client : {ex.Message}";
        }
    }

    /// <summary>Ouvre la fenêtre pour créer une location qui commence le jour <paramref name="arrivee"/>.</summary>
    public LocationEditionViewModel(BaseDeDonnees baseDeDonnees, DateOnly arrivee)
    {
        _clients = new ClientRepository(baseDeDonnees);
        _locations = new LocationRepository(baseDeDonnees);
        try { _tauxTva = new ReglagesRepository(baseDeDonnees).ObtenirTauxTva(); }
        catch (Exception) { _tauxTva = ReglagesRepository.TauxTvaParDefaut; }

        _dateDebut = arrivee.ToDateTime(TimeOnly.MinValue);
        _dateFin = arrivee.AddDays(1).ToDateTime(TimeOnly.MinValue);
        _heureDebut = Location.HeureDebutParDefaut.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        _heureFin = Location.HeureFinParDefaut.ToString("HH\\:mm", CultureInfo.InvariantCulture);

        ChargerClients();
    }

    /// <summary>Jour d'arrivée (la partie heure est ignorée).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumeSejour))]
    private DateTime? _dateDebut;

    /// <summary>Heure d'arrivée, ex. « 16:00 » ou « 16h ».</summary>
    [ObservableProperty]
    private string _heureDebut;

    /// <summary>Jour de départ (la partie heure est ignorée).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumeSejour))]
    private DateTime? _dateFin;

    /// <summary>Heure de départ, ex. « 10:00 » ou « 10h ».</summary>
    [ObservableProperty]
    private string _heureFin;

    /// <summary>Texte tapé pour retrouver un client (nom, prénom, email, téléphone, plaque).</summary>
    [ObservableProperty]
    private string _rechercheClient = "";

    /// <summary>Clients correspondant à la recherche.</summary>
    public ObservableCollection<Client> Clients { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClientChoisi))]
    private Client? _clientSelectionne;

    public bool ClientChoisi => ClientSelectionne is not null;

    /// <summary>Tarif TTC en euros, ex. « 450 », « 450,50 » ou « 450,50 € ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumeTarif))]
    private string _tarifTtc = "";

    /// <summary>Ex. « soit 409,09 € HT (TVA 10 %) » ; vide si le tarif n'est pas encore un montant.</summary>
    public string ResumeTarif => LireMontant(TarifTtc) is { } ttc && ttc >= 0
        ? $"soit {Location.CalculerHt(ttc, _tauxTva).ToString("#,##0.00 €", Francais)} HT (TVA {_tauxTva.ToString("0.##", Francais)} %)"
        : "";
    [ObservableProperty] private string _nombreAdultes = "0";
    [ObservableProperty] private string _nombreEnfants = "0";
    [ObservableProperty] private string _nombreBebes = "0";
    [ObservableProperty] private bool _airbnb;
    [ObservableProperty] private string _numeroReservation = "";

    /// <summary>Erreurs de saisie à afficher (null si aucune).</summary>
    [ObservableProperty] private string? _erreurs;

    /// <summary>Ex. « 3 nuits ».</summary>
    public string ResumeSejour
    {
        get
        {
            if (DateDebut is null || DateFin is null) return "";
            var nuits = DateOnly.FromDateTime(DateFin.Value).DayNumber - DateOnly.FromDateTime(DateDebut.Value).DayNumber;
            return nuits switch
            {
                < 0 => "Le départ est avant l'arrivée",
                0 => "Même jour",
                1 => "1 nuit",
                _ => $"{nuits} nuits",
            };
        }
    }

    /// <summary>Vrai quand la fenêtre modifie une location existante (le bouton « Supprimer » est alors proposé).</summary>
    public bool EstModification => _idLocation > 0;

    public string Titre => EstModification ? "Modifier la location" : "Nouvelle location";

    /// <summary>Déclenché quand la location a été supprimée de la base.</summary>
    public event EventHandler? Supprimee;

    /// <summary>Location enregistrée (renseignée après un enregistrement réussi).</summary>
    public Location? LocationEnregistree { get; private set; }

    /// <summary>
    /// Demande une confirmation à l'utilisateur (oui / non). Utilisé si la location chevauche une autre.
    /// Sans confirmation branchée, l'enregistrement est accepté.
    /// </summary>
    public Func<string, bool>? Confirmer { get; set; }

    /// <summary>Déclenché quand la location a été enregistrée dans la base.</summary>
    public event EventHandler<Location>? Enregistree;

    partial void OnRechercheClientChanged(string value) => ChargerClients();

    partial void OnDateDebutChanged(DateTime? value)
    {
        // Garde le départ après l'arrivée quand on avance la date d'arrivée.
        if (value is not null && (DateFin is null || DateFin.Value.Date <= value.Value.Date))
            DateFin = value.Value.Date.AddDays(1);
    }

    /// <summary>Sélectionne un client qui vient d'être créé (il apparaît seul dans la liste).</summary>
    public void SelectionnerClient(Client client)
    {
        RechercheClient = client.Email;
        ClientSelectionne = Clients.FirstOrDefault(c => c.IdClient == client.IdClient) ?? client;
        if (!Clients.Any(c => c.IdClient == client.IdClient)) Clients.Insert(0, client);
    }

    /// <summary>Accès aux clients pour la fenêtre « Nouveau client ».</summary>
    public ClientRepository DepotClients => _clients;

    private void ChargerClients()
    {
        var selection = ClientSelectionne;
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

        Clients.Clear();
        foreach (var client in resultat) Clients.Add(client);

        // Conserve le client choisi s'il fait toujours partie des résultats ; un seul résultat est choisi d'office.
        ClientSelectionne = Clients.FirstOrDefault(c => c.IdClient == selection?.IdClient)
            ?? (Clients.Count == 1 ? Clients[0] : null);
    }

    [RelayCommand]
    private void Enregistrer()
    {
        var erreurs = new List<string>();
        var location = new Location
        {
            IdLocation = _idLocation,
            IdClient = ClientSelectionne?.IdClient ?? 0,
            Airbnb = Airbnb,
            NumeroReservation = NumeroReservation,
        };

        var heureDebut = LireHeure(HeureDebut, "d'arrivée", erreurs);
        var heureFin = LireHeure(HeureFin, "de départ", erreurs);
        if (DateDebut is null) erreurs.Add("La date d'arrivée est obligatoire.");
        else if (heureDebut is not null) location.DateDebut = DateOnly.FromDateTime(DateDebut.Value).ToDateTime(heureDebut.Value);
        if (DateFin is null) erreurs.Add("La date de départ est obligatoire.");
        else if (heureFin is not null) location.DateFin = DateOnly.FromDateTime(DateFin.Value).ToDateTime(heureFin.Value);

        if (string.IsNullOrWhiteSpace(TarifTtc)) erreurs.Add("Le tarif TTC est obligatoire.");
        else if (LireMontant(TarifTtc) is { } tarif) location.TarifTtc = tarif;
        else erreurs.Add("Le tarif TTC n'est pas un montant valide (ex. 450 ou 450,50).");

        location.NombreAdultes = LireNombre(NombreAdultes, "d'adultes", erreurs);
        location.NombreEnfants = LireNombre(NombreEnfants, "d'enfants", erreurs);
        location.NombreBebes = LireNombre(NombreBebes, "de bébés", erreurs);

        // Les erreurs de saisie passent avant celles du modèle (sinon « date obligatoire » serait affiché en double).
        if (erreurs.Count == 0) erreurs.AddRange(location.Valider());
        else if (location.IdClient <= 0) erreurs.Add("Le client est obligatoire.");

        if (erreurs.Count > 0)
        {
            Erreurs = string.Join(Environment.NewLine, erreurs.Distinct());
            return;
        }

        try
        {
            var chevauchements = _locations
                .ListerEntre(DateOnly.FromDateTime(location.DateDebut), DateOnly.FromDateTime(location.DateFin))
                .Where(l => l.IdLocation != location.IdLocation)
                .Where(l => l.DateDebut < location.DateFin && l.DateFin > location.DateDebut)
                .ToList();
            if (chevauchements.Count > 0 && Confirmer is not null)
            {
                var detail = string.Join(Environment.NewLine, chevauchements.Select(l =>
                    $"• du {l.DateDebut.ToString("dd/MM/yyyy HH:mm", Francais)} au {l.DateFin.ToString("dd/MM/yyyy HH:mm", Francais)}"
                    + $" ({_clients.ObtenirParId(l.IdClient)?.NomComplet ?? "client inconnu"})"));
                if (!Confirmer($"Ces dates chevauchent une location existante :{Environment.NewLine}{detail}" +
                               $"{Environment.NewLine}{Environment.NewLine}Enregistrer quand même ?"))
                    return;
            }

            if (EstModification) _locations.Modifier(location);
            else _locations.Ajouter(location);
        }
        catch (DonneesInvalidesException ex)
        {
            Erreurs = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible d'enregistrer la location : {ex.Message}";
            return;
        }

        Erreurs = null;
        LocationEnregistree = location;
        Enregistree?.Invoke(this, location);
    }

    [RelayCommand(CanExecute = nameof(EstModification))]
    private void Supprimer()
    {
        var resume = $"du {DateDebut?.ToString("dd/MM/yyyy", Francais)} au {DateFin?.ToString("dd/MM/yyyy", Francais)}"
            + (ClientSelectionne is null ? "" : $" ({ClientSelectionne.NomComplet})");
        if (Confirmer is not null && !Confirmer($"Supprimer définitivement la location {resume} ?"))
            return;
        try
        {
            _locations.Supprimer(_idLocation);
        }
        catch (Exception ex)
        {
            Erreurs = $"Impossible de supprimer la location : {ex.Message}";
            return;
        }
        Supprimee?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Accepte « 16:00 », « 16h00 », « 16h », « 16 », « 9:30 ».</summary>
    internal static TimeOnly? LireHeure(string? texte, string quoi, List<string> erreurs)
    {
        var t = (texte ?? "").Trim().ToLowerInvariant().Replace('h', ':');
        if (t.EndsWith(':')) t += "00";
        if (!t.Contains(':')) t += ":00";
        var morceaux = t.Split(':');
        if (morceaux.Length == 2
            && int.TryParse(morceaux[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            && int.TryParse(morceaux[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            && h is >= 0 and < 24 && m is >= 0 and < 60)
            return new TimeOnly(h, m);

        erreurs.Add($"L'heure {quoi} n'est pas valide (ex. 16:00).");
        return null;
    }

    /// <summary>Accepte « 450 », « 450,5 », « 450.50 », « 1 200,00 € ».</summary>
    internal static decimal? LireMontant(string texte)
    {
        var t = texte.Replace("€", "").Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace('.', ',').Trim();
        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, Francais, out var montant) ? montant : null;
    }

    private static int LireNombre(string? texte, string quoi, List<string> erreurs)
    {
        if (string.IsNullOrWhiteSpace(texte)) return 0;
        if (int.TryParse(texte.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 100) return n;
        erreurs.Add($"Le nombre {quoi} n'est pas valide.");
        return 0;
    }
}
