using GestionLocative.Core.Calendar;
using GestionLocative.Core.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace GestionLocative.Tests;

public class CalendarViewModelTests
{
    private static CalendarViewModel Creer(DateTimeOffset maintenant, ICalendarEntrySource? source = null)
    {
        var horloge = new FakeTimeProvider(maintenant);
        horloge.SetLocalTimeZone(TimeZoneInfo.Utc);
        return new CalendarViewModel(horloge, source ?? new EmptyCalendarEntrySource());
    }

    [Fact]
    public void Affiche_le_mois_en_cours_au_demarrage()
    {
        var vm = Creer(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 1), vm.MoisAffiche);
        Assert.Equal("Novembre 2026", vm.Titre);
        Assert.Single(vm.Jours, j => j.EstAujourdhui);
        Assert.Equal(new DateOnly(2026, 11, 24), vm.Jours.Single(j => j.EstAujourdhui).Date);
    }

    [Fact]
    public void Navigation_entre_les_mois_y_compris_changement_d_annee()
    {
        var vm = Creer(new DateTimeOffset(2026, 12, 15, 10, 0, 0, TimeSpan.Zero));

        vm.MoisSuivantCommand.Execute(null);
        Assert.Equal("Janvier 2027", vm.Titre);

        vm.MoisPrecedentCommand.Execute(null);
        vm.MoisPrecedentCommand.Execute(null);
        Assert.Equal("Novembre 2026", vm.Titre);
        Assert.DoesNotContain(vm.Jours, j => j.EstAujourdhui && j.EstDansLeMois);
    }

    [Fact]
    public void Les_jours_hors_du_mois_sont_marques()
    {
        var vm = Creer(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

        Assert.False(vm.Jours[0].EstDansLeMois); // lundi 26 octobre
        Assert.True(vm.Jours[6].EstDansLeMois);  // dimanche 1er novembre
        Assert.Equal(30, vm.Jours.Count(j => j.EstDansLeMois));
        Assert.Equal(6, vm.NombreSemaines);
    }

    [Fact]
    public void Les_entrees_de_la_source_sont_placees_dans_la_bonne_vignette()
    {
        var source = new SourceFixe(new CalendarEntry(new DateOnly(2026, 11, 10), "Arrivée Dupont"));
        var vm = Creer(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero), source);

        var vignette = vm.Jours.Single(j => j.Date == new DateOnly(2026, 11, 10));
        Assert.Equal("Arrivée Dupont", Assert.Single(vignette.Entrees).Libelle);
        Assert.Equal(1, vm.Jours.Sum(j => j.Entrees.Count));
    }

    private sealed class SourceFixe(params CalendarEntry[] entrees) : ICalendarEntrySource
    {
        public IReadOnlyList<CalendarEntry> GetEntries(DateOnly du, DateOnly au) =>
            entrees.Where(e => e.Date >= du && e.Date <= au).ToList();
    }
}
