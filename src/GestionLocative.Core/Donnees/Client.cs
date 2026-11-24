namespace GestionLocative.Core.Donnees;

/// <summary>
/// Client (locataire). Champs obligatoires : nom, prénom, téléphone, email (unique).
/// À l'enregistrement : NOM en majuscules, Prénom avec majuscule initiale, téléphone international, email valide.
/// </summary>
public sealed class Client
{
    /// <summary>Numéro automatique attribué par la base (0 tant que le client n'est pas enregistré).</summary>
    public long IdClient { get; set; }

    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string? Adresse { get; set; }
    public string? CodePostal { get; set; }
    public string? Ville { get; set; }
    /// <summary>Enregistré au format international sans espaces (« +33612345678 »).</summary>
    public string Telephone { get; set; } = "";

    /// <summary>Téléphone pour l'affichage, espacé : « +33 6 12 34 56 78 ».</summary>
    public string TelephoneAffiche => MiseEnForme.TelephoneAffiche(Telephone);

    /// <summary>Unique dans la base, sans tenir compte des majuscules / minuscules.</summary>
    public string Email { get; set; } = "";

    public string? PlaqueImmatriculation { get; set; }

    /// <summary>Ex. « DUPONT Jean ».</summary>
    public string NomComplet => $"{Nom} {Prenom}".Trim();

    /// <summary>
    /// Applique la mise en forme imposée : NOM en majuscules, Prénom avec majuscule initiale,
    /// téléphone au format international (+33...), email en minuscules.
    /// Un téléphone ou un email non reconnu est laissé tel quel (<see cref="Valider"/> le signale).
    /// </summary>
    public void Normaliser()
    {
        Nom = MiseEnForme.Nom(Nom);
        Prenom = MiseEnForme.Prenom(Prenom);
        Telephone = MiseEnForme.Telephone(Telephone) ?? Telephone.Trim();
        Email = MiseEnForme.Email(Email) ?? Email.Trim();
        PlaqueImmatriculation = PlaqueImmatriculation?.Trim().ToUpperInvariant();
    }

    /// <summary>Retourne la liste des erreurs de saisie (vide si le client est valide).</summary>
    public IReadOnlyList<string> Valider()
    {
        var erreurs = new List<string>();
        if (string.IsNullOrWhiteSpace(Nom)) erreurs.Add("Le nom est obligatoire.");
        if (string.IsNullOrWhiteSpace(Prenom)) erreurs.Add("Le prénom est obligatoire.");
        if (string.IsNullOrWhiteSpace(Telephone)) erreurs.Add("Le téléphone est obligatoire.");
        else if (MiseEnForme.Telephone(Telephone) is null)
            erreurs.Add("Le téléphone n'est pas valide (ex. 06 12 34 56 78, ou +44 20 7946 0958 pour l'étranger).");
        if (string.IsNullOrWhiteSpace(Email)) erreurs.Add("L'email est obligatoire.");
        else if (MiseEnForme.Email(Email) is null) erreurs.Add("L'email n'est pas valide (ex. jean.dupont@exemple.fr).");
        return erreurs;
    }
}
