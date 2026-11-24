using System.Globalization;

namespace GestionLocative.Core.Donnees;

/// <summary>Textes copiés dans le presse-papiers depuis le calendrier.</summary>
public static class TextesLocation
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>
    /// Message à envoyer à un prestataire (ménage, linge...) :
    /// <code>
    /// du 05/12/2026 au 08/12/2026
    /// DUPONT Jean
    /// 2 adultes, 1 enfant, 0 bébé
    /// +33 6 12 34 56 78
    /// </code>
    /// </summary>
    public static string MessagePrestataire(Location location, Client client) => string.Join(Environment.NewLine,
        $"du {location.DateDebut.ToString("dd/MM/yyyy", Francais)} au {location.DateFin.ToString("dd/MM/yyyy", Francais)}",
        client.NomComplet,
        Voyageurs(location),
        client.TelephoneAffiche);

    /// <summary>« 2 adultes, 1 enfant, 0 bébé ».</summary>
    public static string Voyageurs(Location location) =>
        $"{Compter(location.NombreAdultes, "adulte")}, {Compter(location.NombreEnfants, "enfant")}, {Compter(location.NombreBebes, "bébé")}";

    private static string Compter(int nombre, string mot) => nombre > 1 ? $"{nombre} {mot}s" : $"{nombre} {mot}";
}
