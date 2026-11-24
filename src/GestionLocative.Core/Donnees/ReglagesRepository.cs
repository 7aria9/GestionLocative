using System.Globalization;

namespace GestionLocative.Core.Donnees;

/// <summary>Réglages enregistrés dans la base, donc communs à tous les postes (taux de TVA, téléphones du prestataire).</summary>
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

    /// <summary>Numéro principal du prestataire (ménage, linge...), au format international ; null si non renseigné.</summary>
    public string? ObtenirTelephonePrestatairePrincipal() => Lire(CleTelephonePrincipal);

    /// <summary>Numéro secondaire du prestataire ; null si non renseigné.</summary>
    public string? ObtenirTelephonePrestataireSecondaire() => Lire(CleTelephoneSecondaire);

    /// <exception cref="DonneesInvalidesException">Numéro non reconnu.</exception>
    public void DefinirTelephonePrestatairePrincipal(string? telephone) =>
        EcrireTelephone(CleTelephonePrincipal, telephone, "principal");

    /// <exception cref="DonneesInvalidesException">Numéro non reconnu.</exception>
    public void DefinirTelephonePrestataireSecondaire(string? telephone) =>
        EcrireTelephone(CleTelephoneSecondaire, telephone, "secondaire");

    private const string CleTelephonePrincipal = "telephone_prestataire_principal";
    private const string CleTelephoneSecondaire = "telephone_prestataire_secondaire";

    /// <summary>Vide : réglage effacé ; sinon enregistré au format international (+33612345678).</summary>
    private void EcrireTelephone(string cle, string? telephone, string quoi)
    {
        if (string.IsNullOrWhiteSpace(telephone))
        {
            Effacer(cle);
            return;
        }
        var numero = MiseEnForme.Telephone(telephone)
            ?? throw new DonneesInvalidesException($"Le numéro {quoi} du prestataire n'est pas valide (ex. 06 12 34 56 78).");
        Ecrire(cle, numero);
    }

    private void Effacer(string cle)
    {
        using var connexion = baseDeDonnees.Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM reglages WHERE cle = $cle;";
        commande.Parameters.AddWithValue("$cle", cle);
        commande.ExecuteNonQuery();
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
