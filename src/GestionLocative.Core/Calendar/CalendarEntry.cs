namespace GestionLocative.Core.Calendar;

/// <summary>
/// Élément affiché dans la vignette d'un jour (location, arrivée, départ...).
/// </summary>
/// <param name="Date">Jour auquel l'élément est rattaché.</param>
/// <param name="Libelle">Texte court affiché dans la vignette.</param>
/// <param name="IdLocation">Location concernée, le cas échéant.</param>
public sealed record CalendarEntry(DateOnly Date, string Libelle, long? IdLocation = null);
