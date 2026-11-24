using System.Windows;

namespace GestionLocative.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Parametres_Click(object sender, RoutedEventArgs e)
    {
        var fenetre = new ParametresWindow { Owner = this };
        if (fenetre.ShowDialog() == true && fenetre.Base is not null)
        {
            App.Base = fenetre.Base;
            MessageBox.Show(this, $"Base de données utilisée :\n{App.Base.CheminFichier}",
                "Paramètres", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
