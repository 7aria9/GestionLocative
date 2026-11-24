using GestionLocative.Core.Parametres;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.Tests;

public sealed class ParametresTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "GestionLocativeTests", Guid.NewGuid().ToString("N"));
    private readonly MagasinParametres _magasin;

    public ParametresTests() => _magasin = new MagasinParametres(Path.Combine(_dossier, "parametres.json"));

    public void Dispose()
    {
        try { Directory.Delete(_dossier, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Sans_fichier_le_chemin_par_defaut_est_utilise()
    {
        Assert.Equal(ParametresApplication.CheminBaseParDefaut, _magasin.Charger().CheminBase);
    }

    [Fact]
    public void Le_chemin_reseau_est_conserve()
    {
        _magasin.Enregistrer(new ParametresApplication { CheminBase = @"\\SERVEUR\Partage\gestionlocative.db" });
        Assert.Equal(@"\\SERVEUR\Partage\gestionlocative.db", _magasin.Charger().CheminBase);
    }

    [Fact]
    public void Un_fichier_abime_redonne_les_valeurs_par_defaut()
    {
        Directory.CreateDirectory(_dossier);
        File.WriteAllText(_magasin.CheminFichier, "{ pas du json");
        Assert.Equal(ParametresApplication.CheminBaseParDefaut, _magasin.Charger().CheminBase);
    }

    [Fact]
    public void Enregistrer_cree_la_base_et_memorise_le_chemin()
    {
        var vm = new ParametresViewModel(_magasin);
        var chemin = Path.Combine(_dossier, "partage", "gestionlocative.db");
        Core.Donnees.BaseDeDonnees? ouverte = null;
        vm.Enregistre += (_, b) => ouverte = b;

        vm.CheminBase = chemin;
        vm.EnregistrerCommand.Execute(null);

        Assert.NotNull(ouverte);
        Assert.True(File.Exists(chemin));
        Assert.Equal(chemin, _magasin.Charger().CheminBase);
        Assert.False(vm.MessageEstErreur);
    }

    [Fact]
    public void Un_chemin_relatif_est_refuse_et_rien_n_est_enregistre()
    {
        var vm = new ParametresViewModel(_magasin);
        var appels = 0;
        vm.Enregistre += (_, _) => appels++;

        vm.CheminBase = "base.db";
        vm.EnregistrerCommand.Execute(null);

        Assert.Equal(0, appels);
        Assert.True(vm.MessageEstErreur);
        Assert.False(File.Exists(_magasin.CheminFichier));
    }

    [Fact]
    public void Un_chemin_vide_ne_peut_pas_etre_enregistre()
    {
        var vm = new ParametresViewModel(_magasin) { CheminBase = " " };
        Assert.False(vm.EnregistrerCommand.CanExecute(null));
    }
}
