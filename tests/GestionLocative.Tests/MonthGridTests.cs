using GestionLocative.Core.Calendar;

namespace GestionLocative.Tests;

public class MonthGridTests
{
    [Theory]
    [InlineData(2026, 11)]
    [InlineData(2026, 2)]
    [InlineData(2026, 6)]
    [InlineData(2024, 2)]
    public void Les_semaines_commencent_le_lundi_et_finissent_le_dimanche(int annee, int mois)
    {
        var jours = MonthGrid.GetDays(annee, mois);

        Assert.Equal(DayOfWeek.Monday, jours[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, jours[^1].DayOfWeek);
        Assert.Equal(0, jours.Count % 7);
    }

    [Theory]
    [InlineData(2026, 11)]
    [InlineData(2026, 2)]
    [InlineData(2025, 12)]
    public void Tous_les_jours_du_mois_sont_presents_une_seule_fois(int annee, int mois)
    {
        var jours = MonthGrid.GetDays(annee, mois);
        var duMois = jours.Where(j => j.Year == annee && j.Month == mois).ToList();

        Assert.Equal(DateTime.DaysInMonth(annee, mois), duMois.Count);
        Assert.Equal(duMois.Distinct().Count(), duMois.Count);
    }

    [Fact]
    public void Novembre_2026_commence_le_lundi_26_octobre()
    {
        // Le 1er novembre 2026 est un dimanche.
        var jours = MonthGrid.GetDays(2026, 11);

        Assert.Equal(new DateOnly(2026, 10, 26), jours[0]);
        Assert.Equal(new DateOnly(2026, 12, 6), jours[^1]);
        Assert.Equal(6, jours.Count / 7);
    }

    [Fact]
    public void Fevrier_2027_tient_sur_quatre_semaines()
    {
        // Le 1er février 2027 est un lundi, février compte 28 jours.
        var jours = MonthGrid.GetDays(2027, 2);

        Assert.Equal(28, jours.Count);
    }
}
