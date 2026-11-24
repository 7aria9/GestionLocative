using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Calendar;

/// <summary>
/// Affiche les locations de la base dans le calendrier : « Arrivée NOM » le premier jour,
/// « Départ NOM » le dernier jour et le nom du client les jours intermédiaires.
/// </summary>
/// <param name="baseCourante">Base à lire (relue à chaque appel, car elle peut changer dans les paramètres).</param>
public sealed class LocationsCalendarSource(Func<BaseDeDonnees> baseCourante) : ICalendarEntrySource
{
    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly du, DateOnly au)
    {
        var baseDeDonnees = baseCourante();
        var locations = new LocationRepository(baseDeDonnees).ListerEntre(du, au);
        if (locations.Count == 0) return [];

        var clients = new ClientRepository(baseDeDonnees);
        var noms = locations.Select(l => l.IdClient).Distinct()
            .ToDictionary(id => id, id => clients.ObtenirParId(id)?.NomComplet ?? "Client inconnu");

        var entrees = new List<CalendarEntry>();
        foreach (var location in locations)
        {
            var nom = noms[location.IdClient];
            var arrivee = DateOnly.FromDateTime(location.DateDebut);
            var depart = DateOnly.FromDateTime(location.DateFin);
            var premier = arrivee > du ? arrivee : du;
            var dernier = depart < au ? depart : au;
            for (var jour = premier; jour <= dernier; jour = jour.AddDays(1))
            {
                var libelle = jour == arrivee ? $"Arrivée {nom}"
                    : jour == depart ? $"Départ {nom}"
                    : nom;
                entrees.Add(new CalendarEntry(jour, libelle, location.IdLocation));
            }
        }
        return entrees;
    }
}
