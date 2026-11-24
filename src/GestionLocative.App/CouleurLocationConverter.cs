using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GestionLocative.App;

/// <summary>
/// Numéro de couleur d'une location → pinceau du thème (« LocationFond3 », « LocationTexte3 »...).
/// Le paramètre indique le rôle : Fond, Texte ou Lisere.
/// </summary>
public sealed class CouleurLocationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var index = value is int i ? i : 0;
        return Application.Current.TryFindResource($"Location{parameter}{index}") as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
