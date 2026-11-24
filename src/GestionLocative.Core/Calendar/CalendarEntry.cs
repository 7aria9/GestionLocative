namespace GestionLocative.Core.Calendar;

/// <summary>
/// Élément affiché dans la vignette d'un jour (plus tard : réservation, location, arrivée, départ...).
/// </summary>
/// <param name="Date">Jour auquel l'élément est rattaché.</param>
/// <param name="Libelle">Texte court affiché dans la vignette.</param>
public sealed record CalendarEntry(DateOnly Date, string Libelle);
