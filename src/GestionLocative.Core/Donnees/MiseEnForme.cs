using System.Globalization;
using System.Net.Mail;
using System.Text;

namespace GestionLocative.Core.Donnees;

/// <summary>
/// Mise en forme imposée des fiches client : NOM en majuscules, Prénom avec une majuscule initiale,
/// téléphone au format international (+33612345678), email en minuscules.
/// </summary>
public static class MiseEnForme
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>
    /// Numéros français d'outre-mer : préfixe national → indicatif international
    /// (les autres numéros en 0X sont en France métropolitaine, +33).
    /// </summary>
    private static readonly (string Prefixe, string Indicatif)[] OutreMer =
    [
        ("0262", "262"), ("0263", "262"), ("0692", "262"), ("0693", "262"), // La Réunion
        ("0269", "262"), ("0639", "262"),                                   // Mayotte
        ("0590", "590"), ("0690", "590"), ("0691", "590"),                  // Guadeloupe, St-Martin, St-Barthélemy
        ("0594", "594"), ("0694", "594"),                                   // Guyane
        ("0596", "596"), ("0696", "596"), ("0697", "596"),                  // Martinique
    ];

    /// <summary>« dupont  de la tour » → « DUPONT DE LA TOUR ».</summary>
    public static string Nom(string? nom) => Espaces(nom).ToUpper(Francais);

    /// <summary>« jean-PIERRE » → « Jean-Pierre », « marie claire » → « Marie Claire », « d'arc » → « D'Arc ».</summary>
    public static string Prenom(string? prenom)
    {
        var texte = Espaces(prenom).ToLower(Francais);
        var resultat = new StringBuilder(texte.Length);
        var debutDeMot = true;
        foreach (var c in texte)
        {
            resultat.Append(debutDeMot ? char.ToUpper(c, Francais) : c);
            debutDeMot = c is ' ' or '-' or '\'' or '’';
        }
        return resultat.ToString();
    }

    /// <summary>
    /// Version « pendant la saisie » de <see cref="Prenom"/> : même longueur que le texte tapé (espaces conservés,
    /// pour ne pas déplacer le curseur), majuscule en début de mot, minuscules ensuite.
    /// </summary>
    public static string PrenomSaisie(string? prenom)
    {
        var texte = prenom ?? "";
        var resultat = new StringBuilder(texte.Length);
        var debutDeMot = true;
        foreach (var c in texte)
        {
            resultat.Append(debutDeMot ? char.ToUpper(c, Francais) : char.ToLower(c, Francais));
            debutDeMot = c is ' ' or '-' or '\'' or '’';
        }
        return resultat.ToString();
    }

    /// <summary>
    /// Convertit un numéro au format international E.164 : « 06 12 34 56 78 » → « +33612345678 »,
    /// « 0044 20 7946 0958 » → « +442079460958 ». Retourne null si le numéro n'est pas reconnu.
    /// </summary>
    public static string? Telephone(string? telephone)
    {
        if (string.IsNullOrWhiteSpace(telephone)) return null;

        // « +33 (0)6 12... » : le 0 entre parenthèses ne se compose pas depuis l'étranger.
        var texte = telephone.Trim().Replace("(0)", "");
        var chiffres = new StringBuilder();
        for (var i = 0; i < texte.Length; i++)
        {
            var c = texte[i];
            if (char.IsAsciiDigit(c)) chiffres.Append(c);
            else if (c == '+' && i == 0) chiffres.Append(c);
            else if (c is not (' ' or '.' or '-' or '/' or '(' or ')' or ' ' or ' ')) return null;
        }
        var numero = chiffres.ToString();

        if (numero.StartsWith("00")) numero = "+" + numero[2..];

        if (!numero.StartsWith('+'))
        {
            // Numéro national français : 10 chiffres commençant par 0.
            if (numero.Length != 10 || numero[0] != '0' || numero[1] == '0') return null;
            var outreMer = OutreMer.FirstOrDefault(o => numero.StartsWith(o.Prefixe));
            return outreMer.Indicatif is null ? "+33" + numero[1..] : "+" + outreMer.Indicatif + numero[1..];
        }

        var international = numero[1..];
        // « +33 06... » : le 0 national ajouté par erreur après l'indicatif.
        if (international.StartsWith("330") && international.Length == 12) international = "33" + international[3..];
        if (international.StartsWith("33") && international.Length != 11) return null;
        if (international.Length is < 8 or > 15 || international[0] == '0') return null;
        return "+" + international;
    }

    /// <summary>
    /// Affichage espacé d'un numéro international : « +33612345678 » → « +33 6 12 34 56 78 »,
    /// « +262692123456 » → « +262 692 12 34 56 ». Les numéros étrangers (regroupement propre à chaque pays)
    /// restent sans espaces : « +442079460958 ».
    /// </summary>
    public static string TelephoneAffiche(string? telephone)
    {
        if (string.IsNullOrWhiteSpace(telephone)) return "";
        var numero = Telephone(telephone);
        if (numero is null) return telephone.Trim();

        var chiffres = numero[1..];
        foreach (var indicatif in OutreMer.Select(o => o.Indicatif).Distinct().Append("33"))
        {
            if (!chiffres.StartsWith(indicatif)) continue;
            var national = chiffres[indicatif.Length..];
            // France : 9 chiffres après l'indicatif → « 6 12 34 56 78 » ; DOM : « 692 12 34 56 ».
            var tete = indicatif == "33" ? 1 : 3;
            if (indicatif != "33" && national.Length != 9) break;
            var groupes = new List<string> { national[..tete] };
            for (var i = tete; i < national.Length; i += 2) groupes.Add(national.Substring(i, Math.Min(2, national.Length - i)));
            return $"+{indicatif} {string.Join(' ', groupes)}";
        }
        return numero;
    }

    /// <summary>Email en minuscules sans espaces autour ; null si ce n'est pas une adresse email valide.</summary>
    public static string? Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var texte = email.Trim().ToLowerInvariant();
        if (texte.Any(char.IsWhiteSpace) || texte.Contains("..")) return null;

        var arobase = texte.IndexOf('@');
        if (arobase <= 0 || arobase != texte.LastIndexOf('@')) return null;
        var local = texte[..arobase];
        var domaine = texte[(arobase + 1)..];
        if (local.StartsWith('.') || local.EndsWith('.')) return null;

        // Domaine : au moins un point, parties non vides en lettres / chiffres / tirets, extension d'au moins 2 lettres.
        var parties = domaine.Split('.');
        if (parties.Length < 2) return null;
        foreach (var partie in parties)
        {
            if (partie.Length == 0 || partie.StartsWith('-') || partie.EndsWith('-')) return null;
            if (!partie.All(c => char.IsLetterOrDigit(c) || c == '-')) return null;
        }
        if (parties[^1].Length < 2 || !parties[^1].All(char.IsLetter)) return null;

        return MailAddress.TryCreate(texte, out var adresse) && adresse.Address == texte ? texte : null;
    }

    /// <summary>Retire les espaces en trop (début, fin, doublons).</summary>
    private static string Espaces(string? texte) =>
        string.Join(' ', (texte ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
