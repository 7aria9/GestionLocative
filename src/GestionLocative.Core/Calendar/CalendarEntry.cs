namespace GestionLocative.Core.Calendar;

/// <summary>
/// Élément affiché dans la vignette d'un jour (location, arrivée, départ...).
/// </summary>
/// <param name="Date">Jour auquel l'élément est rattaché.</param>
/// <param name="Libelle">Texte court affiché dans la vignette (première ligne, en gras).</param>
/// <param name="IdLocation">Location concernée, le cas échéant.</param>
public sealed record CalendarEntry(DateOnly Date, string Libelle, long? IdLocation = null)
{
    /// <summary>Lignes de détail affichées sous le libellé (vide pour un affichage compact).</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    /// <summary>Texte complet affiché au survol.</summary>
    public string? Infobulle { get; init; }

    /// <summary>
    /// Numéro de couleur (0 à <see cref="NombreCouleurs"/> - 1). Tous les jours d'une même location ont la même couleur,
    /// deux locations qui se suivent ont des couleurs différentes.
    /// </summary>
    public int IndexCouleur { get; init; }

    /// <summary>Nombre de couleurs de la palette du calendrier.</summary>
    public const int NombreCouleurs = 10;
}
