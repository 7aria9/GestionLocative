using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.ViewModels;
using Microsoft.Win32;

namespace GestionLocative.App.Views;

/// <summary>Choix de l'emplacement de la base de données (local ou réseau).</summary>
public partial class ParametresWindow : Window
{
    private readonly ParametresViewModel _vm;

    public ParametresWindow()
    {
        InitializeComponent();
        _vm = new ParametresViewModel(App.Parametres);
        _vm.Enregistre += Enregistre;
        _vm.PropertyChanged += CouleurMessage;
        DataContext = _vm;
    }

    /// <summary>Base ouverte avec le nouveau chemin (renseignée si l'utilisateur a enregistré).</summary>
    public BaseDeDonnees? Base { get; private set; }

    private void Enregistre(object? sender, BaseDeDonnees baseDeDonnees)
    {
        Base = baseDeDonnees;
        DialogResult = true;
    }

    private void CouleurMessage(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParametresViewModel.MessageEstErreur))
            Message.Foreground = (Brush)FindResource(_vm.MessageEstErreur ? "Erreur" : "Succes");
    }

    private void Parcourir_Click(object sender, RoutedEventArgs e)
    {
        // Permet de choisir un fichier existant ou de taper le nom d'un nouveau fichier.
        var dialogue = new SaveFileDialog
        {
            Title = "Emplacement de la base de données",
            Filter = "Base de données (*.db)|*.db|Tous les fichiers (*.*)|*.*",
            DefaultExt = ".db",
            OverwritePrompt = false,
            CheckPathExists = true,
        };
        try
        {
            var actuel = _vm.CheminBase.Trim();
            if (Path.IsPathFullyQualified(actuel))
            {
                dialogue.InitialDirectory = Path.GetDirectoryName(actuel);
                dialogue.FileName = Path.GetFileName(actuel);
            }
        }
        catch (ArgumentException)
        {
            // Chemin saisi invalide : on ouvre le dialogue sans présélection.
        }

        if (dialogue.ShowDialog(this) == true)
            _vm.CheminBase = dialogue.FileName;
    }
}
