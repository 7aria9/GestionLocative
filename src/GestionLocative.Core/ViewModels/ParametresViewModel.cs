using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Parametres;

namespace GestionLocative.Core.ViewModels;

/// <summary>Fenêtre « Paramètres » : emplacement de la base de données, thème, taux de TVA et téléphones du prestataire.</summary>
public sealed partial class ParametresViewModel : ObservableObject
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    private readonly MagasinParametres _magasin;
    private readonly string _tauxTvaInitial;
    private readonly string _telephonePrincipalInitial;
    private readonly string _telephoneSecondaireInitial;

    /// <param name="baseActuelle">Base en cours d'utilisation, pour lire le taux de TVA (null au démarrage si elle est inaccessible).</param>
    public ParametresViewModel(MagasinParametres magasin, BaseDeDonnees? baseActuelle = null)
    {
        _magasin = magasin;
        var parametresPoste = magasin.Charger();
        _cheminBase = parametresPoste.CheminBase;
        ThemeInitial = _theme = parametresPoste.Theme;

        var taux = ReglagesRepository.TauxTvaParDefaut;
        string? principal = null, secondaire = null;
        if (baseActuelle is not null)
        {
            try
            {
                var reglages = new ReglagesRepository(baseActuelle);
                taux = reglages.ObtenirTauxTva();
                principal = reglages.ObtenirTelephonePrestatairePrincipal();
                secondaire = reglages.ObtenirTelephonePrestataireSecondaire();
            }
            catch (Exception) { /* base inaccessible : on affiche les valeurs par défaut */ }
        }
        _tauxTvaInitial = _tauxTva = taux.ToString("0.##", Francais);
        _telephonePrincipalInitial = _telephonePrestatairePrincipal = MiseEnForme.TelephoneAffiche(principal);
        _telephoneSecondaireInitial = _telephonePrestataireSecondaire = MiseEnForme.TelephoneAffiche(secondaire);
    }

    /// <summary>Thème enregistré sur ce poste à l'ouverture de la fenêtre (pour revenir en arrière si on annule).</summary>
    public ThemeInterface ThemeInitial { get; }

    /// <summary>Thème choisi, propre à ce poste. Appliqué tout de suite pour aperçu, enregistré avec « Enregistrer ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeClair), nameof(ThemeSombre))]
    private ThemeInterface _theme;

    /// <summary>Pour le bouton radio « Clair ».</summary>
    public bool ThemeClair
    {
        get => Theme == ThemeInterface.Clair;
        set { if (value) Theme = ThemeInterface.Clair; }
    }

    /// <summary>Pour le bouton radio « Sombre ».</summary>
    public bool ThemeSombre
    {
        get => Theme == ThemeInterface.Sombre;
        set { if (value) Theme = ThemeInterface.Sombre; }
    }

    /// <summary>Téléphone principal du prestataire (facultatif), commun à tous les postes.</summary>
    [ObservableProperty]
    private string _telephonePrestatairePrincipal;

    /// <summary>Téléphone secondaire du prestataire (facultatif), commun à tous les postes.</summary>
    [ObservableProperty]
    private string _telephonePrestataireSecondaire;

    /// <summary>Taux de TVA en %, commun à tous les postes (enregistré dans la base). Ex. « 10 » ou « 5,5 ».</summary>
    [ObservableProperty]
    private string _tauxTva;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnregistrerCommand))]
    private string _cheminBase;

    /// <summary>Message affiché sous le champ (erreur ou confirmation).</summary>
    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _messageEstErreur;

    /// <summary>Déclenché quand le nouveau chemin est validé et enregistré.</summary>
    public event EventHandler<BaseDeDonnees>? Enregistre;

    [RelayCommand]
    private void RemettreParDefaut() => CheminBase = ParametresApplication.CheminBaseParDefaut;

    /// <summary>Ouvre (ou crée) la base sans enregistrer le réglage.</summary>
    [RelayCommand]
    private void Tester()
    {
        if (EssayerOuvrir() is not null)
            AfficherMessage("La base de données est accessible.", erreur: false);
    }

    [RelayCommand(CanExecute = nameof(PeutEnregistrer))]
    private void Enregistrer()
    {
        // Le taux n'est écrit que s'il a été modifié : sinon, changer de base garderait le taux de l'ancienne.
        decimal? nouveauTaux = null;
        if (TauxTva.Trim() != _tauxTvaInitial)
        {
            nouveauTaux = LireTaux(TauxTva);
            if (nouveauTaux is null)
            {
                AfficherMessage("Le taux de TVA doit être un nombre entre 0 et 100 (ex. 10 ou 5,5).", erreur: true);
                return;
            }
        }

        // Même règle pour les téléphones : vérifiés avant tout, écrits seulement s'ils ont été modifiés.
        var principalModifie = TelephonePrestatairePrincipal.Trim() != _telephonePrincipalInitial;
        var secondaireModifie = TelephonePrestataireSecondaire.Trim() != _telephoneSecondaireInitial;
        if (principalModifie && !TelephoneValideOuVide(TelephonePrestatairePrincipal))
        {
            AfficherMessage("Le numéro principal du prestataire n'est pas valide (ex. 06 12 34 56 78).", erreur: true);
            return;
        }
        if (secondaireModifie && !TelephoneValideOuVide(TelephonePrestataireSecondaire))
        {
            AfficherMessage("Le numéro secondaire du prestataire n'est pas valide (ex. 06 12 34 56 78).", erreur: true);
            return;
        }

        var baseDeDonnees = EssayerOuvrir();
        if (baseDeDonnees is null) return;

        try
        {
            var reglages = new ReglagesRepository(baseDeDonnees);
            if (nouveauTaux is not null) reglages.DefinirTauxTva(nouveauTaux.Value);
            if (principalModifie) reglages.DefinirTelephonePrestatairePrincipal(TelephonePrestatairePrincipal);
            if (secondaireModifie) reglages.DefinirTelephonePrestataireSecondaire(TelephonePrestataireSecondaire);
        }
        catch (Exception ex)
        {
            AfficherMessage($"Impossible d'enregistrer les réglages : {ex.Message}", erreur: true);
            return;
        }

        var parametres = _magasin.Charger();
        parametres.CheminBase = baseDeDonnees.CheminFichier;
        parametres.Theme = Theme;
        _magasin.Enregistrer(parametres);
        Enregistre?.Invoke(this, baseDeDonnees);
    }

    private static decimal? LireTaux(string texte)
    {
        var t = texte.Replace("%", "").Replace(" ", "").Replace('.', ',').Trim();
        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, Francais, out var taux)
            && taux is >= 0 and <= 100 && decimal.Round(taux, 2) == taux
            ? taux
            : null;
    }

    private static bool TelephoneValideOuVide(string texte) =>
        string.IsNullOrWhiteSpace(texte) || MiseEnForme.Telephone(texte) is not null;

    private bool PeutEnregistrer() => !string.IsNullOrWhiteSpace(CheminBase);

    private BaseDeDonnees? EssayerOuvrir()
    {
        try
        {
            var chemin = CheminBase.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(chemin))
            {
                AfficherMessage("Indiquez un chemin complet, par ex. \\\\SERVEUR\\Partage\\gestionlocative.db", erreur: true);
                return null;
            }
            var baseDeDonnees = new BaseDeDonnees(chemin);
            baseDeDonnees.Initialiser();
            return baseDeDonnees;
        }
        catch (Exception ex)
        {
            AfficherMessage($"Impossible d'ouvrir la base : {ex.Message}", erreur: true);
            return null;
        }
    }

    private void AfficherMessage(string message, bool erreur)
    {
        Message = message;
        MessageEstErreur = erreur;
    }
}
