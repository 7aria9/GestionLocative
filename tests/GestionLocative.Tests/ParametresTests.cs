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

    [Fact]
    public void Le_theme_est_clair_par_defaut()
    {
        Assert.Equal(ThemeInterface.Clair, _magasin.Charger().Theme);
        Assert.True(new ParametresViewModel(_magasin).ThemeClair);
    }

    [Fact]
    public void Le_theme_sombre_est_memorise_sur_le_poste()
    {
        var vm = new ParametresViewModel(_magasin) { CheminBase = Path.Combine(_dossier, "gestionlocative.db") };
        vm.ThemeSombre = true;
        vm.EnregistrerCommand.Execute(null);

        Assert.Equal(ThemeInterface.Sombre, _magasin.Charger().Theme);
        Assert.Contains("\"Sombre\"", File.ReadAllText(_magasin.CheminFichier));
        var rouvert = new ParametresViewModel(_magasin);
        Assert.True(rouvert.ThemeSombre);
        Assert.Equal(ThemeInterface.Sombre, rouvert.ThemeInitial);
    }

    [Fact]
    public void Changer_de_theme_sans_enregistrer_ne_modifie_pas_le_poste()
    {
        var vm = new ParametresViewModel(_magasin);
        vm.ThemeSombre = true;

        Assert.Equal(ThemeInterface.Sombre, vm.Theme);
        Assert.Equal(ThemeInterface.Clair, vm.ThemeInitial);
        Assert.Equal(ThemeInterface.Clair, _magasin.Charger().Theme);
    }

    [Fact]
    public void Un_theme_inconnu_redonne_le_clair_sans_perdre_le_chemin()
    {
        Directory.CreateDirectory(_dossier);
        File.WriteAllText(_magasin.CheminFichier, """{ "CheminBase": "\\\\SERVEUR\\base.db", "Theme": "Fluo" }""");

        var parametres = _magasin.Charger();
        Assert.Equal(ThemeInterface.Clair, parametres.Theme);
        Assert.Equal(@"\\SERVEUR\base.db", parametres.CheminBase);
    }

    [Fact]
    public void Un_ancien_fichier_sans_theme_reste_lisible()
    {
        Directory.CreateDirectory(_dossier);
        File.WriteAllText(_magasin.CheminFichier, """{ "CheminBase": "C:\\Donnees\\base.db" }""");

        var parametres = _magasin.Charger();
        Assert.Equal(ThemeInterface.Clair, parametres.Theme);
        Assert.Equal(@"C:\Donnees\base.db", parametres.CheminBase);
    }
}
