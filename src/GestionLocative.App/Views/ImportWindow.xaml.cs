using System.Windows;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Import;
using Microsoft.Win32;

namespace GestionLocative.App.Views;

/// <summary>Import de clients ou de locations depuis un fichier CSV, avec aperçu et rapport des lignes rejetées.</summary>
public partial class ImportWindow : Window
{
    private readonly ImportViewModel _vm;

    public ImportWindow(BaseDeDonnees baseDeDonnees)
    {
        InitializeComponent();
        _vm = new ImportViewModel(baseDeDonnees);
        DataContext = _vm;
    }

    /// <summary>Vrai si des clients ou des locations ont été enregistrés (le calendrier est à redessiner).</summary>
    public bool ImportFait => _vm.ImportFait;

    private void ChoisirFichier_Click(object sender, RoutedEventArgs e)
    {
        var dialogue = new OpenFileDialog
        {
            Title = _vm.ImportClients ? "Fichier CSV des clients" : "Fichier CSV des locations",
            Filter = "Fichiers CSV (*.csv;*.txt)|*.csv;*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
        };
        if (_vm.CheminFichier is { } precedent && System.IO.Path.GetDirectoryName(precedent) is { } dossier)
            dialogue.InitialDirectory = dossier;
        if (dialogue.ShowDialog(this) == true)
            _vm.ChargerFichier(dialogue.FileName);
    }

    private void Importer_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Rapport is not { } apercu) return;
        var objet = apercu.Type == TypeImport.Clients ? "clients" : "locations";
        var ignorees = apercu.NombreRejetees + apercu.NombreDejaPresentes;
        var question = $"Importer {apercu.NombreAImporter} {(apercu.NombreAImporter > 1 ? objet : objet.TrimEnd('s'))} ?" +
                       (ignorees > 0 ? $"\n\n{ignorees} ligne{(ignorees > 1 ? "s" : "")} rejetée{(ignorees > 1 ? "s" : "")} ou déjà présente{(ignorees > 1 ? "s" : "")} ser{(ignorees > 1 ? "ont" : "a")} ignorée{(ignorees > 1 ? "s" : "")}." : "");
        if (Dialogue.Afficher(this, question, "Importer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _vm.ImporterCommand.Execute(null);
        if (_vm.Rapport is { Enregistre: true } bilan)
        {
            var suite = bilan.Type == TypeImport.Clients
                ? "\n\nVous pouvez maintenant choisir « 2. Locations » et importer le fichier des locations."
                : "";
            Dialogue.Afficher(this, bilan.Resume + suite, "Import terminé", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopierRapport_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Rapport is null) return;
        try
        {
            Clipboard.SetText(_vm.Rapport.Texte());
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException)
        {
            Dialogue.Afficher(this, "Le presse-papiers est occupé par une autre application, réessayez.", "Copier", icone: MessageBoxImage.Warning);
        }
    }
}
