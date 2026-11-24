namespace GestionLocative.Core.Recapitulatif;

/// <summary>Colonne d'un tableau à imprimer : libellé de l'en-tête, et alignement à droite pour les nombres.</summary>
public sealed record ColonneImprimable(string Libelle, bool ADroite = false, double Largeur = 1);

/// <summary>
/// Tableau prêt à imprimer (ou à enregistrer en PDF avec l'imprimante « Microsoft Print to PDF »),
/// indépendant de WPF : l'application le met en page, les tests peuvent en vérifier le contenu.
/// </summary>
public sealed record TableauImprimable(
    string Titre,
    IReadOnlyList<string> SousTitres,
    IReadOnlyList<ColonneImprimable> Colonnes,
    IReadOnlyList<IReadOnlyList<string>> Lignes,
    IReadOnlyList<string>? Total,
    string? Note = null);
