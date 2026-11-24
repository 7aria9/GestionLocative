namespace GestionLocative.Core.Donnees;

/// <summary>Client (locataire). Champs obligatoires : nom, prénom, téléphone, email (unique).</summary>
public sealed class Client
{
    /// <summary>Numéro automatique attribué par la base (0 tant que le client n'est pas enregistré).</summary>
    public long IdClient { get; set; }

    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string? Adresse { get; set; }
    public string? CodePostal { get; set; }
    public string? Ville { get; set; }
    public string Telephone { get; set; } = "";

    /// <summary>Unique dans la base, sans tenir compte des majuscules / minuscules.</summary>
    public string Email { get; set; } = "";

    public string? PlaqueImmatriculation { get; set; }

    /// <summary>Ex. « DUPONT Jean ».</summary>
    public string NomComplet => $"{Nom} {Prenom}".Trim();

    /// <summary>Retourne la liste des erreurs de saisie (vide si le client est valide).</summary>
    public IReadOnlyList<string> Valider()
    {
        var erreurs = new List<string>();
        if (string.IsNullOrWhiteSpace(Nom)) erreurs.Add("Le nom est obligatoire.");
        if (string.IsNullOrWhiteSpace(Prenom)) erreurs.Add("Le prénom est obligatoire.");
        if (string.IsNullOrWhiteSpace(Telephone)) erreurs.Add("Le téléphone est obligatoire.");
        if (string.IsNullOrWhiteSpace(Email)) erreurs.Add("L'email est obligatoire.");
        else if (!EmailValide(Email)) erreurs.Add("L'email n'est pas valide.");
        return erreurs;
    }

    private static bool EmailValide(string email)
    {
        var texte = email.Trim();
        var arobase = texte.IndexOf('@');
        return arobase > 0
            && arobase == texte.LastIndexOf('@')
            && texte.IndexOf('.', arobase) > arobase + 1
            && !texte.EndsWith('.')
            && !texte.Any(char.IsWhiteSpace);
    }
}
