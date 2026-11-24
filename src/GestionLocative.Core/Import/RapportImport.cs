namespace GestionLocative.Core.Import;

public enum TypeImport
{
    Clients,
    Locations,
}

public enum StatutLigne
{
    /// <summary>Ligne correcte, nouvelle : sera (ou a été) ajoutée.</summary>
    AImporter,

    /// <summary>Déjà dans la base ou en double dans le fichier : ignorée, sans erreur.</summary>
    DejaPresente,

    /// <summary>Données invalides : ignorée, la raison est dans le rapport.</summary>
    Rejetee,
}

/// <summary>Résultat d'une ligne du fichier, pour l'aperçu et le rapport.</summary>
/// <param name="Resume">Ce que contient la ligne (« DUPONT Jean · jean@exemple.fr »).</param>
/// <param name="Detail">Raison du rejet ou du doublon (vide si la ligne est importée).</param>
public sealed record LigneRapport(int NumeroLigne, StatutLigne Statut, string Resume, string Detail)
{
    public string StatutAffiche => Statut switch
    {
        StatutLigne.AImporter => "À importer",
        StatutLigne.DejaPresente => "Déjà présente",
        _ => "Rejetée",
    };
}

/// <summary>Aperçu (avant import) ou bilan (après import) d'un fichier CSV.</summary>
public sealed class RapportImport
{
    public required TypeImport Type { get; init; }
    public required TableauCsv Tableau { get; init; }

    /// <summary>« Nom ← NOM CLIENT » pour chaque champ reconnu.</summary>
    public required IReadOnlyList<string> ColonnesReconnues { get; init; }

    public required IReadOnlyList<string> ColonnesIgnorees { get; init; }

    /// <summary>Problème qui empêche tout l'import (colonne obligatoire absente...) ; null sinon.</summary>
    public string? ErreurFichier { get; init; }

    /// <summary>Points d'attention qui n'empêchent pas l'import (colonne n° client absente...).</summary>
    public IReadOnlyList<string> Avertissements { get; init; } = [];

    public required IReadOnlyList<LigneRapport> Lignes { get; init; }

    /// <summary>Vrai une fois les lignes enregistrées dans la base.</summary>
    public bool Enregistre { get; init; }

    public int NombreAImporter => Lignes.Count(l => l.Statut == StatutLigne.AImporter);
    public int NombreDejaPresentes => Lignes.Count(l => l.Statut == StatutLigne.DejaPresente);
    public int NombreRejetees => Lignes.Count(l => l.Statut == StatutLigne.Rejetee);

    public string Objet => Type == TypeImport.Clients ? "client" : "location";

    /// <summary>« 120 clients à importer, 3 déjà présents, 2 lignes rejetées. »</summary>
    public string Resume
    {
        get
        {
            if (ErreurFichier is not null) return ErreurFichier;
            var feminin = Type == TypeImport.Locations;
            var parties = new List<string>
            {
                $"{NombreAImporter} {Pluriel(Objet, NombreAImporter)} {(Enregistre ? Pluriel(feminin ? "importée" : "importé", NombreAImporter) : "à importer")}",
            };
            if (NombreDejaPresentes > 0)
                parties.Add($"{NombreDejaPresentes} {Pluriel(feminin ? "déjà présente" : "déjà présent", NombreDejaPresentes)} (ignoré{(feminin ? "e" : "")}{(NombreDejaPresentes > 1 ? "s" : "")})");
            if (NombreRejetees > 0)
                parties.Add($"{NombreRejetees} {Pluriel("ligne rejetée", NombreRejetees)}");
            return string.Join(", ", parties) + ".";
        }
    }

    /// <summary>« Séparateur « ; », encodage Windows-1252, 125 lignes. »</summary>
    public string DescriptionFichier =>
        $"Séparateur « {(Tableau.Separateur == '\t' ? "tabulation" : Tableau.Separateur.ToString())} », " +
        $"encodage {Tableau.Encodage}, {Tableau.Lignes.Count} {Pluriel("ligne", Tableau.Lignes.Count)} de données.";

    /// <summary>Rapport complet en texte (à copier ou enregistrer).</summary>
    public string Texte()
    {
        var lignes = new List<string>
        {
            $"Import des {Objet}s : {Resume}",
            DescriptionFichier,
            "",
            "Colonnes reconnues :",
        };
        lignes.AddRange(ColonnesReconnues.Select(c => "  " + c));
        if (ColonnesIgnorees.Count > 0) lignes.Add($"Colonnes ignorées : {string.Join(", ", ColonnesIgnorees)}");
        foreach (var avertissement in Avertissements) lignes.Add("Attention : " + avertissement);

        foreach (var (statut, titre) in new[] { (StatutLigne.Rejetee, "Lignes rejetées"), (StatutLigne.DejaPresente, "Lignes déjà présentes") })
        {
            var concernees = Lignes.Where(l => l.Statut == statut).ToList();
            if (concernees.Count == 0) continue;
            lignes.Add("");
            lignes.Add($"{titre} :");
            lignes.AddRange(concernees.Select(l => $"  Ligne {l.NumeroLigne} : {l.Resume} — {l.Detail}"));
        }
        return string.Join(Environment.NewLine, lignes);
    }

    /// <summary>« client » → « clients » au-delà de 1 (chaque mot d'une expression courte).</summary>
    private static string Pluriel(string mots, int nombre) =>
        nombre > 1 ? string.Join(' ', mots.Split(' ').Select(m => m is "déjà" ? m : m + "s")) : mots;
}
