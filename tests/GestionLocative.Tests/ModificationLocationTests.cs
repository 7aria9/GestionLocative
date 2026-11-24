using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.Tests;

public sealed class ModificationLocationTests : IDisposable
{
    private readonly BaseTemporaire _base = new();
    private readonly Client _client;
    private readonly Location _location;

    public ModificationLocationTests()
    {
        _client = BaseTemporaire.NouveauClient();
        _client.PlaqueImmatriculation = "ab-123-cd";
        new ClientRepository(_base.Base).Ajouter(_client);
        _location = new Location
        {
            DateDebut = new DateTime(2026, 12, 5, 15, 30, 0),
            DateFin = new DateTime(2026, 12, 8, 10, 0, 0),
            IdClient = _client.IdClient,
            TarifTtc = 450.5m,
            NombreAdultes = 2,
            NombreEnfants = 1,
            Airbnb = true,
            NumeroReservation = "HM123",
        };
        new LocationRepository(_base.Base).Ajouter(_location);
    }

    public void Dispose() => _base.Dispose();

    [Fact]
    public void La_fenetre_de_modification_reprend_toutes_les_valeurs()
    {
        var vm = new LocationEditionViewModel(_base.Base, _location);

        Assert.True(vm.EstModification);
        Assert.Equal("Modifier la location", vm.Titre);
        Assert.Equal(new DateTime(2026, 12, 5), vm.DateDebut);
        Assert.Equal(new DateTime(2026, 12, 8), vm.DateFin);
        Assert.Equal(("15:30", "10:00"), (vm.HeureDebut, vm.HeureFin));
        Assert.Equal("450,50", vm.TarifTtc);
        Assert.Equal(("2", "1", "0"), (vm.NombreAdultes, vm.NombreEnfants, vm.NombreBebes));
        Assert.True(vm.Airbnb);
        Assert.Equal("HM123", vm.NumeroReservation);
        Assert.Equal(_client.IdClient, vm.ClientSelectionne?.IdClient);
    }

    [Fact]
    public void Modifier_met_a_jour_la_meme_location_sans_signaler_de_chevauchement_avec_elle_meme()
    {
        var vm = new LocationEditionViewModel(_base.Base, _location)
        {
            TarifTtc = "500",
            Confirmer = _ => throw new InvalidOperationException("Pas de confirmation attendue"),
        };
        vm.DateFin = new DateTime(2026, 12, 9);

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.Erreurs);
        var depot = new LocationRepository(_base.Base);
        var lue = depot.ObtenirParId(_location.IdLocation)!;
        Assert.Equal(500m, lue.TarifTtc);
        Assert.Equal(new DateTime(2026, 12, 9, 10, 0, 0), lue.DateFin);
        Assert.Single(depot.ListerParClient(_client.IdClient));
    }

    [Fact]
    public void Supprimer_demande_confirmation()
    {
        var reponse = false;
        var supprimee = false;
        var vm = new LocationEditionViewModel(_base.Base, _location) { Confirmer = _ => reponse };
        vm.Supprimee += (_, _) => supprimee = true;

        vm.SupprimerCommand.Execute(null);
        Assert.False(supprimee);
        Assert.NotNull(new LocationRepository(_base.Base).ObtenirParId(_location.IdLocation));

        reponse = true;
        vm.SupprimerCommand.Execute(null);
        Assert.True(supprimee);
        Assert.Null(new LocationRepository(_base.Base).ObtenirParId(_location.IdLocation));
    }

    [Fact]
    public void Une_nouvelle_location_ne_peut_pas_etre_supprimee()
    {
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 20));
        Assert.False(vm.EstModification);
        Assert.False(vm.SupprimerCommand.CanExecute(null));
    }

    [Fact]
    public void Message_prestataire()
    {
        var attendu = string.Join(Environment.NewLine,
            "du 05/12/2026 au 08/12/2026",
            "DUPONT Jean",
            "2 adultes, 1 enfant, 0 bébé",
            "+33 6 12 34 56 78");
        Assert.Equal(attendu, TextesLocation.MessagePrestataire(_location, new ClientRepository(_base.Base).ObtenirParId(_client.IdClient)!));
    }
}
