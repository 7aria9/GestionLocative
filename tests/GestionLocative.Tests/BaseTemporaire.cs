using GestionLocative.Core.Donnees;

namespace GestionLocative.Tests;

/// <summary>Base SQLite dans un dossier temporaire, supprimée à la fin du test.</summary>
public sealed class BaseTemporaire : IDisposable
{
    public BaseTemporaire()
    {
        Dossier = Path.Combine(Path.GetTempPath(), "GestionLocativeTests", Guid.NewGuid().ToString("N"));
        Base = new BaseDeDonnees(Path.Combine(Dossier, "test.db"));
        Base.Initialiser();
    }

    public string Dossier { get; }
    public BaseDeDonnees Base { get; }

    public void Dispose()
    {
        try { Directory.Delete(Dossier, recursive: true); } catch (IOException) { }
    }

    public static Client NouveauClient(string email = "jean.dupont@example.com") => new()
    {
        Nom = "Dupont",
        Prenom = "Jean",
        Telephone = "06 12 34 56 78",
        Email = email,
    };
}
