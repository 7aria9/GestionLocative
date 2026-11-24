# Gestion locative

Réécriture en C# moderne d'un logiciel de gestion locative (anciennement WinDev).

## Technologies

- **.NET 10** (version LTS) et **WPF** pour l'application de bureau Windows.
- **CommunityToolkit.Mvvm** pour séparer l'interface (XAML) de la logique (ViewModels).
- **xUnit** pour les tests.

## Organisation

| Dossier | Contenu |
| --- | --- |
| `src/GestionLocative.App` | Application WPF : fenêtres, styles, affichage. |
| `src/GestionLocative.Core` | Logique sans dépendance à l'interface : calcul du calendrier, ViewModels. |
| `tests/GestionLocative.Tests` | Tests automatiques de `GestionLocative.Core`. |

## Écran principal

Calendrier du mois en cours, une vignette par jour, semaine commençant le lundi.
Les boutons en haut à droite (ou les touches Page préc. / Page suiv.) changent de mois.
Les jours des mois voisins complètent la première et la dernière semaine, grisés ; le jour courant est mis en évidence.

Chaque vignette peut afficher une liste d'éléments (`CalendarEntry`) fournis par une `ICalendarEntrySource` :
c'est là que viendront se brancher les réservations et locations.

## Lancer l'application

Sous Windows, avec le SDK .NET 10 installé :

```
dotnet run --project src/GestionLocative.App
```

Ou ouvrir `GestionLocative.slnx` dans Visual Studio 2022 (17.14+) ou Visual Studio 2026 et lancer `GestionLocative.App`.

## Tests

```
dotnet test
```

## Téléchargement

À chaque merge sur `main`, le workflow **Release Windows** lance les tests puis publie une
[release](../../releases) contenant `GestionLocative-<version>-win-x64.zip`. Il suffit de
dézipper et de lancer `GestionLocative.exe` : aucune installation de .NET n'est nécessaire.
Le numéro de version (`1.0.N`) augmente automatiquement à chaque release.
