namespace GestionLocative.Core.Donnees;

/// <summary>
/// Location (séjour) d'un client. Champs obligatoires : dates de début et de fin, client, tarif TTC.
/// Par défaut l'arrivée est à 16h00 et le départ à 10h00.
/// </summary>
public sealed class Location
{
    /// <summary>Heure d'arrivée proposée par défaut.</summary>
    public static readonly TimeOnly HeureDebutParDefaut = new(16, 0);

    /// <summary>Heure de départ proposée par défaut.</summary>
    public static readonly TimeOnly HeureFinParDefaut = new(10, 0);

    /// <summary>Numéro automatique attribué par la base (0 tant que la location n'est pas enregistrée).</summary>
    public long IdLocation { get; set; }

    /// <summary>Date et heure d'arrivée (heure locale, à la minute).</summary>
    public DateTime DateDebut { get; set; }

    /// <summary>Date et heure de départ (heure locale, à la minute).</summary>
    public DateTime DateFin { get; set; }

    public long IdClient { get; set; }

    /// <summary>Tarif TTC en euros (stocké au centime près).</summary>
    public decimal TarifTtc { get; set; }

    public int NombreAdultes { get; set; }
    public int NombreEnfants { get; set; }
    public int NombreBebes { get; set; }

    /// <summary>Réservation passée par Airbnb.</summary>
    public bool Airbnb { get; set; }

    public string? NumeroReservation { get; set; }

    /// <summary>Crée une location du <paramref name="arrivee"/> 16h00 au <paramref name="depart"/> 10h00.</summary>
    public static Location Nouvelle(DateOnly arrivee, DateOnly depart, long idClient = 0) => new()
    {
        DateDebut = arrivee.ToDateTime(HeureDebutParDefaut),
        DateFin = depart.ToDateTime(HeureFinParDefaut),
        IdClient = idClient,
    };

    /// <summary>Nombre de nuits (ex. du 1er 16h au 3 10h = 2 nuits).</summary>
    public int NombreNuits => DateOnly.FromDateTime(DateFin).DayNumber - DateOnly.FromDateTime(DateDebut).DayNumber;

    /// <summary>Retourne la liste des erreurs de saisie (vide si la location est valide).</summary>
    public IReadOnlyList<string> Valider()
    {
        var erreurs = new List<string>();
        if (DateDebut == default) erreurs.Add("La date de début est obligatoire.");
        if (DateFin == default) erreurs.Add("La date de fin est obligatoire.");
        if (DateDebut != default && DateFin != default && DateFin <= DateDebut)
            erreurs.Add("La date de fin doit être après la date de début.");
        if (IdClient <= 0) erreurs.Add("Le client est obligatoire.");
        if (TarifTtc < 0) erreurs.Add("Le tarif TTC ne peut pas être négatif.");
        if (decimal.Round(TarifTtc, 2) != TarifTtc) erreurs.Add("Le tarif TTC est limité aux centimes.");
        if (NombreAdultes < 0 || NombreEnfants < 0 || NombreBebes < 0)
            erreurs.Add("Le nombre de personnes ne peut pas être négatif.");
        return erreurs;
    }
}
