using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionLocative.Core.Calendar;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;

namespace GestionLocative.App.Views;

public partial class MainWindow : Window
{
    private readonly CalendarViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        // La source relit App.Base à chaque fois, pour suivre un changement de base dans les paramètres.
        _vm = new CalendarViewModel(TimeProvider.System, new LocationsCalendarSource(() => App.Base));
        DataContext = _vm;
    }

    private void Parametres_Click(object sender, RoutedEventArgs e)
    {
        var fenetre = new ParametresWindow { Owner = this };
        if (fenetre.ShowDialog() == true && fenetre.Base is not null)
        {
            App.Base = fenetre.Base;
            _vm.Actualiser();
            MessageBox.Show(this, $"Base de données utilisée :\n{App.Base.CheminFichier}",
                "Paramètres", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void NouvelleLocation_Executed(object sender, ExecutedRoutedEventArgs e) =>
        NouvelleLocation(_vm.JourParDefaut());

    /// <summary>Double-clic sur une vignette : nouvelle location qui commence ce jour-là.</summary>
    private void Vignette_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: DayTileViewModel jour })
        {
            e.Handled = true;
            NouvelleLocation(jour.Date);
        }
    }

    /// <summary>Double-clic sur une location : fenêtre de modification (ou de suppression).</summary>
    private void Location_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: CalendarEntry { IdLocation: long id } }) return;
        e.Handled = true; // sinon la vignette ouvrirait une nouvelle location

        var location = Charger(() => new LocationRepository(App.Base).ObtenirParId(id));
        if (location is null) return;

        var fenetre = new LocationWindow(App.Base, location) { Owner = this };
        if (fenetre.ShowDialog() == true)
            _vm.Actualiser();
    }

    /// <summary>Clic droit sur une location : copies dans le presse-papiers.</summary>
    private void Location_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarEntry { IdLocation: long id }, ContextMenu: { } menu })
        {
            e.Handled = true;
            return;
        }

        var donnees = Charger(() =>
        {
            var location = new LocationRepository(App.Base).ObtenirParId(id);
            var client = location is null ? null : new ClientRepository(App.Base).ObtenirParId(location.IdClient);
            return location is null || client is null ? null : new { location, client };
        });
        if (donnees is null)
        {
            e.Handled = true;
            return;
        }
        var (loc, client) = (donnees.location, donnees.client);

        string? telephonePrestataire = null;
        try { telephonePrestataire = new ReglagesRepository(App.Base).ObtenirTelephonePrestatairePrincipal(); }
        catch (Exception) { /* réglage illisible : l'élément sera grisé */ }

        menu.Items.Clear();

        var prestataire = new MenuItem { Header = "Prestataire" };
        var numero = ElementCopie("Copier le téléphone principal", MiseEnForme.TelephoneAffiche(telephonePrestataire));
        if (string.IsNullOrWhiteSpace(telephonePrestataire)) numero.Header = "Copier le téléphone principal (à renseigner dans Paramètres)";
        prestataire.Items.Add(numero);
        var message = ElementCopie("Message prestataire", TextesLocation.MessagePrestataire(loc, client));
        message.ToolTip = TextesLocation.MessagePrestataire(loc, client);
        prestataire.Items.Add(message);
        menu.Items.Add(prestataire);

        var location = new MenuItem { Header = "Location" };
        location.Items.Add(ElementCopie("Copier le nom", client.Nom));
        location.Items.Add(ElementCopie("Copier le prénom", client.Prenom));
        location.Items.Add(ElementCopie("Copier l'email", client.Email));
        location.Items.Add(ElementCopie("Copier le téléphone", client.TelephoneAffiche));
        location.Items.Add(ElementCopie("Copier la plaque d'immatriculation", client.PlaqueImmatriculation));
        menu.Items.Add(location);

        menu.Items.Add(new Separator());
        var modifier = new MenuItem { Header = "Modifier ou supprimer la location…" };
        modifier.Click += (_, _) =>
        {
            var fenetre = new LocationWindow(App.Base, loc) { Owner = this };
            if (fenetre.ShowDialog() == true) _vm.Actualiser();
        };
        menu.Items.Add(modifier);
    }

    /// <summary>Élément de menu qui copie <paramref name="texte"/> ; grisé si le texte est vide (ex. pas de plaque).</summary>
    private static MenuItem ElementCopie(string libelle, string? texte)
    {
        var vide = string.IsNullOrWhiteSpace(texte);
        var element = new MenuItem
        {
            Header = vide ? $"{libelle} (non renseigné)" : $"{libelle} : {Apercu(texte!)}",
            IsEnabled = !vide,
        };
        element.Click += (_, _) => Copier(texte!);
        return element;
    }

    private static string Apercu(string texte)
    {
        var premiereLigne = texte.Split(Environment.NewLine)[0];
        return premiereLigne.Length > 40 || texte.Contains(Environment.NewLine) ? premiereLigne[..Math.Min(40, premiereLigne.Length)] + "…" : premiereLigne;
    }

    private static void Copier(string texte)
    {
        try
        {
            Clipboard.SetText(texte);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException)
        {
            // Presse-papiers momentanément occupé par une autre application : on réessaie une fois.
            Thread.Sleep(100);
            try { Clipboard.SetText(texte); }
            catch (Exception) { MessageBox.Show("Le presse-papiers est occupé par une autre application, réessayez.", "Copier"); }
        }
    }

    private T? Charger<T>(Func<T?> lecture) where T : class
    {
        try
        {
            var resultat = lecture();
            if (resultat is null)
            {
                MessageBox.Show(this, "Cette location n'existe plus (supprimée depuis un autre poste ?).",
                    "Gestion locative", MessageBoxButton.OK, MessageBoxImage.Information);
                _vm.Actualiser();
            }
            return resultat;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Impossible de lire la location :\n{ex.Message}",
                "Gestion locative", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void NouvelleLocation(DateOnly arrivee)
    {
        var fenetre = new LocationWindow(App.Base, arrivee) { Owner = this };
        if (fenetre.ShowDialog() == true)
            _vm.Actualiser();
    }
}
