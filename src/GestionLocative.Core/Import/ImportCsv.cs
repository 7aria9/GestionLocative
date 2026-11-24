using System.Globalization;
using GestionLocative.Core.Donnees;
using Microsoft.Data.Sqlite;

namespace GestionLocative.Core.Import;

/// <summary>
/// Import des clients ou des locations depuis un fichier CSV (migration depuis l'ancien programme WinDev, Excel...).
/// <see cref="Analyser"/> prépare l'aperçu sans rien écrire ; <see cref="Importer"/> refait la même analyse
/// et enregistre les lignes correctes en une seule transaction (tout ou rien).
/// Réimporter le même fichier ne crée pas de doublons : clients reconnus par leur email, locations par leur
/// n° de réservation ou par client + dates.
/// </summary>
public static class ImportCsv
{
    /// <summary>Aperçu : ce qui serait importé, ignoré ou rejeté, sans modifier la base.</summary>
    public static RapportImport Analyser(BaseDeDonnees baseDeDonnees, TypeImport type, TableauCsv tableau)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        return Traiter(connexion, null, type, tableau);
    }

    /// <summary>Enregistre les lignes « à importer » et retourne le bilan.</summary>
    public static RapportImport Importer(BaseDeDonnees baseDeDonnees, TypeImport type, TableauCsv tableau)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var transaction = connexion.BeginTransaction();
        var rapport = Traiter(connexion, transaction, type, tableau);
        if (rapport.ErreurFichier is not null) return rapport;
        transaction.Commit();
        return rapport;
    }

    private static RapportImport Traiter(SqliteConnection connexion, SqliteTransaction? transaction, TypeImport type, TableauCsv tableau) =>
        type == TypeImport.Clients
            ? ImportCsvClients.Traiter(connexion, transaction, tableau)
            : ImportCsvLocations.Traiter(connexion, transaction, tableau);

    internal static RapportImport Rapport(TypeImport type, TableauCsv tableau, CorrespondanceColonnes colonnes,
        IReadOnlyList<Champ> champs, IReadOnlyList<Champ> obligatoires, string? alternative, List<LigneRapport> lignes, bool enregistre)
    {
        string? erreur = null;
        var manquants = obligatoires.Where(c => !colonnes.Contient(c)).Select(c => c.Libelle).ToList();
        if (manquants.Count > 0)
            erreur = $"Colonne{(manquants.Count > 1 ? "s" : "")} introuvable{(manquants.Count > 1 ? "s" : "")} : {string.Join(", ", manquants)}{alternative}. " +
                     $"Colonnes du fichier : {string.Join(", ", tableau.Entetes)}.";
        return new RapportImport
        {
            Type = type,
            Tableau = tableau,
            ColonnesReconnues = colonnes.Description(champs).ToList(),
            ColonnesIgnorees = colonnes.Ignorees,
            ErreurFichier = erreur,
            Lignes = erreur is null ? lignes : [],
            Enregistre = enregistre && erreur is null,
        };
    }

    /// <summary>Ligne plus longue que l'en-tête : une valeur contient sans doute le séparateur sans guillemets.</summary>
    internal static string? ErreurValeursEnTrop(LigneCsv ligne, TableauCsv tableau) =>
        ligne.ValeursEnTrop == 0 ? null
            : $"la ligne a plus de valeurs que de colonnes (un « {tableau.Separateur} » dans une valeur non entourée de guillemets ?).";

    internal static SqliteCommand Commande(SqliteConnection connexion, SqliteTransaction? transaction, string sql)
    {
        var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        return commande;
    }

    /// <summary>Ancien numéro de client → idclient dans la base.</summary>
    internal static Dictionary<string, long> LireOrigines(SqliteConnection connexion, SqliteTransaction? transaction)
    {
        var origines = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var commande = Commande(connexion, transaction, "SELECT id_origine, idclient FROM clients_origine;");
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read()) origines[lecteur.GetString(0)] = lecteur.GetInt64(1);
        return origines;
    }

    /// <summary>Numéros comme « 0042 » et « 42 » désignent le même client (WinDev complète parfois avec des zéros).</summary>
    internal static string SimplifierOrigine(string id)
    {
        var t = id.Trim();
        if (t.Length > 1 && t.All(char.IsAsciiDigit)) t = t.TrimStart('0') is { Length: > 0 } sansZeros ? sansZeros : "0";
        return t;
    }
}

internal static class ImportCsvClients
{
    public static readonly Champ IdOrigine = new("N° client d'origine",
        ["idclient", "numclient", "numeroclient", "nclient", "noclient", "codeclient", "clientid", "idorigine", "id",
         "refclient", "referenceclient", "identifiant", "numero", "num"], []);
    public static readonly Champ Nom = new("Nom", ["nom", "nomclient", "name", "lastname", "nomdefamille", "nomfamille"], []);
    public static readonly Champ Prenom = new("Prénom", ["prenom", "prenomclient", "firstname", "prenoms"], ["prenom"]);
    public static readonly Champ Email = new("Email", Synonymes.Email, Synonymes.DebutsEmail);
    public static readonly Champ Adresse = new("Adresse",
        ["adresse", "adresse1", "adresse2", "adresse3", "address", "rue", "adressepostale", "adr", "complementadresse", "complement"],
        ["adresse"]);
    public static readonly Champ CodePostal = new("Code postal", ["codepostal", "cp", "zip", "zipcode", "postal", "codepost"], ["codepostal"]);
    public static readonly Champ Ville = new("Ville", ["ville", "city", "commune", "localite"], ["ville"]);
    public static readonly Champ Telephone = new("Téléphone",
        ["telephone", "tel", "phone", "portable", "mobile", "gsm", "numtel", "numerotelephone", "ntel", "notel"],
        ["tel", "portable", "mobile", "gsm", "phone"]);
    public static readonly Champ Plaque = new("Plaque d'immatriculation",
        ["plaque", "immatriculation", "plaqueimmatriculation", "plaquedimmatriculation", "immat", "vehicule", "nimmatriculation"],
        ["plaque", "immat"]);

    // Email avant Adresse : « Adresse mail » doit aller dans l'email.
    public static readonly Champ[] Champs = [IdOrigine, Nom, Prenom, Email, Adresse, CodePostal, Ville, Telephone, Plaque];

    public static RapportImport Traiter(SqliteConnection connexion, SqliteTransaction? transaction, TableauCsv tableau)
    {
        var colonnes = CorrespondanceColonnes.Etablir(tableau.Entetes, Champs);
        var enregistrer = transaction is not null;
        var lignes = new List<LigneRapport>();
        if (!colonnes.Contient(Nom) || !colonnes.Contient(Email))
            return ImportCsv.Rapport(TypeImport.Clients, tableau, colonnes, Champs, [Nom, Email], null, lignes, enregistrer);

        // Clients existants, par email : (idclient, « NOM Prénom »).
        var existants = new Dictionary<string, (long Id, string Nom)>(StringComparer.OrdinalIgnoreCase);
        using (var commande = ImportCsv.Commande(connexion, transaction, "SELECT idclient, email, nom, prenom FROM clients;"))
        using (var lecteur = commande.ExecuteReader())
            while (lecteur.Read()) existants[lecteur.GetString(1)] = (lecteur.GetInt64(0), $"{lecteur.GetString(2)} {lecteur.GetString(3)}");
        var origines = ImportCsv.LireOrigines(connexion, transaction);
        var vusDansLeFichier = new Dictionary<string, (int Ligne, long Id)>(StringComparer.OrdinalIgnoreCase);

        foreach (var ligne in tableau.Lignes)
        {
            var client = new Client
            {
                Nom = colonnes.Valeur(ligne, Nom) ?? "",
                Prenom = colonnes.Valeur(ligne, Prenom) ?? "",
                Adresse = AdresseComplete(colonnes, ligne),
                CodePostal = colonnes.Valeur(ligne, CodePostal),
                Ville = colonnes.Valeur(ligne, Ville),
                Telephone = colonnes.Valeur(ligne, Telephone) ?? "",
                Email = colonnes.Valeur(ligne, Email) ?? "",
                PlaqueImmatriculation = colonnes.Valeur(ligne, Plaque),
            };
            var idOrigine = colonnes.Valeur(ligne, IdOrigine) is { } id ? ImportCsv.SimplifierOrigine(id) : null;
            client.Normaliser();
            var resume = string.Join(" · ", new[] { client.NomComplet, client.Email }.Where(t => t.Length > 0));
            if (resume.Length == 0) resume = "(vide)";
            if (idOrigine is not null) resume = $"n° {idOrigine} · {resume}";

            if (ImportCsv.ErreurValeursEnTrop(ligne, tableau) is { } enTrop)
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.Rejetee, resume, Majuscule(enTrop)));
                continue;
            }

            // Dédoublonnage par email : base d'abord, puis lignes précédentes du fichier.
            var emailValide = MiseEnForme.Email(client.Email) is not null;
            if (emailValide && existants.TryGetValue(client.Email, out var existant))
            {
                if (enregistrer) AssocierOrigine(connexion, transaction, origines, idOrigine, existant.Id);
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.DejaPresente, resume,
                    $"Email déjà utilisé dans la base par {existant.Nom.Trim()} : fiche existante conservée telle quelle."));
                continue;
            }
            if (emailValide && vusDansLeFichier.TryGetValue(client.Email, out var precedent))
            {
                if (enregistrer) AssocierOrigine(connexion, transaction, origines, idOrigine, precedent.Id);
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.DejaPresente, resume,
                    $"Même email que la ligne {precedent.Ligne} : un seul client est créé."));
                continue;
            }

            var erreurs = client.Valider();
            if (erreurs.Count > 0)
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.Rejetee, resume, string.Join(" ", erreurs)));
                continue;
            }

            if (enregistrer)
            {
                ClientRepository.Inserer(connexion, transaction, client);
                AssocierOrigine(connexion, transaction, origines, idOrigine, client.IdClient);
            }
            vusDansLeFichier[client.Email] = (ligne.NumeroLigne, client.IdClient);
            lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.AImporter, resume, ""));
        }
        return ImportCsv.Rapport(TypeImport.Clients, tableau, colonnes, Champs, [Nom, Email], null, lignes, enregistrer);
    }

    /// <summary>Plusieurs colonnes d'adresse (« Adresse 1 », « Adresse 2 ») : mises bout à bout.</summary>
    private static string? AdresseComplete(CorrespondanceColonnes colonnes, LigneCsv ligne)
    {
        var morceaux = new List<string>();
        for (var i = 0; i < colonnes.Entetes.Count; i++)
        {
            var nom = CorrespondanceColonnes.Simplifier(colonnes.Entetes[i]);
            if ((Adresse.Synonymes.Contains(nom) || Adresse.Debuts.Any(nom.StartsWith)) && !Email.Synonymes.Contains(nom)
                && !Email.Debuts.Any(nom.StartsWith) && ligne.Valeurs[i].Trim() is { Length: > 0 } valeur)
                morceaux.Add(valeur);
        }
        return morceaux.Count == 0 ? null : string.Join(", ", morceaux);
    }

    /// <summary>Retient l'ancien numéro du client, pour rattacher ensuite ses locations (le premier rattachement est conservé).</summary>
    private static void AssocierOrigine(SqliteConnection connexion, SqliteTransaction? transaction,
        Dictionary<string, long> origines, string? idOrigine, long idClient)
    {
        if (idOrigine is null || idClient <= 0 || origines.ContainsKey(idOrigine)) return;
        using var commande = ImportCsv.Commande(connexion, transaction,
            "INSERT OR IGNORE INTO clients_origine (id_origine, idclient) VALUES ($origine, $idclient);");
        commande.Parameters.AddWithValue("$origine", idOrigine);
        commande.Parameters.AddWithValue("$idclient", idClient);
        commande.ExecuteNonQuery();
        origines[idOrigine] = idClient;
    }

    internal static string Majuscule(string texte) => texte.Length == 0 ? texte : char.ToUpper(texte[0]) + texte[1..];
}

internal static class ImportCsvLocations
{
    public static readonly Champ IdClient = new("N° client d'origine",
        ["idclient", "numclient", "numeroclient", "nclient", "noclient", "codeclient", "clientid", "refclient", "referenceclient"],
        ["idclient", "numclient", "numeroclient", "codeclient"]);
    public static readonly Champ EmailClient = new("Email du client", Synonymes.Email, Synonymes.DebutsEmail);
    public static readonly Champ HeureDebut = new("Heure d'arrivée",
        ["heuredebut", "heurearrivee", "heuredarrivee", "heureentree"], ["heuredebut", "heurearrivee", "heuredarrivee"]);
    public static readonly Champ HeureFin = new("Heure de départ",
        ["heurefin", "heuredepart", "heurededepart", "heuresortie"], ["heurefin", "heuredepart", "heurededepart"]);
    public static readonly Champ DateDebut = new("Arrivée",
        ["datedebut", "debut", "arrivee", "datearrivee", "datedarrivee", "du", "dateentree", "entree", "checkin", "startdate", "datedebutlocation"],
        ["datedebut", "datearrivee", "datedarrivee", "arrivee", "debut"]);
    public static readonly Champ DateFin = new("Départ",
        ["datefin", "fin", "depart", "datedepart", "datededepart", "au", "datesortie", "sortie", "checkout", "enddate", "datefinlocation"],
        ["datefin", "datedepart", "datededepart", "depart", "fin"]);
    public static readonly Champ Tarif = new("Tarif TTC",
        ["tarif", "tarifttc", "prix", "prixttc", "montant", "montantttc", "total", "totalttc", "ttc", "loyer", "prixtotal", "montanttotal"],
        ["tarifttc", "prixttc", "montantttc", "totalttc"]);
    public static readonly Champ Adultes = new("Adultes",
        ["adultes", "adulte", "nbadultes", "nbadulte", "nombreadultes", "nombredadultes", "nbreadultes", "nombreadulte"],
        ["nbadulte", "nombreadulte", "nombredadulte", "nbreadulte", "adulte"]);
    public static readonly Champ Enfants = new("Enfants",
        ["enfants", "enfant", "nbenfants", "nbenfant", "nombreenfants", "nombredenfants", "nbreenfants", "nombreenfant"],
        ["nbenfant", "nombreenfant", "nombredenfant", "nbreenfant", "enfant"]);
    public static readonly Champ Bebes = new("Bébés",
        ["bebes", "bebe", "nbbebes", "nbbebe", "nombrebebes", "nombredebebes", "nbrebebes", "nombrebebe"],
        ["nbbebe", "nombrebebe", "nombredebebe", "nbrebebe", "bebe"]);
    public static readonly Champ Airbnb = new("Airbnb",
        ["airbnb", "origine", "source", "plateforme", "canal", "provenance", "reserveparairbnb"], ["airbnb"]);
    public static readonly Champ NumeroReservation = new("N° de réservation",
        ["numeroreservation", "numerodereservation", "nreservation", "noreservation", "numreservation", "reservation",
         "refreservation", "referencereservation", "codereservation", "confirmationcode", "codeconfirmation", "reference", "ref"],
        ["numeroreservation", "numerodereservation", "nreservation", "noreservation", "numreservation", "codereservation"]);

    // Heures avant dates : « Heure d'arrivée » ne doit pas être prise pour la date.
    public static readonly Champ[] Champs =
        [IdClient, EmailClient, HeureDebut, HeureFin, DateDebut, DateFin, Tarif, Adultes, Enfants, Bebes, Airbnb, NumeroReservation];

    private static readonly Champ[] Obligatoires = [DateDebut, DateFin, Tarif];

    public static RapportImport Traiter(SqliteConnection connexion, SqliteTransaction? transaction, TableauCsv tableau)
    {
        var colonnes = CorrespondanceColonnes.Etablir(tableau.Entetes, Champs);
        var enregistrer = transaction is not null;
        var lignes = new List<LigneRapport>();
        var obligatoires = Obligatoires.ToList();
        if (!colonnes.Contient(IdClient) && !colonnes.Contient(EmailClient)) obligatoires.Add(IdClient);
        if (obligatoires.Any(c => !colonnes.Contient(c)))
            return ImportCsv.Rapport(TypeImport.Locations, tableau, colonnes, Champs, obligatoires,
                colonnes.Contient(IdClient) || colonnes.Contient(EmailClient) ? null : " (ou l'email du client)", lignes, enregistrer);

        var clientsParEmail = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var nomsClients = new Dictionary<long, string>();
        using (var commande = ImportCsv.Commande(connexion, transaction, "SELECT idclient, email, nom, prenom FROM clients;"))
        using (var lecteur = commande.ExecuteReader())
            while (lecteur.Read())
            {
                clientsParEmail[lecteur.GetString(1)] = lecteur.GetInt64(0);
                nomsClients[lecteur.GetInt64(0)] = $"{lecteur.GetString(2)} {lecteur.GetString(3)}";
            }
        var origines = ImportCsv.LireOrigines(connexion, transaction);

        // Locations existantes : n° de réservation, et client + dates.
        var reservations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // (jours seulement : une location importée sans heure ne double pas celle saisie à 15h30).
        var sejours = new HashSet<(long, string, string)>();
        using (var commande = ImportCsv.Commande(connexion, transaction, "SELECT idclient, date_debut, date_fin, numero_reservation FROM locations;"))
        using (var lecteur = commande.ExecuteReader())
            while (lecteur.Read())
            {
                sejours.Add((lecteur.GetInt64(0), lecteur.GetString(1)[..10], lecteur.GetString(2)[..10]));
                if (!lecteur.IsDBNull(3)) reservations[lecteur.GetString(3).Trim()] = "dans la base";
            }

        foreach (var ligne in tableau.Lignes)
        {
            var erreurs = new List<string>();
            var idOrigine = colonnes.Valeur(ligne, IdClient) is { } id ? ImportCsv.SimplifierOrigine(id) : null;
            var email = colonnes.Valeur(ligne, EmailClient);
            var debutTexte = colonnes.Valeur(ligne, DateDebut);
            var finTexte = colonnes.Valeur(ligne, DateFin);

            var location = new Location
            {
                NumeroReservation = colonnes.Valeur(ligne, NumeroReservation),
                Airbnb = ValeursCsv.LireAirbnb(colonnes.Valeur(ligne, Airbnb)),
            };

            // Client : par l'ancien numéro, sinon par l'email.
            long idClient = 0;
            if (idOrigine is not null && origines.TryGetValue(idOrigine, out var parOrigine)) idClient = parOrigine;
            else if (email is not null && clientsParEmail.TryGetValue(email.Trim(), out var parEmail)) idClient = parEmail;
            else if (idOrigine is null && email is null) erreurs.Add("Client non renseigné (n° client ou email).");
            else
                erreurs.Add($"Client introuvable ({string.Join(", ", new[] { idOrigine is null ? null : $"n° {idOrigine}", email }.OfType<string>())}) : " +
                            "importez d'abord le fichier des clients (ligne client rejetée ?).");
            location.IdClient = idClient;

            location.DateDebut = LireDateHeure(debutTexte, colonnes.Valeur(ligne, HeureDebut), Location.HeureDebutParDefaut, "d'arrivée", erreurs);
            location.DateFin = LireDateHeure(finTexte, colonnes.Valeur(ligne, HeureFin), Location.HeureFinParDefaut, "de départ", erreurs);

            var tarifTexte = colonnes.Valeur(ligne, Tarif);
            if (tarifTexte is null) erreurs.Add("Le tarif TTC est obligatoire.");
            else if (ValeursCsv.LireMontant(tarifTexte) is { } tarif) location.TarifTtc = tarif;
            else erreurs.Add($"Tarif TTC « {tarifTexte} » non reconnu (ex. 450,50).");

            location.NombreAdultes = Nombre(colonnes.Valeur(ligne, Adultes), "d'adultes", erreurs);
            location.NombreEnfants = Nombre(colonnes.Valeur(ligne, Enfants), "d'enfants", erreurs);
            location.NombreBebes = Nombre(colonnes.Valeur(ligne, Bebes), "de bébés", erreurs);

            var resume = Resume(location, debutTexte, finTexte, idClient > 0 && nomsClients.TryGetValue(idClient, out var nom) ? nom : email ?? (idOrigine is null ? null : $"client n° {idOrigine}"));

            if (ImportCsv.ErreurValeursEnTrop(ligne, tableau) is { } enTrop)
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.Rejetee, resume, ImportCsvClients.Majuscule(enTrop)));
                continue;
            }
            if (erreurs.Count == 0)
                erreurs.AddRange(location.Valider().Where(e => !erreurs.Contains(e)));
            if (erreurs.Count > 0)
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.Rejetee, resume, string.Join(" ", erreurs.Distinct())));
                continue;
            }

            // Doublons : même n° de réservation, ou même client aux mêmes dates.
            var numero = location.NumeroReservation?.Trim();
            if (!string.IsNullOrEmpty(numero) && reservations.TryGetValue(numero, out var ou))
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.DejaPresente, resume, $"N° de réservation {numero} déjà présent {ou}."));
                continue;
            }
            var sejour = (location.IdClient, location.DateDebut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), location.DateFin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!sejours.Add(sejour))
            {
                lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.DejaPresente, resume, "Location du même client aux mêmes dates déjà présente."));
                continue;
            }
            if (!string.IsNullOrEmpty(numero)) reservations[numero] = $"à la ligne {ligne.NumeroLigne}";

            if (enregistrer) LocationRepository.Inserer(connexion, transaction, location);
            lignes.Add(new LigneRapport(ligne.NumeroLigne, StatutLigne.AImporter, resume, ""));
        }
        return ImportCsv.Rapport(TypeImport.Locations, tableau, colonnes, Champs, obligatoires, null, lignes, enregistrer);
    }

    /// <summary>Date du fichier ; heure prise dans la colonne d'heure, sinon dans la date, sinon l'heure par défaut (16h / 10h).</summary>
    private static DateTime LireDateHeure(string? date, string? heure, TimeOnly parDefaut, string quoi, List<string> erreurs)
    {
        if (date is null)
        {
            erreurs.Add($"La date {quoi} est obligatoire.");
            return default;
        }
        if (!ValeursCsv.LireDate(date, out var jour, out var heureDansDate))
        {
            erreurs.Add($"Date {quoi} « {date} » non reconnue (ex. 05/12/2026).");
            return default;
        }
        var h = heureDansDate ?? parDefaut;
        if (heure is not null)
        {
            if (ValeursCsv.LireHeure(heure, out var lue)) h = lue;
            else erreurs.Add($"Heure {quoi} « {heure} » non reconnue (ex. 16:00).");
        }
        return jour.ToDateTime(h);
    }

    private static int Nombre(string? texte, string quoi, List<string> erreurs)
    {
        if (ValeursCsv.LireNombre(texte) is { } n) return n;
        erreurs.Add($"Nombre {quoi} « {texte} » non reconnu.");
        return 0;
    }

    private static string Resume(Location location, string? debut, string? fin, string? client)
    {
        var dates = location.DateDebut != default && location.DateFin != default
            ? $"du {location.DateDebut:dd/MM/yyyy} au {location.DateFin:dd/MM/yyyy}"
            : $"du {debut ?? "?"} au {fin ?? "?"}";
        var morceaux = new List<string> { dates };
        if (client is not null) morceaux.Add(client.Trim());
        if (location.TarifTtc > 0) morceaux.Add(location.TarifTtc.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
        return string.Join(" · ", morceaux);
    }
}

internal static class Synonymes
{
    public static readonly string[] Email =
        ["email", "mail", "courriel", "emailclient", "mailclient", "adressemail", "adresseemail", "adresseelectronique", "emailadresse"];

    public static readonly string[] DebutsEmail = ["email", "mail", "courriel", "adressemail", "adresseemail", "adresseelectronique"];
}
