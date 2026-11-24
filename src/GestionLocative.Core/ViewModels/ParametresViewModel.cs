using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionLocative.Core.Donnees;
using GestionLocative.Core.Parametres;

namespace GestionLocative.Core.ViewModels;

/// <summary>Fenêtre « Paramètres » : choix de l'emplacement de la base de données.</summary>
public sealed partial class ParametresViewModel : ObservableObject
{
    private readonly MagasinParametres _magasin;

    public ParametresViewModel(MagasinParametres magasin)
    {
        _magasin = magasin;
        _cheminBase = magasin.Charger().CheminBase;
    }

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
        var baseDeDonnees = EssayerOuvrir();
        if (baseDeDonnees is null) return;

        var parametres = _magasin.Charger();
        parametres.CheminBase = baseDeDonnees.CheminFichier;
        _magasin.Enregistrer(parametres);
        Enregistre?.Invoke(this, baseDeDonnees);
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
