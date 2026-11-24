using GestionLocative.Core.Donnees;
using GestionLocative.Core.Parametres;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.Tests;

public sealed class ReglagesTests : IDisposable
{
    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public void Le_taux_de_tva_est_de_10_pourcent_par_defaut_et_modifiable()
    {
        var reglages = new ReglagesRepository(_base.Base);
        Assert.Equal(10m, reglages.ObtenirTauxTva());

        reglages.DefinirTauxTva(5.5m);

        Assert.Equal(5.5m, new ReglagesRepository(_base.Base).ObtenirTauxTva());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(5.555)]
    public void Un_taux_invalide_est_refuse(double taux)
    {
        Assert.Throws<DonneesInvalidesException>(() => new ReglagesRepository(_base.Base).DefinirTauxTva((decimal)taux));
    }

    [Theory]
    [InlineData(450, 10, 409.09)]
    [InlineData(110, 10, 100)]
    [InlineData(100, 0, 100)]
    [InlineData(105.5, 5.5, 100)]
    public void Calcul_du_prix_ht(double ttc, double taux, double ht) =>
        Assert.Equal((decimal)ht, Location.CalculerHt((decimal)ttc, (decimal)taux));

    [Fact]
    public void La_fenetre_parametres_affiche_et_enregistre_le_taux()
    {
        var magasin = new MagasinParametres(Path.Combine(_base.Dossier, "parametres.json"));
        magasin.Enregistrer(new ParametresApplication { CheminBase = _base.Base.CheminFichier });
        var vm = new ParametresViewModel(magasin, _base.Base);
        Assert.Equal("10", vm.TauxTva);

        vm.TauxTva = "5,5 %";
        vm.EnregistrerCommand.Execute(null);

        Assert.False(vm.MessageEstErreur);
        Assert.Equal(5.5m, new ReglagesRepository(_base.Base).ObtenirTauxTva());
    }

    [Fact]
    public void Un_taux_saisi_invalide_bloque_l_enregistrement()
    {
        var magasin = new MagasinParametres(Path.Combine(_base.Dossier, "parametres.json"));
        var vm = new ParametresViewModel(magasin, _base.Base) { CheminBase = _base.Base.CheminFichier, TauxTva = "abc" };
        var appels = 0;
        vm.Enregistre += (_, _) => appels++;

        vm.EnregistrerCommand.Execute(null);

        Assert.Equal(0, appels);
        Assert.True(vm.MessageEstErreur);
        Assert.Equal(10m, new ReglagesRepository(_base.Base).ObtenirTauxTva());
    }

    [Fact]
    public void Telephones_du_prestataire_au_format_international_et_effacables()
    {
        var reglages = new ReglagesRepository(_base.Base);
        Assert.Null(reglages.ObtenirTelephonePrestatairePrincipal());

        reglages.DefinirTelephonePrestatairePrincipal("06 12 34 56 78");
        reglages.DefinirTelephonePrestataireSecondaire("0033 7 00 00 00 01");
        Assert.Equal("+33612345678", reglages.ObtenirTelephonePrestatairePrincipal());
        Assert.Equal("+33700000001", reglages.ObtenirTelephonePrestataireSecondaire());

        reglages.DefinirTelephonePrestataireSecondaire(" ");
        Assert.Null(reglages.ObtenirTelephonePrestataireSecondaire());
        Assert.Throws<DonneesInvalidesException>(() => reglages.DefinirTelephonePrestatairePrincipal("0612"));
    }

    [Fact]
    public void La_fenetre_parametres_affiche_et_enregistre_les_telephones_du_prestataire()
    {
        new ReglagesRepository(_base.Base).DefinirTelephonePrestatairePrincipal("0612345678");
        var magasin = new MagasinParametres(Path.Combine(_base.Dossier, "parametres.json"));
        var vm = new ParametresViewModel(magasin, _base.Base) { CheminBase = _base.Base.CheminFichier };
        Assert.Equal("+33 6 12 34 56 78", vm.TelephonePrestatairePrincipal);
        Assert.Equal("", vm.TelephonePrestataireSecondaire);

        vm.TelephonePrestataireSecondaire = "07 11 22 33 44";
        vm.EnregistrerCommand.Execute(null);

        Assert.False(vm.MessageEstErreur);
        var reglages = new ReglagesRepository(_base.Base);
        Assert.Equal("+33612345678", reglages.ObtenirTelephonePrestatairePrincipal());
        Assert.Equal("+33711223344", reglages.ObtenirTelephonePrestataireSecondaire());
    }

    [Fact]
    public void Un_telephone_prestataire_invalide_bloque_l_enregistrement()
    {
        var magasin = new MagasinParametres(Path.Combine(_base.Dossier, "parametres.json"));
        var vm = new ParametresViewModel(magasin, _base.Base) { CheminBase = _base.Base.CheminFichier, TelephonePrestatairePrincipal = "12" };
        var appels = 0;
        vm.Enregistre += (_, _) => appels++;

        vm.EnregistrerCommand.Execute(null);

        Assert.Equal(0, appels);
        Assert.True(vm.MessageEstErreur);
        Assert.Null(new ReglagesRepository(_base.Base).ObtenirTelephonePrestatairePrincipal());
    }

    [Fact]
    public void Changer_de_base_sans_toucher_au_taux_garde_celui_de_la_nouvelle_base()
    {
        using var autre = new BaseTemporaire();
        new ReglagesRepository(autre.Base).DefinirTauxTva(20m);
        var magasin = new MagasinParametres(Path.Combine(_base.Dossier, "parametres.json"));
        var vm = new ParametresViewModel(magasin, _base.Base) { CheminBase = autre.Base.CheminFichier };

        vm.EnregistrerCommand.Execute(null);

        Assert.Equal(20m, new ReglagesRepository(autre.Base).ObtenirTauxTva());
    }
}
