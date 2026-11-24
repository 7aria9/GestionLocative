using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Calendar;

namespace GestionLocative.Core.ViewModels;

/// <summary>Écran principal : calendrier d'un mois avec navigation mois précédent / suivant.</summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    private readonly TimeProvider _horloge;
    private readonly ICalendarEntrySource _source;

    public CalendarViewModel()
        : this(TimeProvider.System, new EmptyCalendarEntrySource())
    {
    }

    public CalendarViewModel(TimeProvider horloge, ICalendarEntrySource source)
    {
        _horloge = horloge;
        _source = source;

        var aujourdhui = Aujourdhui();
        _moisAffiche = new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
        Recharger();
    }

    /// <summary>En-têtes des colonnes, du lundi au dimanche.</summary>
    public IReadOnlyList<string> JoursSemaine { get; } = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"];

    /// <summary>Vignettes à afficher, semaine par semaine (7 par ligne).</summary>
    public ObservableCollection<DayTileViewModel> Jours { get; } = [];

    /// <summary>Premier jour du mois affiché.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titre))]
    private DateOnly _moisAffiche;

    /// <summary>Ex. « Novembre 2026 ».</summary>
    public string Titre
    {
        get
        {
            var texte = MoisAffiche.ToString("MMMM yyyy", Francais);
            return Francais.TextInfo.ToUpper(texte[0]) + texte[1..];
        }
    }

    /// <summary>Nombre de semaines affichées (4 à 6 selon le mois).</summary>
    public int NombreSemaines => Jours.Count / 7;

    [RelayCommand]
    private void MoisPrecedent() => MoisAffiche = MoisAffiche.AddMonths(-1);

    [RelayCommand]
    private void MoisSuivant() => MoisAffiche = MoisAffiche.AddMonths(1);

    partial void OnMoisAfficheChanged(DateOnly value) => Recharger();

    private void Recharger()
    {
        var aujourdhui = Aujourdhui();
        var dates = MonthGrid.GetDays(MoisAffiche.Year, MoisAffiche.Month);
        var entrees = _source.GetEntries(dates[0], dates[^1]).ToLookup(e => e.Date);

        Jours.Clear();
        foreach (var date in dates)
        {
            Jours.Add(new DayTileViewModel(
                date,
                estDansLeMois: date.Month == MoisAffiche.Month,
                estAujourdhui: date == aujourdhui,
                entrees[date]));
        }
        OnPropertyChanged(nameof(NombreSemaines));
    }

    private DateOnly Aujourdhui() => DateOnly.FromDateTime(_horloge.GetLocalNow().DateTime);
}
