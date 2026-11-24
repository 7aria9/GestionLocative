using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

public sealed class ClientRepositoryTests : IDisposable
{
    private readonly BaseTemporaire _temp = new();
    private readonly ClientRepository _clients;

    public ClientRepositoryTests() => _clients = new ClientRepository(_temp.Base);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Ajouter_attribue_un_numero_automatique_et_relit_tous_les_champs()
    {
        var client = new Client
        {
            Nom = " Martin ", Prenom = "cLAIRE", Adresse = "3 rue des Lilas", CodePostal = "44000",
            Ville = "Nantes", Telephone = "06.12.34.56.78", Email = " Claire@Example.com ", PlaqueImmatriculation = "ab-123-cd",
        };
        _clients.Ajouter(client);
        var second = BaseTemporaire.NouveauClient();
        _clients.Ajouter(second);

        Assert.True(client.IdClient > 0);
        Assert.True(second.IdClient > client.IdClient);

        var relu = _clients.ObtenirParId(client.IdClient)!;
        Assert.Equal("MARTIN", relu.Nom);
        Assert.Equal("Claire", relu.Prenom);
        Assert.Equal("3 rue des Lilas", relu.Adresse);
        Assert.Equal("44000", relu.CodePostal);
        Assert.Equal("Nantes", relu.Ville);
        Assert.Equal("+33612345678", relu.Telephone);
        Assert.Equal("claire@example.com", relu.Email);
        Assert.Equal("AB-123-CD", relu.PlaqueImmatriculation);
    }

    [Fact]
    public void Les_champs_facultatifs_vides_sont_enregistres_comme_absents()
    {
        var client = BaseTemporaire.NouveauClient();
        client.Ville = "   ";
        _clients.Ajouter(client);
        var relu = _clients.ObtenirParId(client.IdClient)!;
        Assert.Null(relu.Ville);
        Assert.Null(relu.Adresse);
        Assert.Null(relu.PlaqueImmatriculation);
    }

    [Theory]
    [InlineData("", "Jean", "0612345678", "a@b.fr", "Le nom est obligatoire.")]
    [InlineData("Dupont", " ", "0612345678", "a@b.fr", "Le prénom est obligatoire.")]
    [InlineData("Dupont", "Jean", "", "a@b.fr", "Le téléphone est obligatoire.")]
    [InlineData("Dupont", "Jean", "0612345678", "", "L'email est obligatoire.")]
    [InlineData("Dupont", "Jean", "0612", "a@b.fr", "Le téléphone n'est pas valide (ex. 06 12 34 56 78, ou +44 20 7946 0958 pour l'étranger).")]
    [InlineData("Dupont", "Jean", "0612345678", "jean@exemple", "L'email n'est pas valide (ex. jean.dupont@exemple.fr).")]
    [InlineData("Dupont", "Jean", "0612345678", "pas-un-email", "L'email n'est pas valide (ex. jean.dupont@exemple.fr).")]
    public void Les_champs_obligatoires_sont_verifies(string nom, string prenom, string telephone, string email, string erreur)
    {
        var client = new Client { Nom = nom, Prenom = prenom, Telephone = telephone, Email = email };
        var ex = Assert.Throws<DonneesInvalidesException>(() => _clients.Ajouter(client));
        Assert.Contains(erreur, ex.Erreurs);
        Assert.Empty(_clients.Lister());
    }

    [Fact]
    public void L_email_est_unique_sans_tenir_compte_des_majuscules()
    {
        _clients.Ajouter(BaseTemporaire.NouveauClient("jean.dupont@example.com"));
        var ex = Assert.Throws<DonneesInvalidesException>(() => _clients.Ajouter(BaseTemporaire.NouveauClient("Jean.Dupont@Example.com")));
        Assert.Equal("Un autre client utilise déjà cet email.", ex.Message);
    }

    [Fact]
    public void Modifier_vers_un_email_deja_pris_est_refuse()
    {
        _clients.Ajouter(BaseTemporaire.NouveauClient("a@example.com"));
        var autre = BaseTemporaire.NouveauClient("b@example.com");
        _clients.Ajouter(autre);

        autre.Email = "a@example.com";
        Assert.Throws<DonneesInvalidesException>(() => _clients.Modifier(autre));
        Assert.Equal("b@example.com", _clients.ObtenirParId(autre.IdClient)!.Email);
    }

    [Fact]
    public void Modifier_enregistre_les_changements()
    {
        var client = BaseTemporaire.NouveauClient();
        _clients.Ajouter(client);
        client.Ville = "Brest";
        _clients.Modifier(client);
        Assert.Equal("Brest", _clients.ObtenirParId(client.IdClient)!.Ville);
    }

    [Fact]
    public void ObtenirParEmail_ignore_les_majuscules()
    {
        _clients.Ajouter(BaseTemporaire.NouveauClient("jean@example.com"));
        Assert.NotNull(_clients.ObtenirParEmail(" JEAN@example.com "));
    }

    [Fact]
    public void Lister_trie_par_nom_et_filtre_la_recherche()
    {
        _clients.Ajouter(new Client { Nom = "Zola", Prenom = "Émile", Telephone = "0611111111", Email = "z@x.fr" });
        _clients.Ajouter(new Client { Nom = "andré", Prenom = "Paul", Telephone = "0622222222", Email = "a@x.fr", PlaqueImmatriculation = "GH-456-IJ" });
        _clients.Ajouter(new Client { Nom = "Martin", Prenom = "Léa", Telephone = "+44 20 7946 0958", Email = "m@x.fr" });

        Assert.Equal(["ANDRÉ", "MARTIN", "ZOLA"], _clients.Lister().Select(c => c.Nom));
        Assert.Equal(["ANDRÉ"], _clients.Lister("gh-456").Select(c => c.Nom));
        Assert.Equal(["MARTIN"], _clients.Lister("mar").Select(c => c.Nom));
        Assert.Equal(["ANDRÉ"], _clients.Lister("06 22 22").Select(c => c.Nom));
        Assert.Equal(["MARTIN"], _clients.Lister("020 7946").Select(c => c.Nom));
        Assert.Empty(_clients.Lister("100%"));
    }

    [Fact]
    public void Un_client_avec_des_locations_ne_peut_pas_etre_supprime()
    {
        var client = BaseTemporaire.NouveauClient();
        _clients.Ajouter(client);
        new LocationRepository(_temp.Base).Ajouter(Location.Nouvelle(new(2026, 7, 1), new(2026, 7, 8), client.IdClient));

        var ex = Assert.Throws<DonneesInvalidesException>(() => _clients.Supprimer(client.IdClient));
        Assert.Equal("Ce client a des locations : supprimez-les d'abord.", ex.Message);
        Assert.NotNull(_clients.ObtenirParId(client.IdClient));
    }

    [Fact]
    public void Supprimer_un_client_sans_location()
    {
        var client = BaseTemporaire.NouveauClient();
        _clients.Ajouter(client);
        _clients.Supprimer(client.IdClient);
        Assert.Null(_clients.ObtenirParId(client.IdClient));
    }
}
