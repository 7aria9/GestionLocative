using System.Globalization;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Recapitulatif;

/// <summary>Une location dans le récapitulatif annuel (textes déjà mis en forme pour l'affichage).</summary>
public sealed class LigneRecapitulatif
{
    internal static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    public LigneRecapitulatif(Location location, string client, decimal tauxTva)
    {
        Location = location;
        Client = client;
        Ht = location.TarifHt(tauxTva);
    }

    public Location Location { get; }
    public long IdLocation => Location.IdLocation;
    public string Client { get; }
    public decimal Ttc => Location.TarifTtc;
    public decimal Ht { get; }
    public int Nuits => Location.NombreNuits;
    public int Personnes => Location.NombrePersonnes;

    public string Arrivee => Location.DateDebut.ToString("dd/MM/yyyy", Francais);
    public string Depart => Location.DateFin.ToString("dd/MM/yyyy", Francais);
    public string HtAffiche => Montant(Ht);
    public string TtcAffiche => Montant(Ttc);

    /// <summary>« Airbnb » ou vide.</summary>
    public string Origine => Location.Airbnb ? "Airbnb" : "";

    /// <summary>Ex. « 2 adultes, 1 enfant, 1 bébé » ; « — » si aucun voyageur n'est renseigné.</summary>
    public string DetailPersonnes => DetailVoyageurs(Location);

    internal static string Montant(decimal montant) => montant.ToString("#,##0.00 €", Francais);

    internal static string DetailVoyageurs(Location location)
    {
        var parties = new List<string>();
        if (location.NombreAdultes > 0) parties.Add(Pluriel(location.NombreAdultes, "adulte"));
        if (location.NombreEnfants > 0) parties.Add(Pluriel(location.NombreEnfants, "enfant"));
        if (location.NombreBebes > 0) parties.Add(Pluriel(location.NombreBebes, "bébé"));
        return parties.Count == 0 ? "—" : string.Join(", ", parties);
    }

    internal static string Pluriel(int nombre, string mot) => $"{nombre} {mot}{(nombre > 1 ? "s" : "")}";
}
