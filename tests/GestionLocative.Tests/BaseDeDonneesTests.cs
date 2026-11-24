using GestionLocative.Core.Donnees;
using Microsoft.Data.Sqlite;

namespace GestionLocative.Tests;

public sealed class BaseDeDonneesTests : IDisposable
{
    private readonly BaseTemporaire _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Initialiser_cree_le_fichier_et_les_tables()
    {
        Assert.True(File.Exists(_temp.Base.CheminFichier));
        using var connexion = _temp.Base.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT group_concat(name, ',') FROM (SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);";
        Assert.Equal("clients,locations,reglages", commande.ExecuteScalar());
    }

    [Fact]
    public void Une_base_en_version_1_est_mise_a_jour_sans_perdre_les_donnees()
    {
        new ClientRepository(_temp.Base).Ajouter(BaseTemporaire.NouveauClient());
        using (var connexion = _temp.Base.Ouvrir())
        {
            using var commande = connexion.CreateCommand();
            commande.CommandText = "DROP TABLE reglages; PRAGMA user_version = 1;";
            commande.ExecuteNonQuery();
        }

        _temp.Base.Initialiser();

        Assert.Single(new ClientRepository(_temp.Base).Lister());
        Assert.Equal(10m, new ReglagesRepository(_temp.Base).ObtenirTauxTva());
        using var verification = _temp.Base.Ouvrir();
        Assert.Equal(2L, Lire(verification, "PRAGMA user_version;"));
    }

    [Fact]
    public void Initialiser_deux_fois_ne_perd_pas_les_donnees()
    {
        new ClientRepository(_temp.Base).Ajouter(BaseTemporaire.NouveauClient());
        _temp.Base.Initialiser();
        Assert.Single(new ClientRepository(_temp.Base).Lister());
    }

    [Fact]
    public void Initialiser_cree_le_dossier_manquant()
    {
        var chemin = Path.Combine(_temp.Dossier, "sous", "dossier", "base.db");
        new BaseDeDonnees(chemin).Initialiser();
        Assert.True(File.Exists(chemin));
    }

    [Fact]
    public void Reglages_prudents_pour_le_reseau()
    {
        using var connexion = _temp.Base.Ouvrir();
        Assert.Equal("delete", Lire(connexion, "PRAGMA journal_mode;"));
        Assert.Equal(1L, Lire(connexion, "PRAGMA foreign_keys;"));
        Assert.Equal(2L, Lire(connexion, "PRAGMA synchronous;")); // FULL
        Assert.Equal(BaseDeDonnees.VersionSchema, Convert.ToInt32(Lire(connexion, "PRAGMA user_version;")));
    }

    [Fact]
    public void Refuse_une_base_plus_recente_que_le_programme()
    {
        using (var connexion = _temp.Base.Ouvrir())
        {
            using var commande = connexion.CreateCommand();
            commande.CommandText = $"PRAGMA user_version = {BaseDeDonnees.VersionSchema + 1};";
            commande.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(_temp.Base.Initialiser);
    }

    [Fact]
    public void Le_fichier_n_est_pas_garde_ouvert_apres_une_operation()
    {
        new ClientRepository(_temp.Base).Ajouter(BaseTemporaire.NouveauClient());
        // Sans pool de connexions, le fichier peut être déplacé dès que l'opération est finie.
        File.Move(_temp.Base.CheminFichier, _temp.Base.CheminFichier + ".bak");
        Assert.True(File.Exists(_temp.Base.CheminFichier + ".bak"));
    }

    private static object? Lire(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        return commande.ExecuteScalar();
    }
}
