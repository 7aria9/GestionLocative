namespace GestionLocative.Core.Calendar;

/// <summary>
/// Fournit les éléments à afficher sur le calendrier pour une période donnée.
/// Point d'extension pour brancher plus tard les réservations / locations.
/// </summary>
public interface ICalendarEntrySource
{
    /// <summary>Retourne les éléments dont la date est comprise entre <paramref name="du"/> et <paramref name="au"/> inclus.</summary>
    IReadOnlyList<CalendarEntry> GetEntries(DateOnly du, DateOnly au);
}

/// <summary>Source vide utilisée tant qu'aucune donnée n'est branchée.</summary>
public sealed class EmptyCalendarEntrySource : ICalendarEntrySource
{
    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly du, DateOnly au) => [];
}
