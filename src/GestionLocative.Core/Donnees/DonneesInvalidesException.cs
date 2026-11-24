namespace GestionLocative.Core.Donnees;

/// <summary>Saisie refusée (champ obligatoire manquant, email déjà utilisé...). Le message est affichable tel quel.</summary>
public sealed class DonneesInvalidesException(IReadOnlyList<string> erreurs)
    : Exception(string.Join(Environment.NewLine, erreurs))
{
    public IReadOnlyList<string> Erreurs { get; } = erreurs;

    public DonneesInvalidesException(string erreur) : this([erreur]) { }
}
