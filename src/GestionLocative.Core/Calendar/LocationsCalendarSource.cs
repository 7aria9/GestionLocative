using System.Globalization;
using GestionLocative.Core.Donnees;

namespace GestionLocative.Core.Calendar;

/// <summary>
/// Affiche les locations de la base dans le calendrier, chacune avec sa couleur.
/// Le jour d'arrivée (ou le premier jour visible) et chaque lundi montrent le détail : client, nombre de personnes, email, prix HT et TTC ;
/// les autres jours montrent seulement le nom, avec le détail au survol.
/// </summary>
/// <param name="baseCourante">Base à lire (relue à chaque appel, car elle peut changer dans les paramètres).</param>
public sealed class LocationsCalendarSource(Func<BaseDeDonnees> baseCourante) : ICalendarEntrySource
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly du, DateOnly au)
    {
        var baseDeDonnees = baseCourante();
        var locations = new LocationRepository(baseDeDonnees).ListerEntre(du, au);
        if (locations.Count == 0) return [];

        var tauxTva = new ReglagesRepository(baseDeDonnees).ObtenirTauxTva();
        var depot = new ClientRepository(baseDeDonnees);
        var clients = locations.Select(l => l.IdClient).Distinct().ToDictionary(id => id, depot.ObtenirParId);

        var entrees = new List<CalendarEntry>();
        foreach (var location in locations)
        {
            var client = clients[location.IdClient];
            var nom = client?.NomComplet ?? "Client inconnu";
            var details = Details(location, client, tauxTva);
            var infobulle = Infobulle(location, nom, details);
            var couleur = (int)(location.IdLocation % CalendarEntry.NombreCouleurs);

            var arrivee = DateOnly.FromDateTime(location.DateDebut);
            var depart = DateOnly.FromDateTime(location.DateFin);
            var premier = arrivee > du ? arrivee : du;
            var dernier = depart < au ? depart : au;
            for (var jour = premier; jour <= dernier; jour = jour.AddDays(1))
            {
                var libelle = jour == arrivee ? $"Arrivée {nom}"
                    : jour == depart ? $"Départ {nom}"
                    : nom;
                // Détail complet le premier jour affiché et chaque lundi (début de ligne du calendrier),
                // mais pas le jour du départ, souvent partagé avec une arrivée.
                var complet = (jour == premier || jour.DayOfWeek == DayOfWeek.Monday) && jour != depart;
                entrees.Add(new CalendarEntry(jour, libelle, location.IdLocation)
                {
                    Details = complet ? details : [],
                    Infobulle = infobulle,
                    IndexCouleur = couleur,
                });
            }
        }
        return entrees;
    }

    private static IReadOnlyList<string> Details(Location location, Client? client, decimal tauxTva)
    {
        var personnes = location.NombrePersonnes switch
        {
            0 => "Personnes non renseignées",
            1 => "1 personne",
            var n => $"{n} personnes",
        };
        var lignes = new List<string> { personnes };
        if (client is not null) lignes.Add(client.Email);
        lignes.Add($"HT {Euros(location.TarifHt(tauxTva))} · TTC {Euros(location.TarifTtc)}");
        return lignes;
    }

    private static string Infobulle(Location location, string nom, IReadOnlyList<string> details)
    {
        var lignes = new List<string>
        {
            nom,
            $"Du {location.DateDebut.ToString("dddd d MMMM HH:mm", Francais)} au {location.DateFin.ToString("dddd d MMMM HH:mm", Francais)}",
        };
        lignes.AddRange(details);
        if (location.NombrePersonnes > 0)
            lignes[2] += $" ({location.NombreAdultes} adulte(s), {location.NombreEnfants} enfant(s), {location.NombreBebes} bébé(s))";
        if (location.Airbnb || !string.IsNullOrWhiteSpace(location.NumeroReservation))
            lignes.Add((location.Airbnb ? "Airbnb" : "Réservation") +
                       (string.IsNullOrWhiteSpace(location.NumeroReservation) ? "" : $" n° {location.NumeroReservation}"));
        return string.Join(Environment.NewLine, lignes);
    }

    private static string Euros(decimal montant) => montant.ToString("#,##0.00 €", Francais);
}
