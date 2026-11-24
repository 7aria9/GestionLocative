using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

public class MiseEnFormeTests
{
    [Theory]
    [InlineData("dupont", "DUPONT")]
    [InlineData("  le   bris ", "LE BRIS")]
    [InlineData("bénard", "BÉNARD")]
    public void Nom_en_majuscules(string saisie, string attendu) => Assert.Equal(attendu, MiseEnForme.Nom(saisie));

    [Theory]
    [InlineData("jean", "Jean")]
    [InlineData("JEAN-PIERRE", "Jean-Pierre")]
    [InlineData("marie  claire", "Marie Claire")]
    [InlineData("élodie", "Élodie")]
    [InlineData("d'artagnan", "D'Artagnan")]
    public void Prenom_avec_majuscule_initiale(string saisie, string attendu) => Assert.Equal(attendu, MiseEnForme.Prenom(saisie));

    [Theory]
    [InlineData("j", "J")]
    [InlineData("jEAN", "Jean")]
    [InlineData("jean-p", "Jean-P")]
    [InlineData("marie ", "Marie ")]
    [InlineData("marie c", "Marie C")]
    [InlineData("", "")]
    public void Prenom_pendant_la_saisie_garde_la_longueur(string saisie, string attendu) =>
        Assert.Equal(attendu, MiseEnForme.PrenomSaisie(saisie));

    [Theory]
    [InlineData("06 12 34 56 78", "+33612345678")]
    [InlineData("06.12.34.56.78", "+33612345678")]
    [InlineData("0612345678", "+33612345678")]
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    [InlineData("+33 (0)6 12 34 56 78", "+33612345678")]
    [InlineData("+33 06 12 34 56 78", "+33612345678")]
    [InlineData("0033 6 12 34 56 78", "+33612345678")]
    [InlineData("02 40 12 34 56", "+33240123456")]
    [InlineData("0692 12 34 56", "+262692123456")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("0041 22 123 45 67", "+41221234567")]
    public void Telephone_au_format_international(string saisie, string attendu) =>
        Assert.Equal(attendu, MiseEnForme.Telephone(saisie));

    [Theory]
    [InlineData("")]
    [InlineData("0612")]
    [InlineData("06123456789")]
    [InlineData("6 12 34 56 78")]
    [InlineData("+33 6 12 34")]
    [InlineData("06 12 AB 56 78")]
    [InlineData("+1234")]
    public void Telephone_non_reconnu(string saisie) => Assert.Null(MiseEnForme.Telephone(saisie));

    [Theory]
    [InlineData("+33612345678", "+33 6 12 34 56 78")]
    [InlineData("06 12 34 56 78", "+33 6 12 34 56 78")]
    [InlineData("+33240123456", "+33 2 40 12 34 56")]
    [InlineData("+262692123456", "+262 692 12 34 56")]
    [InlineData("+590690123456", "+590 690 12 34 56")]
    [InlineData("+442079460958", "+442079460958")]
    [InlineData("pas un numéro", "pas un numéro")]
    [InlineData("", "")]
    public void Telephone_affiche_espace(string enregistre, string attendu) =>
        Assert.Equal(attendu, MiseEnForme.TelephoneAffiche(enregistre));

    [Theory]
    [InlineData("jean.dupont@example.com", "jean.dupont@example.com")]
    [InlineData(" Jean.Dupont@Example.COM ", "jean.dupont@example.com")]
    [InlineData("a+location@sous.domaine.fr", "a+location@sous.domaine.fr")]
    public void Email_valide(string saisie, string attendu) => Assert.Equal(attendu, MiseEnForme.Email(saisie));

    [Theory]
    [InlineData("pas-un-email")]
    [InlineData("jean@exemple")]
    [InlineData("jean@@exemple.fr")]
    [InlineData("jean dupont@exemple.fr")]
    [InlineData("jean..dupont@exemple.fr")]
    [InlineData(".jean@exemple.fr")]
    [InlineData("jean@exemple.f")]
    [InlineData("jean@exemple.123")]
    [InlineData("jean@-exemple.fr")]
    [InlineData("@exemple.fr")]
    [InlineData("jean@exemple.fr.")]
    public void Email_invalide(string saisie) => Assert.Null(MiseEnForme.Email(saisie));
}
