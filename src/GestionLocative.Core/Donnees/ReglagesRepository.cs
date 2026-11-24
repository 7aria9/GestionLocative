using System.Globalization;

namespace GestionLocative.Core.Donnees;

/// <summary>Réglages enregistrés dans la base, donc communs à tous les postes (ex. taux de TVA).</summary>
public sealed class ReglagesRepository(BaseDeDonnees baseDeDonnees)
{
    /// <summary>Taux de TVA utilisé si la base n'en contient pas (hébergement : 10 %).</summary>
    public const decimal TauxTvaParDefaut = 10m;

    private const string CleTauxTva = "taux_tva";

    /// <summary>Taux de TVA en pourcentage (10 = 10 %).</summary>
    public decimal ObtenirTauxTva()
    {
        var valeur = Lire(CleTauxTva);
        return decimal.TryParse(valeur, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var taux)
            && taux is >= 0 and <= 100
            ? taux
            : TauxTvaParDefaut;
    }

    /// <exception cref="DonneesInvalidesException">Taux hors de 0 à 100 % ou plus de 2 décimales.</exception>
    public void DefinirTauxTva(decimal taux)
    {
        if (taux is < 0 or > 100) throw new DonneesInvalidesException("Le taux de TVA doit être compris entre 0 et 100 %.");
        if (decimal.Round(taux, 2) != taux) throw new DonneesInvalidesException("Le taux de TVA est limité à 2 décimales.");
        Ecrire(CleTauxTva, taux.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private string? Lire(string cle)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT valeur FROM reglages WHERE cle = $cle;";
        commande.Parameters.AddWithValue("$cle", cle);
        return commande.ExecuteScalar() as string;
    }

    private void Ecrire(string cle, string valeur)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO reglages (cle, valeur) VALUES ($cle, $valeur)
            ON CONFLICT (cle) DO UPDATE SET valeur = excluded.valeur;
            """;
        commande.Parameters.AddWithValue("$cle", cle);
        commande.Parameters.AddWithValue("$valeur", valeur);
        commande.ExecuteNonQuery();
    }
}
