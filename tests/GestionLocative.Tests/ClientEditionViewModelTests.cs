using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.Tests;

public sealed class ClientEditionViewModelTests : IDisposable
{
    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public void Champs_obligatoires_verifies()
    {
        var vm = new ClientEditionViewModel(new ClientRepository(_base.Base)) { Nom = "Dupont" };

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.ClientEnregistre);
        Assert.Contains("Le prénom est obligatoire.", vm.Erreurs);
        Assert.Contains("Le téléphone est obligatoire.", vm.Erreurs);
        Assert.Contains("L'email est obligatoire.", vm.Erreurs);
    }

    [Fact]
    public void Email_deja_utilise_refuse_avec_le_nom_du_client()
    {
        var depot = new ClientRepository(_base.Base);
        depot.Ajouter(BaseTemporaire.NouveauClient("jean@example.com"));
        var vm = new ClientEditionViewModel(depot)
        {
            Nom = "Martin", Prenom = "Paul", Telephone = "0600000000", Email = "JEAN@example.com",
        };

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.ClientEnregistre);
        Assert.Contains("DUPONT Jean", vm.Erreurs);
    }

    [Fact]
    public void Client_cree_puis_selectionne_dans_la_location()
    {
        var depot = new ClientRepository(_base.Base);
        depot.Ajouter(BaseTemporaire.NouveauClient("autre@example.com"));
        var location = new LocationEditionViewModel(_base.Base, new DateOnly(2026, 12, 5));
        var vm = new ClientEditionViewModel(location.DepotClients)
        {
            Nom = "Martin", Prenom = "Paul", Telephone = "0600000000", Email = "paul@example.com", Ville = "Annecy",
        };
        vm.Enregistre += (_, c) => location.SelectionnerClient(c);

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.Erreurs);
        Assert.Equal("paul@example.com", location.ClientSelectionne?.Email);
        Assert.Equal("Annecy", depot.ObtenirParId(location.ClientSelectionne!.IdClient)?.Ville);
    }

    [Fact]
    public void La_mise_en_forme_est_appliquee_et_affichee()
    {
        var vm = new ClientEditionViewModel(new ClientRepository(_base.Base))
        {
            Nom = "de la tour", Prenom = "JEAN-PIERRE", Telephone = "06 12 34 56 78", Email = " JP@Example.FR ",
        };

        vm.EnregistrerCommand.Execute(null);

        Assert.Null(vm.Erreurs);
        Assert.Equal(("DE LA TOUR", "Jean-Pierre", "+33 6 12 34 56 78", "jp@example.fr"), (vm.Nom, vm.Prenom, vm.Telephone, vm.Email));
        var lu = new ClientRepository(_base.Base).ObtenirParId(vm.ClientEnregistre!.IdClient)!;
        Assert.Equal(("DE LA TOUR", "Jean-Pierre", "+33612345678", "jp@example.fr"), (lu.Nom, lu.Prenom, lu.Telephone, lu.Email));
    }
}
