using System.Globalization;
using Microsoft.Data.Sqlite;

namespace GestionLocative.Core.Donnees;

/// <summary>Lecture et enregistrement des locations.</summary>
public sealed class LocationRepository(BaseDeDonnees baseDeDonnees)
{
    private const string FormatDate = "yyyy-MM-dd HH:mm";

    private const string Colonnes = """
        idlocation, date_debut, date_fin, idclient, tarif_ttc_centimes,
        nombre_adultes, nombre_enfants, nombre_bebes, airbnb, numero_reservation
        """;

    /// <summary>Enregistre une nouvelle location et renseigne son <see cref="Location.IdLocation"/>.</summary>
    /// <exception cref="DonneesInvalidesException">Champ obligatoire manquant, dates incohérentes ou client inconnu.</exception>
    public void Ajouter(Location location)
    {
        Verifier(location);
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO locations (date_debut, date_fin, idclient, tarif_ttc_centimes,
                nombre_adultes, nombre_enfants, nombre_bebes, airbnb, numero_reservation)
            VALUES ($debut, $fin, $idclient, $tarif, $adultes, $enfants, $bebes, $airbnb, $reservation)
            RETURNING idlocation;
            """;
        AjouterParametres(commande, location);
        location.IdLocation = (long)ExecuterAvecClientExistant(commande.ExecuteScalar)!;
    }

    /// <exception cref="DonneesInvalidesException">Champ obligatoire manquant, client inconnu ou location introuvable.</exception>
    public void Modifier(Location location)
    {
        Verifier(location);
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            UPDATE locations SET date_debut = $debut, date_fin = $fin, idclient = $idclient,
                tarif_ttc_centimes = $tarif, nombre_adultes = $adultes, nombre_enfants = $enfants,
                nombre_bebes = $bebes, airbnb = $airbnb, numero_reservation = $reservation
            WHERE idlocation = $id;
            """;
        AjouterParametres(commande, location);
        commande.Parameters.AddWithValue("$id", location.IdLocation);
        if (ExecuterAvecClientExistant(commande.ExecuteNonQuery) is 0)
            throw new DonneesInvalidesException("Cette location n'existe plus dans la base.");
    }

    public void Supprimer(long idLocation)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM locations WHERE idlocation = $id;";
        commande.Parameters.AddWithValue("$id", idLocation);
        commande.ExecuteNonQuery();
    }

    public Location? ObtenirParId(long idLocation)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"SELECT {Colonnes} FROM locations WHERE idlocation = $id;";
        commande.Parameters.AddWithValue("$id", idLocation);
        using var lecteur = commande.ExecuteReader();
        return lecteur.Read() ? Lire(lecteur) : null;
    }

    /// <summary>
    /// Locations qui touchent au moins un jour entre <paramref name="du"/> et <paramref name="au"/> inclus
    /// (par ex. pour afficher un mois du calendrier), triées par date de début.
    /// </summary>
    public IReadOnlyList<Location> ListerEntre(DateOnly du, DateOnly au)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"""
            SELECT {Colonnes} FROM locations
            WHERE date_debut < $apres AND date_fin >= $du
            ORDER BY date_debut, idlocation;
            """;
        commande.Parameters.AddWithValue("$du", Formater(du.ToDateTime(TimeOnly.MinValue)));
        commande.Parameters.AddWithValue("$apres", Formater(au.AddDays(1).ToDateTime(TimeOnly.MinValue)));
        return LireTout(commande);
    }

    /// <summary>Historique des locations d'un client, la plus récente en premier.</summary>
    public IReadOnlyList<Location> ListerParClient(long idClient)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"SELECT {Colonnes} FROM locations WHERE idclient = $id ORDER BY date_debut DESC;";
        commande.Parameters.AddWithValue("$id", idClient);
        return LireTout(commande);
    }

    private static void Verifier(Location location)
    {
        var erreurs = location.Valider();
        if (erreurs.Count > 0) throw new DonneesInvalidesException(erreurs);
    }

    private static T ExecuterAvecClientExistant<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (SqliteException ex) when (BaseDeDonnees.EstErreurCleEtrangere(ex))
        {
            throw new DonneesInvalidesException("Le client de cette location n'existe pas dans la base.");
        }
    }

    private static void AjouterParametres(SqliteCommand commande, Location location)
    {
        commande.Parameters.AddWithValue("$debut", Formater(location.DateDebut));
        commande.Parameters.AddWithValue("$fin", Formater(location.DateFin));
        commande.Parameters.AddWithValue("$idclient", location.IdClient);
        commande.Parameters.AddWithValue("$tarif", (long)(location.TarifTtc * 100));
        commande.Parameters.AddWithValue("$adultes", location.NombreAdultes);
        commande.Parameters.AddWithValue("$enfants", location.NombreEnfants);
        commande.Parameters.AddWithValue("$bebes", location.NombreBebes);
        commande.Parameters.AddWithValue("$airbnb", location.Airbnb ? 1 : 0);
        commande.Parameters.AddWithValue("$reservation", ClientRepository.Nettoyer(location.NumeroReservation));
    }

    private static string Formater(DateTime date) => date.ToString(FormatDate, CultureInfo.InvariantCulture);

    private static DateTime Analyser(string texte) =>
        DateTime.ParseExact(texte, FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static List<Location> LireTout(SqliteCommand commande)
    {
        var locations = new List<Location>();
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read()) locations.Add(Lire(lecteur));
        return locations;
    }

    private static Location Lire(SqliteDataReader lecteur) => new()
    {
        IdLocation = lecteur.GetInt64(0),
        DateDebut = Analyser(lecteur.GetString(1)),
        DateFin = Analyser(lecteur.GetString(2)),
        IdClient = lecteur.GetInt64(3),
        TarifTtc = lecteur.GetInt64(4) / 100m,
        NombreAdultes = lecteur.GetInt32(5),
        NombreEnfants = lecteur.GetInt32(6),
        NombreBebes = lecteur.GetInt32(7),
        Airbnb = lecteur.GetInt64(8) != 0,
        NumeroReservation = lecteur.IsDBNull(9) ? null : lecteur.GetString(9),
    };
}
