using System.Collections.ObjectModel;
using GestionLocative.Core.Calendar;

namespace GestionLocative.Core.ViewModels;

/// <summary>Une vignette du calendrier, représentant un jour.</summary>
public sealed class DayTileViewModel
{
    public DayTileViewModel(DateOnly date, bool estDansLeMois, bool estAujourdhui, IEnumerable<CalendarEntry> entrees)
    {
        Date = date;
        EstDansLeMois = estDansLeMois;
        EstAujourdhui = estAujourdhui;
        Entrees = new ObservableCollection<CalendarEntry>(entrees);
    }

    public DateOnly Date { get; }

    public int Numero => Date.Day;

    /// <summary>Faux pour les jours du mois précédent / suivant qui complètent les semaines.</summary>
    public bool EstDansLeMois { get; }

    public bool EstAujourdhui { get; }

    public bool EstWeekEnd => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Éléments (réservations, locations...) à afficher dans la vignette.</summary>
    public ObservableCollection<CalendarEntry> Entrees { get; }
}
