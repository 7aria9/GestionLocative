using System.Text.Json;

namespace GestionLocative.Core.Parametres;

/// <summary>Lit et enregistre les réglages dans un petit fichier JSON.</summary>
public sealed class MagasinParametres(string cheminFichier)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>%APPDATA%\GestionLocative\parametres.json</summary>
    public static string CheminParDefaut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GestionLocative",
        "parametres.json");

    public string CheminFichier { get; } = cheminFichier;

    /// <summary>Retourne les réglages enregistrés, ou les réglages par défaut si le fichier est absent ou illisible.</summary>
    public ParametresApplication Charger()
    {
        try
        {
            if (File.Exists(CheminFichier))
            {
                var parametres = JsonSerializer.Deserialize<ParametresApplication>(File.ReadAllText(CheminFichier), Options);
                if (parametres is not null && !string.IsNullOrWhiteSpace(parametres.CheminBase))
                    return parametres;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fichier abîmé ou illisible : on repart des valeurs par défaut.
        }
        return new ParametresApplication();
    }

    public void Enregistrer(ParametresApplication parametres)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CheminFichier))!);
        // Écriture dans un fichier temporaire puis remplacement, pour ne jamais laisser un fichier à moitié écrit.
        var temporaire = CheminFichier + ".tmp";
        File.WriteAllText(temporaire, JsonSerializer.Serialize(parametres, Options));
        File.Move(temporaire, CheminFichier, overwrite: true);
    }
}
