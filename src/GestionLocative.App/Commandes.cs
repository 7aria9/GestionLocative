using System.Windows.Input;

namespace GestionLocative.App;

/// <summary>Commandes de l'application qui n'existent pas dans WPF (ApplicationCommands).</summary>
public static class Commandes
{
    /// <summary>Ouvre le récapitulatif annuel (Ctrl+R).</summary>
    public static readonly RoutedUICommand Recapitulatif = new(
        "Récapitulatif", nameof(Recapitulatif), typeof(Commandes), [new KeyGesture(Key.R, ModifierKeys.Control)]);
}
