using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Parametres;

namespace GestionLocative.Core.ViewModels;

/// <summary>Fenêtre « Paramètres » : emplacement de la base de données et taux de TVA.</summary>
public sealed partial class ParametresViewModel : ObservableObject
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    private readonly MagasinParametres _magasin;
    private readonly string _tauxTvaInitial;

    /// <param name="baseActuelle">Base en cours d'utilisation, pour lire le taux de TVA (null au démarrage si elle est inaccessible).</param>
    public ParametresViewModel(MagasinParametres magasin, BaseDeDonnees? baseActuelle = null)
    {
        _magasin = magasin;
        _cheminBase = magasin.Charger().CheminBase;

        var taux = ReglagesRepository.TauxTvaParDefaut;
        if (baseActuelle is not null)
        {
            try { taux = new ReglagesRepository(baseActuelle).ObtenirTauxTva(); }
            catch (Exception) { /* base inaccessible : on affiche la valeur par défaut */ }
        }
        _tauxTvaInitial = _tauxTva = taux.ToString("0.##", Francais);
    }

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

        var baseDeDonnees = EssayerOuvrir();
        if (baseDeDonnees is null) return;

        if (nouveauTaux is not null)
        {
            try
            {
                new ReglagesRepository(baseDeDonnees).DefinirTauxTva(nouveauTaux.Value);
            }
            catch (Exception ex)
            {
                AfficherMessage($"Impossible d'enregistrer le taux de TVA : {ex.Message}", erreur: true);
                return;
            }
        }

        var parametres = _magasin.Charger();
        parametres.CheminBase = baseDeDonnees.CheminFichier;
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
