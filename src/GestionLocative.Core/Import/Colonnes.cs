using System.Globalization;
using System.Text;

namespace GestionLocative.Core.Import;

/// <summary>
/// Champ de la base alimenté par une colonne du fichier CSV.
/// La correspondance se fait sur le nom de la colonne, sans tenir compte des majuscules, des accents,
/// des espaces ni de la ponctuation : « N° réservation », « num_reservation » et « NumReservation » se valent.
/// </summary>
/// <param name="Libelle">Nom affiché dans le rapport.</param>
/// <param name="Synonymes">Noms de colonne reconnus (déjà simplifiés : minuscules, sans accent ni ponctuation).</param>
/// <param name="Debuts">Débuts de nom reconnus en second choix (ex. « tel » pour « Tél. portable »).</param>
public sealed record Champ(string Libelle, string[] Synonymes, string[] Debuts)
{
    public Champ(string libelle, params string[] synonymes) : this(libelle, synonymes, []) { }
}

/// <summary>Colonnes du fichier reconnues pour chaque champ, et colonnes ignorées.</summary>
public sealed class CorrespondanceColonnes
{
    private readonly Dictionary<Champ, List<int>> _colonnes;

    private CorrespondanceColonnes(Dictionary<Champ, List<int>> colonnes, IReadOnlyList<string> entetes, IReadOnlyList<string> ignorees)
    {
        _colonnes = colonnes;
        Entetes = entetes;
        Ignorees = ignorees;
    }

    public IReadOnlyList<string> Entetes { get; }

    /// <summary>En-têtes qui ne correspondent à aucun champ (leur contenu n'est pas importé).</summary>
    public IReadOnlyList<string> Ignorees { get; }

    public bool Contient(Champ champ) => _colonnes.ContainsKey(champ);

    /// <summary>« Nom ← NOM CLIENT », pour le rapport.</summary>
    public IEnumerable<string> Description(IEnumerable<Champ> champs) =>
        champs.Where(Contient).Select(c => $"{c.Libelle} ← {string.Join(" / ", _colonnes[c].Select(i => Entetes[i]))}");

    /// <summary>Première valeur non vide parmi les colonnes du champ (null si absente ou vide).</summary>
    public string? Valeur(LigneCsv ligne, Champ champ)
    {
        if (!_colonnes.TryGetValue(champ, out var indices)) return null;
        foreach (var i in indices)
        {
            var valeur = ligne.Valeurs[i].Trim();
            if (valeur.Length > 0) return valeur;
        }
        return null;
    }

    /// <summary>
    /// Associe chaque en-tête à un champ : d'abord les noms exacts (synonymes), puis les débuts de nom.
    /// Une colonne ne sert qu'à un seul champ ; plusieurs colonnes peuvent alimenter le même champ
    /// (ex. « Tél. fixe » et « Portable » : la première non vide est retenue).
    /// </summary>
    public static CorrespondanceColonnes Etablir(IReadOnlyList<string> entetes, IReadOnlyList<Champ> champs)
    {
        var colonnes = new Dictionary<Champ, List<int>>();
        var simplifies = entetes.Select(Simplifier).ToArray();
        var attribuees = new bool[entetes.Count];

        void Attribuer(Func<Champ, string, bool> correspond)
        {
            for (var i = 0; i < simplifies.Length; i++)
            {
                if (attribuees[i] || simplifies[i].Length == 0) continue;
                var champ = champs.FirstOrDefault(c => correspond(c, simplifies[i]));
                if (champ is null) continue;
                attribuees[i] = true;
                if (!colonnes.TryGetValue(champ, out var liste)) colonnes[champ] = liste = [];
                liste.Add(i);
            }
        }

        Attribuer((c, nom) => c.Synonymes.Contains(nom));
        Attribuer((c, nom) => c.Debuts.Any(nom.StartsWith));

        var ignorees = entetes.Where((_, i) => !attribuees[i] && entetes[i].Trim().Length > 0).ToList();
        return new CorrespondanceColonnes(colonnes, entetes, ignorees);
    }

    /// <summary>« N° Réservation » → « nreservation », « E-mail » → « email ».</summary>
    public static string Simplifier(string entete)
    {
        var decompose = entete.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultat = new StringBuilder(decompose.Length);
        foreach (var c in decompose)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) resultat.Append(c);
            else if (c is 'œ') resultat.Append("oe");
        }
        return resultat.ToString();
    }
}
