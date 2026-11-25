using GestionLocative.Core.Calendar;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

public sealed class LocationsCalendarSourceTests : IDisposable
{
    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private Location Ajouter(Client client, DateOnly arrivee, DateOnly depart, decimal ttc = 450m)
    {
        var location = Location.Nouvelle(arrivee, depart, client.IdClient);
        location.TarifTtc = ttc;
        location.NombreAdultes = 2;
        location.NombreEnfants = 1;
        location.NombreBebes = 1;
        new LocationRepository(_base.Base).Ajouter(location);
        return location;
    }

    private Client NouveauClient(string email = "jean.dupont@example.com")
    {
        var client = BaseTemporaire.NouveauClient(email);
        new ClientRepository(_base.Base).Ajouter(client);
        return client;
    }

    [Fact]
    public void Affiche_arrivee_jours_intermediaires_et_depart()
    {
        Ajouter(NouveauClient(), new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 2));
        var source = new LocationsCalendarSource(() => _base.Base);

        var entrees = source.GetEntries(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 31));

        Assert.Equal(
            [new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2)],
            entrees.Select(e => e.Date));
        Assert.Equal("DUPONT Jean", entrees[0].Libelle);
        Assert.Equal("Départ DUPONT Jean", entrees[2].Libelle);

        var avecArrivee = source.GetEntries(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        Assert.Equal("Arrivée DUPONT Jean", avecArrivee[0].Libelle);
    }

    [Fact]
    public void Le_detail_montre_personnes_email_et_prix_ht_ttc_selon_le_taux_regle()
    {
        Ajouter(NouveauClient(), new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 8));
        var source = new LocationsCalendarSource(() => _base.Base);

        var entrees = source.GetEntries(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));

        var arrivee = entrees[0];
        Assert.Equal("Arrivée DUPONT Jean", arrivee.Libelle);
        Assert.Equal("4 personnes (2/1/1)", arrivee.Details[0]);
        Assert.Equal("jean.dupont@example.com", arrivee.Details[1]);
        Assert.Equal("HT 409,09 € · TTC 450,00 €", arrivee.Details[2].Replace(' ', ' ').Replace(' ', ' '));
        Assert.Contains("2 adulte(s), 1 enfant(s), 1 bébé(s)", arrivee.Infobulle);
        Assert.DoesNotContain("(2/1/1)", arrivee.Infobulle);
        // Les jours suivants sont compacts (détail au survol), le lundi 7 répète le détail en début de ligne.
        Assert.Empty(entrees.Single(e => e.Date == new DateOnly(2026, 12, 6)).Details);
        Assert.NotEmpty(entrees.Single(e => e.Date == new DateOnly(2026, 12, 7)).Details);
        Assert.Empty(entrees.Single(e => e.Date == new DateOnly(2026, 12, 8)).Details);

        new ReglagesRepository(_base.Base).DefinirTauxTva(20m);
        Assert.StartsWith("HT 375,00 €", source.GetEntries(new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 5))[0].Details[2].Replace(' ', ' ').Replace(' ', ' '));
    }

    [Fact]
    public void Chaque_location_a_sa_couleur_et_la_garde_tous_les_jours()
    {
        var client = NouveauClient();
        var premiere = Ajouter(client, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5));
        var seconde = Ajouter(NouveauClient("autre@example.com"), new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 9));
        var source = new LocationsCalendarSource(() => _base.Base);

        var entrees = source.GetEntries(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));

        var couleurs1 = entrees.Where(e => e.IdLocation == premiere.IdLocation).Select(e => e.IndexCouleur).Distinct().ToList();
        var couleurs2 = entrees.Where(e => e.IdLocation == seconde.IdLocation).Select(e => e.IndexCouleur).Distinct().ToList();
        Assert.Single(couleurs1);
        Assert.Single(couleurs2);
        Assert.NotEqual(couleurs1[0], couleurs2[0]);
        Assert.All(entrees, e => Assert.InRange(e.IndexCouleur, 0, CalendarEntry.NombreCouleurs - 1));
    }
}
