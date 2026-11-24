using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GestionLocative.App.Views;
using GestionLocative.Core.Recapitulatif;

namespace GestionLocative.App;

/// <summary>
/// Impression d'un tableau (récapitulatif, liste des présences). Pour obtenir un PDF, choisir l'imprimante
/// « Microsoft Print to PDF » fournie avec Windows : pas de bibliothèque supplémentaire à installer.
/// La page est toujours en noir sur blanc, quel que soit le thème de l'écran.
/// </summary>
public static class Impression
{
    private static readonly Brush Noir = Brushes.Black;
    private static readonly Brush Gris = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66));
    private static readonly Brush Trait = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xC8));
    private static readonly Brush FondEnTete = new SolidColorBrush(Color.FromRgb(0xEE, 0xEC, 0xF4));
    private static readonly Brush FondAlterne = new SolidColorBrush(Color.FromRgb(0xF7, 0xF6, 0xFA));

    public static void Imprimer(Window proprietaire, TableauImprimable tableau)
    {
        try
        {
            var dialogue = new PrintDialog();
            if (dialogue.ShowDialog() != true) return;
            var document = Construire(tableau, dialogue.PrintableAreaWidth, dialogue.PrintableAreaHeight);
            dialogue.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, tableau.Titre);
        }
        catch (Exception ex)
        {
            Dialogue.Afficher(proprietaire, $"L'impression n'a pas pu se faire :\n{ex.Message}", "Imprimer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static FlowDocument Construire(TableauImprimable tableau, double largeur, double hauteur)
    {
        var document = new FlowDocument
        {
            PageWidth = largeur,
            PageHeight = hauteur,
            PagePadding = new Thickness(48),
            ColumnWidth = double.PositiveInfinity, // une seule colonne de texte
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = Noir,
            Background = Brushes.White,
        };

        document.Blocks.Add(new Paragraph(new Run(tableau.Titre)) { FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var sousTitre in tableau.SousTitres.Where(t => !string.IsNullOrWhiteSpace(t)))
            document.Blocks.Add(new Paragraph(new Run(sousTitre)) { Foreground = Gris, Margin = new Thickness(0, 0, 0, 2) });

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 14, 0, 0), BorderBrush = Trait, BorderThickness = new Thickness(0, 0, 0, 1) };
        foreach (var colonne in tableau.Colonnes)
            table.Columns.Add(new TableColumn { Width = new GridLength(colonne.Largeur, GridUnitType.Star) });

        var enTete = new TableRowGroup { Background = FondEnTete, FontWeight = FontWeights.SemiBold };
        enTete.Rows.Add(Ligne(tableau, tableau.Colonnes.Select(c => c.Libelle).ToList(), new Thickness(0, 0, 0, 1)));
        table.RowGroups.Add(enTete);

        var corps = new TableRowGroup();
        for (var i = 0; i < tableau.Lignes.Count; i++)
        {
            var ligne = Ligne(tableau, tableau.Lignes[i], new Thickness(0));
            if (i % 2 == 1) ligne.Background = FondAlterne;
            corps.Rows.Add(ligne);
        }
        table.RowGroups.Add(corps);

        if (tableau.Total is { } total)
        {
            var pied = new TableRowGroup { FontWeight = FontWeights.Bold };
            pied.Rows.Add(Ligne(tableau, total, new Thickness(0, 1.5, 0, 0)));
            table.RowGroups.Add(pied);
        }
        document.Blocks.Add(table);

        var note = $"Édité le {DateTime.Now:dd/MM/yyyy à HH:mm}";
        if (!string.IsNullOrWhiteSpace(tableau.Note)) note = $"{tableau.Note}\n{note}";
        document.Blocks.Add(new Paragraph(new Run(note)) { Foreground = Gris, FontSize = 9, Margin = new Thickness(0, 14, 0, 0) });
        return document;
    }

    private static TableRow Ligne(TableauImprimable tableau, IReadOnlyList<string> textes, Thickness bordure)
    {
        var ligne = new TableRow();
        for (var i = 0; i < tableau.Colonnes.Count; i++)
        {
            var texte = i < textes.Count ? textes[i] : "";
            ligne.Cells.Add(new TableCell(new Paragraph(new Run(texte))
            {
                TextAlignment = tableau.Colonnes[i].ADroite ? TextAlignment.Right : TextAlignment.Left,
                Margin = new Thickness(0),
            })
            {
                Padding = new Thickness(6, 4, 6, 4),
                BorderBrush = Trait,
                BorderThickness = bordure,
            });
        }
        return ligne;
    }
}
