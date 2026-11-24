using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

public sealed class LocationRepositoryTests : IDisposable
{
    private readonly BaseTemporaire _temp = new();
    private readonly LocationRepository _locations;
    private readonly long _idClient;

    public LocationRepositoryTests()
    {
        _locations = new LocationRepository(_temp.Base);
        var client = BaseTemporaire.NouveauClient();
        new ClientRepository(_temp.Base).Ajouter(client);
        _idClient = client.IdClient;
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Nouvelle_location_arrive_a_16h_et_part_a_10h()
    {
        var location = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8));
        Assert.Equal(new DateTime(2026, 7, 1, 16, 0, 0), location.DateDebut);
        Assert.Equal(new DateTime(2026, 7, 8, 10, 0, 0), location.DateFin);
        Assert.Equal(7, location.NombreNuits);
    }

    [Fact]
    public void Ajouter_attribue_un_numero_automatique_et_relit_tous_les_champs()
    {
        var location = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8), _idClient);
        location.TarifTtc = 845.50m;
        location.NombreAdultes = 2;
        location.NombreEnfants = 1;
        location.NombreBebes = 1;
        location.Airbnb = true;
        location.NumeroReservation = "HMABC123";
        _locations.Ajouter(location);

        Assert.True(location.IdLocation > 0);
        var relue = _locations.ObtenirParId(location.IdLocation)!;
        Assert.Equal(new DateTime(2026, 7, 1, 16, 0, 0), relue.DateDebut);
        Assert.Equal(new DateTime(2026, 7, 8, 10, 0, 0), relue.DateFin);
        Assert.Equal(_idClient, relue.IdClient);
        Assert.Equal(845.50m, relue.TarifTtc);
        Assert.Equal(2, relue.NombreAdultes);
        Assert.Equal(1, relue.NombreEnfants);
        Assert.Equal(1, relue.NombreBebes);
        Assert.True(relue.Airbnb);
        Assert.Equal("HMABC123", relue.NumeroReservation);
    }

    [Fact]
    public void Les_heures_saisies_sont_conservees()
    {
        var location = new Location
        {
            DateDebut = new(2026, 7, 1, 18, 30, 0),
            DateFin = new(2026, 7, 3, 11, 0, 0),
            IdClient = _idClient,
            TarifTtc = 200,
        };
        _locations.Ajouter(location);
        var relue = _locations.ObtenirParId(location.IdLocation)!;
        Assert.Equal(new DateTime(2026, 7, 1, 18, 30, 0), relue.DateDebut);
        Assert.Equal(new DateTime(2026, 7, 3, 11, 0, 0), relue.DateFin);
        Assert.False(relue.Airbnb);
        Assert.Null(relue.NumeroReservation);
    }

    [Fact]
    public void Le_client_est_obligatoire_et_doit_exister()
    {
        var sansClient = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8));
        Assert.Contains("Le client est obligatoire.", Assert.Throws<DonneesInvalidesException>(() => _locations.Ajouter(sansClient)).Erreurs);

        var clientInconnu = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8), idClient: 9999);
        var ex = Assert.Throws<DonneesInvalidesException>(() => _locations.Ajouter(clientInconnu));
        Assert.Equal("Le client de cette location n'existe pas dans la base.", ex.Message);
    }

    [Fact]
    public void Les_dates_sont_obligatoires_et_dans_le_bon_ordre()
    {
        var sansDates = new Location { IdClient = _idClient };
        var erreurs = Assert.Throws<DonneesInvalidesException>(() => _locations.Ajouter(sansDates)).Erreurs;
        Assert.Contains("La date de début est obligatoire.", erreurs);
        Assert.Contains("La date de fin est obligatoire.", erreurs);

        var inversee = Location.Nouvelle(new(2026, 7, 8), new(2026, 7, 1), _idClient);
        Assert.Contains("La date de fin doit être après la date de début.",
            Assert.Throws<DonneesInvalidesException>(() => _locations.Ajouter(inversee)).Erreurs);
    }

    [Fact]
    public void Tarif_et_nombres_de_personnes_ne_peuvent_pas_etre_negatifs()
    {
        var location = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8), _idClient);
        location.TarifTtc = -1;
        location.NombreEnfants = -2;
        var erreurs = Assert.Throws<DonneesInvalidesException>(() => _locations.Ajouter(location)).Erreurs;
        Assert.Contains("Le tarif TTC ne peut pas être négatif.", erreurs);
        Assert.Contains("Le nombre de personnes ne peut pas être négatif.", erreurs);
    }

    [Fact]
    public void Modifier_et_supprimer()
    {
        var location = Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8), _idClient);
        _locations.Ajouter(location);

        location.TarifTtc = 990.99m;
        location.Airbnb = true;
        _locations.Modifier(location);
        var relue = _locations.ObtenirParId(location.IdLocation)!;
        Assert.Equal(990.99m, relue.TarifTtc);
        Assert.True(relue.Airbnb);

        _locations.Supprimer(location.IdLocation);
        Assert.Null(_locations.ObtenirParId(location.IdLocation));
    }

    [Fact]
    public void ListerEntre_retourne_les_locations_qui_touchent_la_periode()
    {
        Ajouter(new(2026, 6, 25), new(2026, 7, 1));   // part le 1er juillet : incluse
        Ajouter(new(2026, 7, 10), new(2026, 7, 17));  // pleinement en juillet
        Ajouter(new(2026, 7, 31), new(2026, 8, 3));   // arrive le 31 juillet : incluse
        Ajouter(new(2026, 6, 1), new(2026, 6, 30));   // juin seulement
        Ajouter(new(2026, 8, 1), new(2026, 8, 5));    // août seulement

        var juillet = _locations.ListerEntre(new(2026, 7, 1), new(2026, 7, 31));

        Assert.Equal(
            [new DateOnly(2026, 6, 25), new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 31)],
            juillet.Select(l => DateOnly.FromDateTime(l.DateDebut)));
    }

    [Fact]
    public void ListerParClient_la_plus_recente_en_premier()
    {
        Ajouter(new(2025, 7, 1), new(2025, 7, 8));
        Ajouter(new(2026, 7, 1), new(2026, 7, 8));
        Assert.Equal([2026, 2025], _locations.ListerParClient(_idClient).Select(l => l.DateDebut.Year));
    }

    private void Ajouter(DateOnly arrivee, DateOnly depart) =>
        _locations.Ajouter(Location.Nouvelle(arrivee, depart, _idClient));
}
