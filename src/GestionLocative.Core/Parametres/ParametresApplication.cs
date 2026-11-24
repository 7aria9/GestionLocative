namespace GestionLocative.Core.Parametres;

/// <summary>Réglages propres à chaque poste (enregistrés dans le profil Windows de l'utilisateur).</summary>
public sealed class ParametresApplication
{
    /// <summary>
    /// Chemin complet du fichier de base de données. Peut être un chemin réseau,
    /// ex. <c>\\SERVEUR\Partage\GestionLocative\gestionlocative.db</c> ou <c>Z:\GestionLocative\gestionlocative.db</c>.
    /// </summary>
    public string CheminBase { get; set; } = CheminBaseParDefaut;

    /// <summary>Documents\GestionLocative\gestionlocative.db</summary>
    public static string CheminBaseParDefaut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GestionLocative",
        "gestionlocative.db");
}
