using System.Text;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Import;

namespace GestionLocative.Tests;

public sealed class LecteurCsvTests
{
    static LecteurCsvTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Lit_un_fichier_Windows_1252_separe_par_des_points_virgules()
    {
        var octets = Encoding.GetEncoding(1252).GetBytes("Nom;Prénom\r\nLEFÈVRE;François\r\n");

        var tableau = LecteurCsv.Lire(octets);

        Assert.Equal("Windows-1252", tableau.Encodage);
        Assert.Equal(';', tableau.Separateur);
        Assert.Equal(["Nom", "Prénom"], tableau.Entetes);
        Assert.Equal(["LEFÈVRE", "François"], tableau.Lignes[0].Valeurs);
        Assert.Equal(2, tableau.Lignes[0].NumeroLigne);
    }

    [Fact]
    public void Lit_un_fichier_UTF8_avec_BOM_separe_par_des_virgules_et_des_guillemets()
    {
        var texte = "Nom,Adresse,Note\n\"Martin\",\"3, place de l'Église\",\"Il a dit \"\"bonjour\"\"\nsur deux lignes\"\nDurand,Lyon,\n";
        var octets = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(texte)).ToArray();

        var tableau = LecteurCsv.Lire(octets);

        Assert.Equal("UTF-8", tableau.Encodage);
        Assert.Equal(',', tableau.Separateur);
        Assert.Equal(2, tableau.Lignes.Count);
        Assert.Equal("3, place de l'Église", tableau.Lignes[0].Valeurs[1]);
        Assert.Equal("Il a dit \"bonjour\"\nsur deux lignes", tableau.Lignes[0].Valeurs[2]);
        Assert.Equal(4, tableau.Lignes[1].NumeroLigne); // la valeur sur deux lignes décale la numérotation
    }

    [Fact]
    public void UTF8_sans_BOM_est_reconnu()
    {
        var tableau = LecteurCsv.Lire(Encoding.UTF8.GetBytes("Prénom;Ville\nÉlodie;Orléans"));
        Assert.Equal("UTF-8", tableau.Encodage);
        Assert.Equal("Élodie", tableau.Lignes[0].Valeurs[0]);
    }

    [Fact]
    public void Ligne_sep_d_Excel_impose_le_separateur()
    {
        var tableau = LecteurCsv.LireTexte("sep=,\nNom;Prénom,Ville\nA;B,C");
        Assert.Equal(',', tableau.Separateur);
        Assert.Equal(["Nom;Prénom", "Ville"], tableau.Entetes);
        Assert.Equal(3, tableau.Lignes[0].NumeroLigne);
    }

    [Fact]
    public void Lignes_vides_ignorees_et_lignes_courtes_completees()
    {
        var tableau = LecteurCsv.LireTexte("A;B;C;;\n\n1;2\n;;\n4;5;6;7");
        Assert.Equal(["A", "B", "C"], tableau.Entetes);
        Assert.Equal(2, tableau.Lignes.Count);
        Assert.Equal(["1", "2", ""], tableau.Lignes[0].Valeurs);
        Assert.Equal(1, tableau.Lignes[1].ValeursEnTrop);
    }

    [Fact]
    public void Fichier_vide_refuse() => Assert.Throws<FormatException>(() => LecteurCsv.LireTexte("\r\n  \r\n"));
}

public sealed class ValeursCsvTests
{
    [Theory]
    [InlineData("05/12/2026", 2026, 12, 5, null)]
    [InlineData("5/2/2026", 2026, 2, 5, null)]
    [InlineData("2026-12-05", 2026, 12, 5, null)]
    [InlineData("20261205", 2026, 12, 5, null)]
    [InlineData("05/12/2026 15:30", 2026, 12, 5, "15:30")]
    [InlineData("2026-12-05T15:30:00", 2026, 12, 5, "15:30")]
    [InlineData("05/12/2026 00:00:00", 2026, 12, 5, null)]
    [InlineData("20261205153000", 2026, 12, 5, "15:30")]
    public void Dates(string texte, int annee, int mois, int jour, string? heure)
    {
        Assert.True(ValeursCsv.LireDate(texte, out var date, out var h));
        Assert.Equal(new DateOnly(annee, mois, jour), date);
        Assert.Equal(heure is null ? null : TimeOnly.Parse(heure), h);
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("demain")]
    [InlineData("05/12/2026 25:00")]
    public void Dates_invalides(string texte) => Assert.False(ValeursCsv.LireDate(texte, out _, out _));

    [Theory]
    [InlineData("450", "450")]
    [InlineData("450,5", "450.50")]
    [InlineData("450.50", "450.50")]
    [InlineData("1 250,00 €", "1250")]
    [InlineData("1.250,00", "1250")]
    [InlineData("1,250.00", "1250")]
    [InlineData("450,499999", "450.50")]
    [InlineData("1250 EUR", "1250")]
    public void Montants(string texte, string attendu) =>
        Assert.Equal(decimal.Parse(attendu, System.Globalization.CultureInfo.InvariantCulture), ValeursCsv.LireMontant(texte));

    [Theory]
    [InlineData("Oui", true)]
    [InlineData("1", true)]
    [InlineData("X", true)]
    [InlineData("Airbnb", true)]
    [InlineData("Réservé via AirBnB", true)]
    [InlineData("Non", false)]
    [InlineData("Booking", false)]
    [InlineData("", false)]
    public void Airbnb(string texte, bool attendu) => Assert.Equal(attendu, ValeursCsv.LireAirbnb(texte));

    [Fact]
    public void Correspondance_souple_des_colonnes()
    {
        var colonnes = CorrespondanceColonnes.Etablir(
            ["N° Client", "NOM", "Prénom", "E-mail", "Tél. fixe", "Portable", "Adresse mail perso", "Commentaire"],
            ImportCsvClients.Champs);

        Assert.True(colonnes.Contient(ImportCsvClients.IdOrigine));
        Assert.True(colonnes.Contient(ImportCsvClients.Prenom));
        Assert.Equal(["Commentaire"], colonnes.Ignorees);
        var ligne = new LigneCsv(2, ["7", "Durand", "Paul", "", "", "06 11 22 33 44", "paul@exemple.fr", "x"], 0);
        Assert.Equal("06 11 22 33 44", colonnes.Valeur(ligne, ImportCsvClients.Telephone)); // fixe vide → portable
        Assert.Equal("paul@exemple.fr", colonnes.Valeur(ligne, ImportCsvClients.Email));     // « Adresse mail » ≠ adresse postale
        Assert.Null(colonnes.Valeur(ligne, ImportCsvClients.Adresse));
    }
}

public sealed class ImportCsvTests : IDisposable
{
    private readonly BaseTemporaire _temp = new();

    public void Dispose() => _temp.Dispose();

    private static TableauCsv Csv(string texte) => LecteurCsv.LireTexte(texte);

    private const string Clients = """
        N° client;Nom;Prénom;Téléphone;E-mail;Ville
        1;dupont;jean-pierre;06 12 34 56 78;Jean-Pierre.Dupont@Exemple.fr;Paris
        2;Martin;élodie;0033 6 98 76 54 32;elodie.martin@exemple.fr;
        3;Sans;Email;06 00 00 00 01;;
        4;Mauvais;Telephone;12;mauvais@exemple.fr;
        5;Dupont;Jean-Pierre;0612345678;jean-pierre.dupont@exemple.fr;
        """;

    [Fact]
    public void Apercu_des_clients_sans_rien_enregistrer()
    {
        var rapport = ImportCsv.Analyser(_temp.Base, TypeImport.Clients, Csv(Clients));

        Assert.Null(rapport.ErreurFichier);
        Assert.Equal(2, rapport.NombreAImporter);
        Assert.Equal(1, rapport.NombreDejaPresentes);
        Assert.Equal(2, rapport.NombreRejetees);
        Assert.Contains("L'email est obligatoire", rapport.Lignes.Single(l => l.NumeroLigne == 4).Detail);
        Assert.Contains("téléphone", rapport.Lignes.Single(l => l.NumeroLigne == 5).Detail);
        Assert.Contains("ligne 2", rapport.Lignes.Single(l => l.NumeroLigne == 6).Detail);
        Assert.Empty(new ClientRepository(_temp.Base).Lister());
    }

    [Fact]
    public void Import_des_clients_normalises_et_dedoublonnes()
    {
        var rapport = ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));

        Assert.True(rapport.Enregistre);
        var clients = new ClientRepository(_temp.Base).Lister();
        Assert.Equal(2, clients.Count);
        var dupont = clients.Single(c => c.Nom == "DUPONT");
        Assert.Equal("Jean-Pierre", dupont.Prenom);
        Assert.Equal("+33612345678", dupont.Telephone);
        Assert.Equal("jean-pierre.dupont@exemple.fr", dupont.Email);
        Assert.Equal("Paris", dupont.Ville);
        Assert.Equal("+33698765432", clients.Single(c => c.Nom == "MARTIN").Telephone);

        // Réimport : tout est déjà présent.
        var deuxieme = ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));
        Assert.Equal(0, deuxieme.NombreAImporter);
        Assert.Equal(3, deuxieme.NombreDejaPresentes);
        Assert.Equal(2, new ClientRepository(_temp.Base).Lister().Count);
    }

    [Fact]
    public void Client_deja_dans_la_base_n_est_pas_modifie()
    {
        var existant = BaseTemporaire.NouveauClient("elodie.martin@exemple.fr");
        new ClientRepository(_temp.Base).Ajouter(existant);

        var rapport = ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));

        Assert.Equal(StatutLigne.DejaPresente, rapport.Lignes.Single(l => l.NumeroLigne == 3).Statut);
        Assert.Equal("DUPONT", new ClientRepository(_temp.Base).ObtenirParId(existant.IdClient)!.Nom);
    }

    [Fact]
    public void Colonnes_obligatoires_manquantes()
    {
        var rapport = ImportCsv.Analyser(_temp.Base, TypeImport.Clients, Csv("Nom;Prénom\nDupont;Jean"));
        Assert.NotNull(rapport.ErreurFichier);
        Assert.Contains("Email", rapport.ErreurFichier);
        Assert.False(ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv("Nom;Prénom\nDupont;Jean")).Enregistre);
    }

    private const string Locations = """
        N° réservation;N° client;Email client;Date arrivée;Date départ;Tarif TTC;Adultes;Enfants;Bébés;Airbnb
        R1;1;;15/07/2025;22/07/2025;"1 250,00 €";2;2;0;Non
        HM1;0002;;01/08/2025 15:00;05/08/2025 11:00;680,50;2;0;1;Oui
        R3;;JEAN-PIERRE.DUPONT@exemple.fr;20/12/2025;03/01/2026;1450;4;;;
        R4;5;;14/02/2026;16/02/2026;320;2;;;
        R5;99;;14/03/2026;16/03/2026;320;2;;;
        R6;1;;16/03/2026;14/03/2026;320;2;;;
        R7;1;;01/04/2026;03/04/2026;gratuit;2;;;
        R1;1;;15/07/2025;22/07/2025;1250;2;2;0;Non
        ;2;;01/08/2025;05/08/2025;680,50;2;;;
        """;

    [Fact]
    public void Import_des_locations_rattachees_par_numero_d_origine_ou_email()
    {
        ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));

        var rapport = ImportCsv.Importer(_temp.Base, TypeImport.Locations, Csv(Locations));

        Assert.True(rapport.Enregistre);
        Assert.Equal(4, rapport.NombreAImporter);
        Assert.Equal(2, rapport.NombreDejaPresentes);
        Assert.Equal(3, rapport.NombreRejetees);
        Assert.Contains("introuvable", rapport.Lignes.Single(l => l.NumeroLigne == 6).Detail);
        Assert.Contains("après la date de début", rapport.Lignes.Single(l => l.NumeroLigne == 7).Detail);
        Assert.Contains("gratuit", rapport.Lignes.Single(l => l.NumeroLigne == 8).Detail);
        Assert.Contains("R1", rapport.Lignes.Single(l => l.NumeroLigne == 9).Detail);
        Assert.Contains("mêmes dates", rapport.Lignes.Single(l => l.NumeroLigne == 10).Detail);

        var clients = new ClientRepository(_temp.Base);
        var dupont = clients.ObtenirParEmail("jean-pierre.dupont@exemple.fr")!;
        var martin = clients.ObtenirParEmail("elodie.martin@exemple.fr")!;
        var repo = new LocationRepository(_temp.Base);

        var r1 = repo.ListerParClient(dupont.IdClient).Single(l => l.NumeroReservation == "R1");
        Assert.Equal(new DateTime(2025, 7, 15, 16, 0, 0), r1.DateDebut); // 16h par défaut
        Assert.Equal(new DateTime(2025, 7, 22, 10, 0, 0), r1.DateFin);   // 10h par défaut
        Assert.Equal(1250m, r1.TarifTtc);
        Assert.Equal((2, 2, 0, false), (r1.NombreAdultes, r1.NombreEnfants, r1.NombreBebes, r1.Airbnb));

        var hm1 = repo.ListerParClient(martin.IdClient).Single();
        Assert.Equal(new DateTime(2025, 8, 1, 15, 0, 0), hm1.DateDebut);
        Assert.Equal(new DateTime(2025, 8, 5, 11, 0, 0), hm1.DateFin);
        Assert.True(hm1.Airbnb);
        Assert.Equal(680.50m, hm1.TarifTtc);

        // Le client n° 5 (doublon du n° 1 par l'email) désigne la même fiche.
        Assert.Equal(3, repo.ListerParClient(dupont.IdClient).Count);

        // Réimport : rien de nouveau.
        Assert.Equal(0, ImportCsv.Importer(_temp.Base, TypeImport.Locations, Csv(Locations)).NombreAImporter);
    }

    [Fact]
    public void Locations_sans_colonne_client_refusees()
    {
        var rapport = ImportCsv.Analyser(_temp.Base, TypeImport.Locations, Csv("Arrivée;Départ;Prix\n01/01/2026;02/01/2026;100"));
        Assert.Contains("N° client d'origine (ou l'email du client)", rapport.ErreurFichier);
    }

    [Fact]
    public void Heures_dans_des_colonnes_separees()
    {
        ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));
        var rapport = ImportCsv.Importer(_temp.Base, TypeImport.Locations,
            Csv("Email;Date début;Heure début;Date fin;Heure fin;Prix TTC\nelodie.martin@exemple.fr;2026-05-01;14h;2026-05-03;12:30;200"));

        Assert.Equal(1, rapport.NombreAImporter);
        var location = new LocationRepository(_temp.Base).ListerEntre(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)).Single();
        Assert.Equal(new DateTime(2026, 5, 1, 14, 0, 0), location.DateDebut);
        Assert.Equal(new DateTime(2026, 5, 3, 12, 30, 0), location.DateFin);
    }

    [Fact]
    public void Separateur_dans_une_valeur_sans_guillemets_rejete_la_ligne()
    {
        var rapport = ImportCsv.Analyser(_temp.Base, TypeImport.Clients,
            Csv("Nom,Prénom,Téléphone,Email\nDupont,Jean,0612345678,jean@exemple.fr,en trop"));
        Assert.Equal(StatutLigne.Rejetee, rapport.Lignes.Single().Statut);
        Assert.Contains("guillemets", rapport.Lignes.Single().Detail);
    }

    [Fact]
    public void Suppression_d_un_client_importe_supprime_son_numero_d_origine()
    {
        ImportCsv.Importer(_temp.Base, TypeImport.Clients, Csv(Clients));
        var martin = new ClientRepository(_temp.Base).ObtenirParEmail("elodie.martin@exemple.fr")!;
        new ClientRepository(_temp.Base).Supprimer(martin.IdClient);

        var rapport = ImportCsv.Analyser(_temp.Base, TypeImport.Locations, Csv("N° client;Arrivée;Départ;Tarif\n2;01/01/2026;02/01/2026;100"));
        Assert.Contains("introuvable", rapport.Lignes.Single().Detail);
    }

    [Fact]
    public void Le_type_de_fichier_est_devine_d_apres_les_colonnes()
    {
        Assert.Equal(TypeImport.Clients, ImportViewModel.DevinerType(Csv(Clients)));
        Assert.Equal(TypeImport.Locations, ImportViewModel.DevinerType(Csv(Locations)));
    }

    [Fact]
    public void Fichiers_d_exemple_du_depot()
    {
        var dossier = TrouverExemples();
        var clients = ImportCsv.Importer(_temp.Base, TypeImport.Clients, LecteurCsv.LireFichier(Path.Combine(dossier, "clients.csv")));
        Assert.Equal("Windows-1252", clients.Tableau.Encodage);
        Assert.Equal((4, 1, 0), (clients.NombreAImporter, clients.NombreDejaPresentes, clients.NombreRejetees));
        Assert.Equal("Lefèvre".ToUpperInvariant(), new ClientRepository(_temp.Base).ObtenirParEmail("f.lefevre@exemple.re")!.Nom);

        var locations = ImportCsv.Importer(_temp.Base, TypeImport.Locations, LecteurCsv.LireFichier(Path.Combine(dossier, "locations.csv")));
        Assert.Equal((4, 0, 0), (locations.NombreAImporter, locations.NombreDejaPresentes, locations.NombreRejetees));
    }

    private static string TrouverExemples()
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier is not null; dossier = dossier.Parent)
        {
            var exemples = Path.Combine(dossier.FullName, "exemples-import");
            if (Directory.Exists(exemples)) return exemples;
        }
        throw new DirectoryNotFoundException("exemples-import");
    }
}
