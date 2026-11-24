using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace GestionLocative.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Interface en français, quelle que soit la langue de Windows.
        var francais = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = francais;
        CultureInfo.DefaultThreadCurrentUICulture = francais;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(francais.IetfLanguageTag)));

        base.OnStartup(e);
    }
}
