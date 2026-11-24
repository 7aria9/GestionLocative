using Microsoft.Data.Sqlite;

namespace GestionLocative.Core.Donnees;

/// <summary>Lecture et enregistrement des clients.</summary>
public sealed class ClientRepository(BaseDeDonnees baseDeDonnees)
{
    private const string Colonnes =
        "idclient, nom, prenom, adresse, code_postal, ville, telephone, email, plaque_immatriculation";

    /// <summary>Enregistre un nouveau client et renseigne son <see cref="Client.IdClient"/>.</summary>
    /// <exception cref="DonneesInvalidesException">Champ obligatoire manquant ou email déjà utilisé.</exception>
    public void Ajouter(Client client)
    {
        Verifier(client);
        using var connexion = baseDeDonnees.Ouvrir();
        Inserer(connexion, null, client);
    }

    /// <summary>INSERT d'un client déjà normalisé et validé, dans une connexion (et transaction) existante.</summary>
    internal static void Inserer(SqliteConnection connexion, SqliteTransaction? transaction, Client client)
    {
        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = """
            INSERT INTO clients (nom, prenom, adresse, code_postal, ville, telephone, email, plaque_immatriculation)
            VALUES ($nom, $prenom, $adresse, $code_postal, $ville, $telephone, $email, $plaque)
            RETURNING idclient;
            """;
        AjouterParametres(commande, client);
        client.IdClient = (long)ExecuterAvecEmailUnique(commande.ExecuteScalar)!;
    }

    /// <exception cref="DonneesInvalidesException">Champ obligatoire manquant, email déjà utilisé ou client introuvable.</exception>
    public void Modifier(Client client)
    {
        Verifier(client);
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            UPDATE clients SET nom = $nom, prenom = $prenom, adresse = $adresse, code_postal = $code_postal,
                ville = $ville, telephone = $telephone, email = $email, plaque_immatriculation = $plaque
            WHERE idclient = $id;
            """;
        AjouterParametres(commande, client);
        commande.Parameters.AddWithValue("$id", client.IdClient);
        if (ExecuterAvecEmailUnique(commande.ExecuteNonQuery) is 0)
            throw new DonneesInvalidesException("Ce client n'existe plus dans la base.");
    }

    /// <summary>Supprime un client. Refusé s'il a encore des locations.</summary>
    public void Supprimer(long idClient)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM clients WHERE idclient = $id;";
        commande.Parameters.AddWithValue("$id", idClient);
        try
        {
            commande.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (BaseDeDonnees.EstErreurCleEtrangere(ex))
        {
            throw new DonneesInvalidesException("Ce client a des locations : supprimez-les d'abord.");
        }
    }

    public Client? ObtenirParId(long idClient)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"SELECT {Colonnes} FROM clients WHERE idclient = $id;";
        commande.Parameters.AddWithValue("$id", idClient);
        using var lecteur = commande.ExecuteReader();
        return lecteur.Read() ? Lire(lecteur) : null;
    }

    public Client? ObtenirParEmail(string email)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"SELECT {Colonnes} FROM clients WHERE email = $email;";
        commande.Parameters.AddWithValue("$email", email.Trim());
        using var lecteur = commande.ExecuteReader();
        return lecteur.Read() ? Lire(lecteur) : null;
    }

    /// <summary>
    /// Liste les clients triés par nom puis prénom. Si <paramref name="recherche"/> est renseigné,
    /// ne garde que ceux dont le nom, le prénom, l'email, le téléphone ou la plaque contient ce texte.
    /// </summary>
    public IReadOnlyList<Client> Lister(string? recherche = null)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        var filtre = "";
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            filtre = """
                WHERE nom LIKE $motif ESCAPE '\' OR prenom LIKE $motif ESCAPE '\' OR email LIKE $motif ESCAPE '\'
                   OR telephone LIKE $motif ESCAPE '\' OR plaque_immatriculation LIKE $motif ESCAPE '\'
                """;
            var echappe = recherche.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            commande.Parameters.AddWithValue("$motif", $"%{echappe}%");

            // Les téléphones sont enregistrés au format international : « 06 12 » doit retrouver « +33612... ».
            if (MotifTelephone(recherche) is { } motifTelephone)
            {
                filtre += " OR telephone LIKE $motif_tel";
                commande.Parameters.AddWithValue("$motif_tel", motifTelephone);
            }
        }
        commande.CommandText = $"SELECT {Colonnes} FROM clients {filtre} ORDER BY nom COLLATE NOCASE, prenom COLLATE NOCASE;";

        var clients = new List<Client>();
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read()) clients.Add(Lire(lecteur));
        return clients;
    }

    /// <summary>Recherche tapée comme un numéro (« 06 12 34 ») → motif sur le format international (« %61234% ») ; sinon null.</summary>
    private static string? MotifTelephone(string recherche)
    {
        var texte = recherche.Trim();
        if (texte.Length < 2 || !texte.All(c => char.IsAsciiDigit(c) || c is ' ' or '.' or '-' or '+')) return null;
        var chiffres = new string(texte.Where(char.IsAsciiDigit).ToArray());
        if (chiffres.Length < 2) return null;
        if (chiffres.StartsWith("00")) chiffres = chiffres[2..];
        else if (chiffres[0] == '0') chiffres = chiffres[1..];
        return $"%{chiffres}%";
    }

    private static void Verifier(Client client)
    {
        client.Normaliser();
        var erreurs = client.Valider();
        if (erreurs.Count > 0) throw new DonneesInvalidesException(erreurs);
    }

    private static T ExecuterAvecEmailUnique<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067) // SQLITE_CONSTRAINT_UNIQUE
        {
            throw new DonneesInvalidesException("Un autre client utilise déjà cet email.");
        }
    }

    private static void AjouterParametres(SqliteCommand commande, Client client)
    {
        commande.Parameters.AddWithValue("$nom", client.Nom.Trim());
        commande.Parameters.AddWithValue("$prenom", client.Prenom.Trim());
        commande.Parameters.AddWithValue("$adresse", Nettoyer(client.Adresse));
        commande.Parameters.AddWithValue("$code_postal", Nettoyer(client.CodePostal));
        commande.Parameters.AddWithValue("$ville", Nettoyer(client.Ville));
        commande.Parameters.AddWithValue("$telephone", client.Telephone.Trim());
        commande.Parameters.AddWithValue("$email", client.Email.Trim());
        commande.Parameters.AddWithValue("$plaque", Nettoyer(client.PlaqueImmatriculation?.ToUpperInvariant()));
    }

    /// <summary>Texte facultatif : espaces retirés, vide enregistré comme NULL.</summary>
    internal static object Nettoyer(string? texte) =>
        string.IsNullOrWhiteSpace(texte) ? DBNull.Value : texte.Trim();

    private static Client Lire(SqliteDataReader lecteur) => new()
    {
        IdClient = lecteur.GetInt64(0),
        Nom = lecteur.GetString(1),
        Prenom = lecteur.GetString(2),
        Adresse = lecteur.IsDBNull(3) ? null : lecteur.GetString(3),
        CodePostal = lecteur.IsDBNull(4) ? null : lecteur.GetString(4),
        Ville = lecteur.IsDBNull(5) ? null : lecteur.GetString(5),
        Telephone = lecteur.GetString(6),
        Email = lecteur.GetString(7),
        PlaqueImmatriculation = lecteur.IsDBNull(8) ? null : lecteur.GetString(8),
    };
}
