using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.Tests;

public sealed class LocationEditionViewModelTests : IDisposable
{
    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private Client AjouterClient(string nom, string email)
    {
        var client = BaseTemporaire.NouveauClient(email);
        client.Nom = nom;
        new ClientRepository(_base.Base).Ajouter(client);
        return client;
    }

    [Fact]
    public void Valeurs_par_defaut_arrivee_16h_depart_le_lendemain_10h()
    {
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5));

        Assert.Equal(new DateTime(2026, 12, 5), vm.DateDebut);
        Assert.Equal(new DateTime(2026, 12, 6), vm.DateFin);
        Assert.Equal("16:00", vm.HeureDebut);
        Assert.Equal("10:00", vm.HeureFin);
        Assert.Equal("1 nuit", vm.ResumeSejour);
    }

    [Fact]
    public void Le_client_est_obligatoire()
    {
        AjouterClient("Dupont", "a@example.com");
        AjouterClient("Martin", "b@example.com");
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5)) { TarifTtc = "300" };

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.LocationEnregistree);
        Assert.Contains("Le client est obligatoire.", vm.Erreurs);
    }

    [Fact]
    public void La_recherche_filtre_et_selectionne_un_resultat_unique()
    {
        AjouterClient("Dupont", "a@example.com");
        var martin = AjouterClient("Martin", "b@example.com");
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5));
        Assert.Equal(2, vm.Clients.Count);
        Assert.Null(vm.ClientSelectionne);

        vm.RechercheClient = "mart";

        Assert.Single(vm.Clients);
        Assert.Equal(martin.IdClient, vm.ClientSelectionne?.IdClient);
    }

    [Fact]
    public void Enregistre_la_location_avec_toutes_les_valeurs()
    {
        var client = AjouterClient("Dupont", "a@example.com");
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5))
        {
            DateFin = new DateTime(2026, 12, 8),
            HeureDebut = "15h30",
            TarifTtc = "1 250,50 €",
            NombreAdultes = "2",
            NombreEnfants = "1",
            NombreBebes = "1",
            Airbnb = true,
            NumeroReservation = "HMABC123",
        };
        vm.SelectionnerClient(client);
        Location? notifiee = null;
        vm.Enregistree += (_, l) => notifiee = l;

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.Erreurs);
        Assert.NotNull(notifiee);
        var lue = new LocationRepository(_base.Base).ObtenirParId(notifiee!.IdLocation)!;
        Assert.Equal(new DateTime(2026, 12, 5, 15, 30, 0), lue.DateDebut);
        Assert.Equal(new DateTime(2026, 12, 8, 10, 0, 0), lue.DateFin);
        Assert.Equal(client.IdClient, lue.IdClient);
        Assert.Equal(1250.50m, lue.TarifTtc);
        Assert.Equal((2, 1, 1), (lue.NombreAdultes, lue.NombreEnfants, lue.NombreBebes));
        Assert.True(lue.Airbnb);
        Assert.Equal("HMABC123", lue.NumeroReservation);
    }

    [Theory]
    [InlineData("", "Le tarif TTC est obligatoire.")]
    [InlineData("abc", "Le tarif TTC n'est pas un montant valide")]
    [InlineData("10,123", "Le tarif TTC est limité aux centimes.")]
    public void Tarif_invalide_refuse(string tarif, string message)
    {
        var client = AjouterClient("Dupont", "a@example.com");
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5)) { TarifTtc = tarif };
        vm.SelectionnerClient(client);

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.LocationEnregistree);
        Assert.Contains(message, vm.Erreurs);
    }

    [Fact]
    public void Heure_invalide_refusee()
    {
        var client = AjouterClient("Dupont", "a@example.com");
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5)) { TarifTtc = "100", HeureFin = "25:00" };
        vm.SelectionnerClient(client);

        vm.EnregistrerCommand.Execute(null);

        Assert.Contains("L'heure de départ n'est pas valide", vm.Erreurs);
    }

    [Fact]
    public void Avancer_l_arrivee_repousse_le_depart()
    {
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5));

        vm.DateDebut = new DateTime(2026, 12, 10);

        Assert.Equal(new DateTime(2026, 12, 11), vm.DateFin);
    }

    [Fact]
    public void Chevauchement_demande_confirmation()
    {
        var client = AjouterClient("Dupont", "a@example.com");
        new LocationRepository(_base.Base).Ajouter(new Location
        {
            DateDebut = new DateTime(2026, 12, 4, 16, 0, 0),
            DateFin = new DateTime(2026, 12, 7, 10, 0, 0),
            IdClient = client.IdClient,
            TarifTtc = 100,
        });
        string? question = null;
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5))
        {
            TarifTtc = "100",
            Confirmer = q => { question = q; return false; },
        };
        vm.SelectionnerClient(client);

        vm.EnregistrerCommand.Execute(null);

        Assert.NotNull(question);
        Assert.Null(vm.LocationEnregistree);
    }

    [Fact]
    public void Depart_le_jour_d_une_arrivee_n_est_pas_un_chevauchement()
    {
        var client = AjouterClient("Dupont", "a@example.com");
        new LocationRepository(_base.Base).Ajouter(Location.Nouvelle(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), client.IdClient));
        var vm = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5))
        {
            TarifTtc = "100",
            Confirmer = _ => throw new InvalidOperationException("Pas de confirmation attendue"),
        };
        vm.SelectionnerClient(client);

        vm.EnregistrerCommand.Execute(null);

        Assert.NotNull(vm.LocationEnregistree);
    }
}
