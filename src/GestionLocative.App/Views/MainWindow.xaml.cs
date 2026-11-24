using System.Windows;
using System.Windows.Input;
using GestionLocative.Core.Calendar;
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

    private void NouvelleLocation(DateOnly arrivee)
    {
        var fenetre = new LocationWindow(App.Base, arrivee) { Owner = this };
        if (fenetre.ShowDialog() == true)
            _vm.Actualiser();
    }
}
