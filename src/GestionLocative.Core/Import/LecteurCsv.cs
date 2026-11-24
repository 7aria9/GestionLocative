using System.Text;

namespace GestionLocative.Core.Import;

/// <summary>Une ligne de données du fichier CSV.</summary>
/// <param name="NumeroLigne">Numéro de la ligne dans le fichier (1 = ligne des en-têtes), pour le rapport.</param>
/// <param name="Valeurs">Une valeur par colonne d'en-tête (complétée par des vides si la ligne est plus courte).</param>
/// <param name="ValeursEnTrop">Nombre de valeurs non vides au-delà de la dernière colonne (séparateur présent dans une valeur non entourée de guillemets ?).</param>
public sealed record LigneCsv(int NumeroLigne, IReadOnlyList<string> Valeurs, int ValeursEnTrop);

/// <summary>Contenu d'un fichier CSV : en-têtes, lignes, et la façon dont il a été lu.</summary>
public sealed record TableauCsv(
    IReadOnlyList<string> Entetes,
    IReadOnlyList<LigneCsv> Lignes,
    char Separateur,
    string Encodage);

/// <summary>
/// Lecture des fichiers CSV exportés par Excel, WinDev, LibreOffice ou un tableur en ligne :
/// séparateur « ; », « , » ou tabulation détecté sur la ligne des en-têtes (ou imposé par une première ligne « sep=; »),
/// encodage UTF-8 (avec ou sans BOM) ou Windows-1252 (Excel / WinDev français), valeurs entre guillemets
/// pouvant contenir le séparateur, des guillemets doublés ("") ou des retours à la ligne.
/// </summary>
public static class LecteurCsv
{
    static LecteurCsv() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <exception cref="FormatException">Fichier vide ou sans ligne d'en-têtes.</exception>
    public static TableauCsv LireFichier(string chemin) => Lire(File.ReadAllBytes(chemin));

    /// <exception cref="FormatException">Fichier vide ou sans ligne d'en-têtes.</exception>
    public static TableauCsv Lire(byte[] octets)
    {
        var (texte, encodage) = Decoder(octets);
        return LireTexte(texte, encodage);
    }

    /// <summary>
    /// UTF-8 si le fichier commence par un BOM ou s'il est entièrement valide en UTF-8,
    /// sinon Windows-1252 (« é » enregistré sur un seul octet par Excel ou WinDev).
    /// </summary>
    internal static (string Texte, string Encodage) Decoder(byte[] octets)
    {
        if (octets.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
            return (Encoding.UTF8.GetString(octets, 3, octets.Length - 3), "UTF-8");
        if (octets.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
            return (Encoding.Unicode.GetString(octets, 2, octets.Length - 2), "UTF-16");
        if (octets.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
            return (Encoding.BigEndianUnicode.GetString(octets, 2, octets.Length - 2), "UTF-16");

        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return (utf8Strict.GetString(octets), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(1252).GetString(octets), "Windows-1252");
        }
    }

    internal static TableauCsv LireTexte(string texte, string encodage = "UTF-8")
    {
        texte = texte.TrimStart('﻿');
        var debut = 0;
        var premiereLigne = 1;
        char? separateurImpose = null;

        // Excel accepte une première ligne « sep=; » qui indique le séparateur.
        var finPremiereLigne = texte.IndexOfAny(['\r', '\n']);
        var premiere = finPremiereLigne < 0 ? texte : texte[..finPremiereLigne];
        if (premiere.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && premiere.Trim().Length == 5)
        {
            separateurImpose = premiere.Trim()[4];
            debut = finPremiereLigne < 0 ? texte.Length : finPremiereLigne;
            if (debut < texte.Length && texte[debut] == '\r') debut++;
            if (debut < texte.Length && texte[debut] == '\n') debut++;
            premiereLigne = 2;
        }

        var separateur = separateurImpose ?? DetecterSeparateur(texte, debut);
        var enregistrements = Decouper(texte, debut, premiereLigne, separateur)
            .Where(e => e.Valeurs.Any(v => !string.IsNullOrWhiteSpace(v)))
            .ToList();
        if (enregistrements.Count == 0)
            throw new FormatException("Le fichier est vide.");

        var entetes = enregistrements[0].Valeurs.Select(v => v.Trim()).ToList();
        // Colonnes vides en fin d'en-tête (« Nom;Prénom;; » exporté par Excel) : ignorées.
        while (entetes.Count > 0 && entetes[^1].Length == 0) entetes.RemoveAt(entetes.Count - 1);
        if (entetes.Count == 0)
            throw new FormatException("La première ligne du fichier doit contenir les noms des colonnes.");

        var lignes = new List<LigneCsv>(enregistrements.Count - 1);
        foreach (var (numero, valeurs) in enregistrements.Skip(1))
        {
            var cellules = new string[entetes.Count];
            for (var i = 0; i < cellules.Length; i++) cellules[i] = i < valeurs.Count ? valeurs[i] : "";
            var enTrop = valeurs.Skip(entetes.Count).Count(v => !string.IsNullOrWhiteSpace(v));
            lignes.Add(new LigneCsv(numero, cellules, enTrop));
        }
        return new TableauCsv(entetes, lignes, separateur, encodage);
    }

    /// <summary>Séparateur le plus fréquent hors guillemets sur la ligne des en-têtes (« ; » par défaut).</summary>
    private static char DetecterSeparateur(string texte, int debut)
    {
        int pointVirgule = 0, virgule = 0, tabulation = 0;
        var entreGuillemets = false;
        for (var i = debut; i < texte.Length; i++)
        {
            var c = texte[i];
            if (c == '"') entreGuillemets = !entreGuillemets;
            else if (!entreGuillemets && c is '\r' or '\n')
            {
                if (pointVirgule + virgule + tabulation > 0) break;
                continue; // lignes vides au début
            }
            else if (!entreGuillemets && c == ';') pointVirgule++;
            else if (!entreGuillemets && c == ',') virgule++;
            else if (!entreGuillemets && c == '\t') tabulation++;
        }
        if (tabulation > pointVirgule && tabulation > virgule) return '\t';
        return virgule > pointVirgule ? ',' : ';';
    }

    /// <summary>Découpage RFC 4180 tolérant : guillemets doublés, retours à la ligne entre guillemets, fins de ligne CRLF / LF / CR.</summary>
    private static IEnumerable<(int Numero, List<string> Valeurs)> Decouper(string texte, int debut, int premiereLigne, char separateur)
    {
        var ligneCourante = premiereLigne;
        var numeroEnregistrement = premiereLigne;
        var valeurs = new List<string>();
        var valeur = new StringBuilder();
        var entreGuillemets = false;
        var i = debut;

        while (i < texte.Length)
        {
            var c = texte[i];
            if (entreGuillemets)
            {
                if (c == '"')
                {
                    if (i + 1 < texte.Length && texte[i + 1] == '"') { valeur.Append('"'); i += 2; continue; }
                    entreGuillemets = false;
                }
                else
                {
                    if (c == '\n' || (c == '\r' && (i + 1 >= texte.Length || texte[i + 1] != '\n'))) ligneCourante++;
                    valeur.Append(c);
                }
                i++;
                continue;
            }

            if (c == '"' && valeur.ToString().Trim().Length == 0)
            {
                valeur.Clear(); // espaces avant le guillemet ouvrant
                entreGuillemets = true;
            }
            else if (c == separateur)
            {
                valeurs.Add(valeur.ToString());
                valeur.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < texte.Length && texte[i + 1] == '\n') i++;
                valeurs.Add(valeur.ToString());
                valeur.Clear();
                yield return (numeroEnregistrement, valeurs);
                valeurs = [];
                ligneCourante++;
                numeroEnregistrement = ligneCourante;
            }
            else valeur.Append(c);
            i++;
        }

        if (valeur.Length > 0 || valeurs.Count > 0)
        {
            valeurs.Add(valeur.ToString());
            yield return (numeroEnregistrement, valeurs);
        }
    }
}
