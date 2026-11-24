namespace GestionLocative.Core.Calendar;

/// <summary>
/// Calcule la grille d'affichage d'un mois : semaines complètes commençant le lundi.
/// Les jours des mois voisins complètent la première et la dernière semaine.
/// </summary>
public static class MonthGrid
{
    /// <summary>Retourne les jours à afficher pour le mois donné, du lundi de la première semaine au dimanche de la dernière.</summary>
    public static IReadOnlyList<DateOnly> GetDays(int annee, int mois)
    {
        var premier = new DateOnly(annee, mois, 1);
        var dernier = premier.AddMonths(1).AddDays(-1);

        var debut = premier.AddDays(-DaysSinceMonday(premier.DayOfWeek));
        var fin = dernier.AddDays(6 - DaysSinceMonday(dernier.DayOfWeek));

        var jours = new List<DateOnly>(42);
        for (var jour = debut; jour <= fin; jour = jour.AddDays(1))
        {
            jours.Add(jour);
        }
        return jours;
    }

    /// <summary>0 pour lundi, 6 pour dimanche.</summary>
    public static int DaysSinceMonday(DayOfWeek jour) => ((int)jour + 6) % 7;
}
