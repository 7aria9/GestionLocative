using System.Text.Json.Serialization;

namespace GestionLocative.Core.Parametres;

/// <summary>Réglages propres à chaque poste (enregistrés dans le profil Windows de l'utilisateur).</summary>
public sealed class ParametresApplication
{
    /// <summary>
    /// Chemin complet du fichier de base de données. Peut être un chemin réseau,
    /// ex. <c>\\SERVEUR\Partage\GestionLocative\gestionlocative.db</c> ou <c>Z:\GestionLocative\gestionlocative.db</c>.
    /// </summary>
    public string CheminBase { get; set; } = CheminBaseParDefaut;

    /// <summary>Thème de l'interface (clair par défaut).</summary>
    [JsonIgnore]
    public ThemeInterface Theme { get; set; } = ThemeInterface.Clair;

    /// <summary>
    /// Thème écrit en toutes lettres dans le fichier JSON (« Clair », « Sombre »). Une valeur inconnue
    /// redonne le thème clair, sans faire perdre les autres réglages (comme le chemin de la base).
    /// </summary>
    [JsonPropertyName("Theme")]
    public string ThemeTexte
    {
        get => Theme.ToString();
        set => Theme = Enum.TryParse<ThemeInterface>(value, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
            ? theme
            : ThemeInterface.Clair;
    }

    /// <summary>Documents\GestionLocative\gestionlocative.db</summary>
    public static string CheminBaseParDefaut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GestionLocative",
        "gestionlocative.db");
}
