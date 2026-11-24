using Microsoft.Data.Sqlite;

namespace GestionLocative.Core.Donnees;

/// <summary>
/// Fichier SQLite de l'application (local ou sur un partage réseau, ex. <c>\\SERVEUR\Partage\gestionlocative.db</c>).
/// </summary>
/// <remarks>
/// Réglages prudents pour un fichier partagé sur le réseau local :
/// <list type="bullet">
/// <item>journal « DELETE » (le mode WAL ne fonctionne pas sur un partage réseau) ;</item>
/// <item>pas de pool de connexions : chaque opération ouvre puis referme le fichier, rien ne reste verrouillé ;</item>
/// <item>attente jusqu'à 10 s si un autre poste est en train d'écrire, au lieu d'échouer tout de suite ;</item>
/// <item>synchronisation complète des écritures sur le disque, contraintes de clés étrangères activées.</item>
/// </list>
/// </remarks>
public sealed class BaseDeDonnees
{
    /// <summary>Version du schéma créé par ce programme (stockée dans <c>PRAGMA user_version</c>).</summary>
    public const int VersionSchema = 2;

    private const int AttenteVerrouSecondes = 10;

    private readonly string _chaineConnexion;

    public BaseDeDonnees(string cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
            throw new ArgumentException("Le chemin de la base de données est vide.", nameof(cheminFichier));

        CheminFichier = cheminFichier;
        _chaineConnexion = new SqliteConnectionStringBuilder
        {
            DataSource = cheminFichier,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = true,
            DefaultTimeout = AttenteVerrouSecondes,
        }.ToString();
    }

    public string CheminFichier { get; }

    /// <summary>
    /// Crée le fichier et les tables s'ils n'existent pas, puis met le schéma à jour si besoin.
    /// À appeler au démarrage et après un changement de chemin.
    /// </summary>
    public void Initialiser()
    {
        var dossier = Path.GetDirectoryName(Path.GetFullPath(CheminFichier));
        if (!string.IsNullOrEmpty(dossier))
        {
            try
            {
                Directory.CreateDirectory(dossier);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Le dossier « {dossier} » est introuvable ou inaccessible (partage réseau déconnecté ?).", ex);
            }
        }

        using var connexion = Ouvrir();

        // Mode de journal persistant : une fois réglé, il s'applique à tous les postes.
        Executer(connexion, "PRAGMA journal_mode = DELETE;");

        var version = Convert.ToInt32(new SqliteCommand("PRAGMA user_version;", connexion).ExecuteScalar());
        if (version > VersionSchema)
            throw new InvalidOperationException(
                $"Cette base a été créée par une version plus récente du programme (schéma {version}). Mettez le programme à jour.");

        if (version < 1)
        {
            using var transaction = connexion.BeginTransaction();
            Executer(connexion, SchemaV1, transaction);
            Executer(connexion, "PRAGMA user_version = 1;", transaction);
            transaction.Commit();
        }

        if (version < 2)
        {
            using var transaction = connexion.BeginTransaction();
            Executer(connexion, SchemaV2, transaction);
            Executer(connexion, "PRAGMA user_version = 2;", transaction);
            transaction.Commit();
        }
    }

    /// <summary>Ouvre une connexion configurée. À fermer rapidement (using).</summary>
    public SqliteConnection Ouvrir()
    {
        var connexion = new SqliteConnection(_chaineConnexion);
        connexion.Open();
        Executer(connexion, "PRAGMA synchronous = FULL;");
        return connexion;
    }

    /// <summary>Référence vers un client inexistant, ou suppression d'un client qui a encore des locations.</summary>
    internal static bool EstErreurCleEtrangere(SqliteException ex) =>
        ex.SqliteErrorCode == 19 // SQLITE_CONSTRAINT (le code détaillé varie selon ON DELETE RESTRICT ou non)
        && ex.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);

    internal static void Executer(SqliteConnection connexion, string sql, SqliteTransaction? transaction = null)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        commande.Transaction = transaction;
        commande.ExecuteNonQuery();
    }

    /// <summary>
    /// Dates : texte « AAAA-MM-JJ HH:MM » en heure locale (triable et lisible).
    /// Tarif : entier en centimes, pour éviter les erreurs d'arrondi.
    /// </summary>
    private const string SchemaV1 = """
        CREATE TABLE clients (
            idclient                INTEGER PRIMARY KEY AUTOINCREMENT,
            nom                     TEXT NOT NULL CHECK (trim(nom) <> ''),
            prenom                  TEXT NOT NULL CHECK (trim(prenom) <> ''),
            adresse                 TEXT,
            code_postal             TEXT,
            ville                   TEXT,
            telephone               TEXT NOT NULL CHECK (trim(telephone) <> ''),
            email                   TEXT NOT NULL COLLATE NOCASE CHECK (trim(email) <> '') UNIQUE,
            plaque_immatriculation  TEXT
        );

        CREATE TABLE locations (
            idlocation              INTEGER PRIMARY KEY AUTOINCREMENT,
            date_debut              TEXT NOT NULL,
            date_fin                TEXT NOT NULL,
            idclient                INTEGER NOT NULL REFERENCES clients (idclient) ON DELETE RESTRICT,
            tarif_ttc_centimes      INTEGER NOT NULL CHECK (tarif_ttc_centimes >= 0),
            nombre_adultes          INTEGER NOT NULL DEFAULT 0 CHECK (nombre_adultes >= 0),
            nombre_enfants          INTEGER NOT NULL DEFAULT 0 CHECK (nombre_enfants >= 0),
            nombre_bebes            INTEGER NOT NULL DEFAULT 0 CHECK (nombre_bebes >= 0),
            airbnb                  INTEGER NOT NULL DEFAULT 0 CHECK (airbnb IN (0, 1)),
            numero_reservation      TEXT,
            CHECK (date_fin > date_debut)
        );

        CREATE INDEX ix_locations_dates ON locations (date_debut, date_fin);
        CREATE INDEX ix_locations_client ON locations (idclient);
        CREATE INDEX ix_clients_nom ON clients (nom, prenom);
        """;

    /// <summary>V2 : réglages communs à tous les postes (taux de TVA...), valeurs en texte.</summary>
    private const string SchemaV2 = """
        CREATE TABLE reglages (
            cle     TEXT PRIMARY KEY,
            valeur  TEXT NOT NULL
        );

        INSERT INTO reglages (cle, valeur) VALUES ('taux_tva', '10');
        """;
}
