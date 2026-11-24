using GestionLocative.Core.Donnees;
using GestionLocative.Core.Recapitulatif;

namespace GestionLocative.Tests;

public sealed class RecapitulatifTests : IDisposable
{
    private readonly BaseTemporaire _temp = new();
    private readonly LocationRepository _locations;
    private readonly Client _dupont;
    private readonly Client _martin;

    public RecapitulatifTests()
    {
        _locations = new LocationRepository(_temp.Base);
        var clients = new ClientRepository(_temp.Base);
        _dupont = BaseTemporaire.NouveauClient();
        clients.Ajouter(_dupont);
        _martin = BaseTemporaire.NouveauClient("paul.martin@example.com");
        _martin.Nom = "Martin";
        _martin.Prenom = "Paul";
        clients.Ajouter(_martin);
    }

    public void Dispose() => _temp.Dispose();

    private Location Ajouter(DateOnly arrivee, DateOnly depart, Client client, decimal ttc, int adultes = 2, int enfants = 0, int bebes = 0)
    {
        var location = Location.Nouvelle(arrivee, depart, client.IdClient);
        location.TarifTtc = ttc;
        location.NombreAdultes = adultes;
        location.NombreEnfants = enfants;
        location.NombreBebes = bebes;
        _locations.Ajouter(location);
        return location;
    }

    [Fact]
    public void Une_location_a_cheval_compte_dans_l_annee_de_son_arrivee()
    {
        var reveillon = Ajouter(new(2025, 12, 28), new(2026, 1, 3), _dupont, 600m);
        var juillet = Ajouter(new(2026, 7, 1), new(2026, 7, 8), _martin, 845.50m);
        var finAnnee = Ajouter(new(2026, 12, 30), new(2027, 1, 2), _dupont, 300m);

        Assert.Equal([reveillon.IdLocation], _locations.ListerParAnneeArrivee(2025).Select(l => l.IdLocation));
        Assert.Equal([juillet.IdLocation, finAnnee.IdLocation], _locations.ListerParAnneeArrivee(2026).Select(l => l.IdLocation));
        Assert.Equal([finAnnee.IdLocation], _locations.ListerParAnneeArrivee(2026, _dupont.IdClient).Select(l => l.IdLocation));
    }

    [Fact]
    public void Le_recapitulatif_totalise_le_HT_et_le_TTC_de_l_annee()
    {
        Ajouter(new(2026, 7, 1), new(2026, 7, 8), _martin, 845.50m);
        Ajouter(new(2026, 8, 10), new(2026, 8, 12), _dupont, 220m);
        Ajouter(new(2027, 2, 1), new(2027, 2, 3), _dupont, 999m);

        var vm = new RecapitulatifViewModel(_temp.Base, 2026);

        Assert.Equal(2, vm.Lignes.Count);
        Assert.Equal("MARTIN Paul", vm.Lignes[0].Client);
        Assert.Equal(1065.50m, vm.TotalTtc);
        Assert.Equal(768.64m + 200m, vm.TotalHt); // 845,50 / 1,10 = 768,64 ; 220 / 1,10 = 200
        Assert.Equal(9, vm.TotalNuits);
        Assert.Equal("1 065,50 €", vm.TotalTtcAffiche.Replace(' ', ' ').Replace(' ', ' '));

        vm.AnneeSuivanteCommand.Execute(null);
        Assert.Equal(2027, vm.Annee);
        Assert.Single(vm.Lignes);
        Assert.Equal(999m, vm.TotalTtc);

        var impression = vm.Impression();
        Assert.Equal("Récapitulatif 2027", impression.Titre);
        Assert.Equal(impression.Colonnes.Count, impression.Total!.Count);
        Assert.All(impression.Lignes, l => Assert.Equal(impression.Colonnes.Count, l.Count));
    }

    [Fact]
    public void La_liste_des_presences_donne_les_nuitees_et_les_personnes_par_nuit_du_client()
    {
        Ajouter(new(2026, 4, 3), new(2026, 4, 6), _dupont, 300m, adultes: 2, enfants: 1, bebes: 1);
        Ajouter(new(2026, 7, 1), new(2026, 7, 8), _martin, 845.50m);
        Ajouter(new(2026, 10, 20), new(2026, 10, 22), _dupont, 200m, adultes: 1);

        var vm = new PresencesViewModel(_temp.Base, 2026);
        Assert.Null(vm.Impression());

        vm.ClientSelectionne = vm.Clients.Single(c => c.IdClient == _dupont.IdClient);

        Assert.Equal(2, vm.Lignes.Count);
        Assert.Equal(3, vm.Lignes[0].Nuitees);
        Assert.Equal(4, vm.Lignes[0].PersonnesParNuit);
        Assert.Equal("2 adultes, 1 enfant, 1 bébé", vm.Lignes[0].DetailPersonnes);
        Assert.Equal(5, vm.TotalNuitees);
        Assert.Equal(3 * 4 + 2 * 1, vm.TotalNuiteesPersonnes);

        var impression = vm.Impression()!;
        Assert.Contains("DUPONT Jean", impression.SousTitres);
        Assert.Equal(2, impression.Lignes.Count);
    }
}
