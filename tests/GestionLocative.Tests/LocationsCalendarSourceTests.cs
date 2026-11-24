using GestionLocative.Core.Calendar;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

public sealed class LocationsCalendarSourceTests : IDisposable
{
    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public void Affiche_arrivee_jours_intermediaires_et_depart()
    {
        var client = BaseTemporaire.NouveauClient();
        new ClientRepository(_base.Base).Ajouter(client);
        new LocationRepository(_base.Base).Ajouter(
            Location.Nouvelle(new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 2), client.IdClient));
        var source = new LocationsCalendarSource(() => _base.Base);

        var entrees = source.GetEntries(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 31));

        Assert.Equal(
            [
                new DateOnly(2026, 11, 30),
                new DateOnly(2026, 12, 1),
                new DateOnly(2026, 12, 2),
            ],
            entrees.Select(e => e.Date));
        Assert.Equal("DUPONT Jean", entrees[0].Libelle);
        Assert.Equal("Départ DUPONT Jean", entrees[2].Libelle);

        var avecArrivee = source.GetEntries(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        Assert.Equal("Arrivée DUPONT Jean", avecArrivee[0].Libelle);
    }
}
