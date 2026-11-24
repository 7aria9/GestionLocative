# Gestion locative

Réécriture en C# moderne d'un logiciel de gestion locative (anciennement WinDev).

## Technologies

- **.NET 10** (version LTS) et **WPF** pour l'application de bureau Windows.
- **CommunityToolkit.Mvvm** pour séparer l'interface (XAML) de la logique (ViewModels).
- **SQLite** (via Microsoft.Data.Sqlite) pour la base de données : un simple fichier, sans serveur à installer.
- **xUnit** pour les tests.

## Organisation

| Dossier | Contenu |
| --- | --- |
| `src/GestionLocative.App` | Application WPF : fenêtres, styles, affichage. |
| `src/GestionLocative.Core` | Logique sans dépendance à l'interface : calendrier, base de données (`Donnees`), import CSV (`Import`), réglages (`Parametres`), ViewModels. |
| `exemples-import` | Fichiers CSV d'exemple pour l'import des clients et des locations. |
| `tests/GestionLocative.Tests` | Tests automatiques de `GestionLocative.Core`. |

## Écran principal

Calendrier du mois en cours, une vignette par jour, semaine commençant le lundi.
Les boutons en haut à droite (ou les touches Page préc. / Page suiv.) changent de mois.
Les jours des mois voisins complètent la première et la dernière semaine, grisés ; le jour courant est mis en évidence.

Chaque vignette affiche les locations du jour (`LocationsCalendarSource`), chacune dans sa couleur :
« Arrivée NOM Prénom » le premier jour, le nom du client les jours intermédiaires et « Départ NOM Prénom » le dernier jour.
Le jour d'arrivée (et chaque lundi, en début de ligne) montre aussi le nombre total de personnes, l'email,
le prix HT et le prix TTC. Le survol d'une location affiche tout le détail (dates, voyageurs, Airbnb, n° de réservation).

- **Double-clic sur une location** : fenêtre de modification, avec un bouton « Supprimer la location… » (confirmation demandée).
- **Clic droit sur une location** : deux sous-menus.
  - **Prestataire** : copier le téléphone principal du prestataire (réglé dans **Paramètres**, avec un numéro secondaire)
    ou le **message prestataire** ci-dessous.
  - **Location** : copier le nom, le prénom, l'email, le téléphone ou la plaque d'immatriculation du client.

  Message prestataire :

  ```
  du 05/12/2026 au 08/12/2026
  DUPONT Jean
  2 adultes, 1 enfant, 0 bébé
  +33 6 12 34 56 78
  ```

Le prix HT est calculé à partir du tarif TTC avec le taux de TVA réglé dans **Paramètres** (10 % par défaut).

### Thèmes clair et sombre

Deux apparences dans le style de Scan-Online (Segoe UI, angles arrondis, accents rose → violet, bouton principal en dégradé) :
**Clair** (par défaut) et **Sombre**. Le choix se fait en haut des **Paramètres** : le thème s'applique tout de suite
pour l'essayer, et il est mémorisé sur le poste avec **Enregistrer** (« Annuler » revient au thème précédent).
Il est enregistré dans `%APPDATA%\GestionLocative\parametres.json`, donc chaque ordinateur garde le sien.
Les couleurs sont dans `Themes/Clair.xaml` et `Themes/Sombre.xaml` ; les styles communs dans `Theme.xaml`.
Chaque location garde sa teinte dans les deux thèmes (fond pastel en clair, fond sombre teinté en sombre, texte contrasté et liseré vif).

## Créer une location

Bouton **+ Nouvelle location** (ou Ctrl+N), ou **double-clic sur un jour** du calendrier pour commencer la location ce jour-là.

- Arrivée à 16h00 et départ le lendemain à 10h00 par défaut (heures modifiables, ex. « 15h30 »).
- Client obligatoire : le champ de recherche filtre la liste (nom, prénom, email, téléphone, plaque).
  Le bouton **+ Nouveau client…** ouvre la fiche client ; une fois enregistré, il est choisi automatiquement.
- Tarif TTC obligatoire (« 450 », « 450,50 € »...), nombre d'adultes / enfants / bébés, Airbnb, n° de réservation.
- Si les dates chevauchent une autre location, le programme demande confirmation avant d'enregistrer
  (un départ à 10h et une arrivée à 16h le même jour ne se chevauchent pas).

## Importer des clients et des locations (CSV)

Bouton **Importer…** en haut de l'écran principal. Il sert surtout à reprendre les données de l'ancien programme WinDev
(ou d'un tableau Excel). On importe **d'abord les clients, puis les locations**.

1. Choisir **1. Clients** ou **2. Locations** (le programme bascule tout seul s'il reconnaît l'autre type de fichier).
2. **Choisir un fichier…** : un aperçu s'affiche sans rien enregistrer. Chaque ligne est marquée **À importer**,
   **Déjà présente** (ignorée) ou **Rejetée**, avec la raison. **Copier le rapport** copie la liste des lignes à corriger.
3. **Importer** enregistre en une seule fois les lignes « À importer » (tout ou rien en cas de problème).

Réimporter le même fichier ne crée pas de doublons : on peut corriger les lignes rejetées dans Excel et relancer l'import.

### Format du fichier

- Première ligne = noms des colonnes. L'ordre des colonnes est libre et les colonnes inconnues sont ignorées.
- Séparateur `;` (Excel français, WinDev), `,` ou tabulation, détecté automatiquement (une première ligne `sep=;` est aussi acceptée).
- Encodage UTF-8 (avec ou sans BOM) ou Windows-1252 (« CSV (séparateur : point-virgule) » d'Excel, exports WinDev) : les accents sont conservés.
- Une valeur contenant le séparateur ou un retour à la ligne doit être entre guillemets (`"3, place de l'Église"`), comme le font Excel et WinDev.
- Les noms de colonnes sont reconnus sans tenir compte des majuscules, des accents, des espaces ni de la ponctuation :
  `N° réservation`, `num_reservation` et `NumReservation` sont équivalents.

Exemples prêts à l'emploi dans [`exemples-import/`](exemples-import) : `clients.csv` (Windows-1252) et `locations.csv` (UTF-8).

**Clients**

| Champ | Noms de colonne reconnus (exemples) | Règle |
| --- | --- | --- |
| N° client d'origine | `IdClient`, `N° client`, `NumClient`, `Code client`, `Id` | facultatif mais conseillé : sert à rattacher les locations (`0042` = `42`) |
| Nom | `Nom`, `Nom client` | **obligatoire**, mis en MAJUSCULES |
| Prénom | `Prénom`, `Prénoms` | obligatoire, majuscule initiale |
| Téléphone | `Téléphone`, `Tél`, `Portable`, `Mobile`, `GSM` | obligatoire, converti au format international ; s'il y a plusieurs colonnes (fixe, portable), la première non vide est prise |
| Email | `Email`, `E-mail`, `Mail`, `Courriel`, `Adresse mail` | **obligatoire**, valide, sert à repérer les doublons |
| Adresse | `Adresse`, `Adresse 1`, `Adresse 2`, `Rue` | facultatif ; plusieurs colonnes sont mises bout à bout |
| Code postal, Ville | `Code postal`, `CP` / `Ville`, `Commune` | facultatifs |
| Plaque | `Plaque`, `Immatriculation`, `Immat` | facultatif, en majuscules |

Les colonnes **Nom** et **Email** doivent exister. Les mêmes mises en forme et vérifications que dans la fiche client
s'appliquent ; une ligne qui ne les respecte pas est rejetée (ex. téléphone ou email manquant).
**Doublons** : un client dont l'email existe déjà dans la base n'est pas recréé et sa fiche n'est pas modifiée ;
deux lignes du fichier avec le même email ne donnent qu'un seul client. Dans les deux cas, l'ancien numéro de la ligne
ignorée est quand même associé à la fiche retenue, pour que ses locations la retrouvent.

**Locations**

| Champ | Noms de colonne reconnus (exemples) | Règle |
| --- | --- | --- |
| N° client d'origine | `IdClient`, `N° client`, `NumClient`, `Code client` | le client est cherché par ce numéro… |
| Email du client | `Email`, `Mail`, `Email client` | … sinon par son email (au moins l'une des deux colonnes) |
| Arrivée | `Date arrivée`, `Date début`, `Début`, `Du` | **obligatoire** : `05/12/2026`, `05/12/26`, `2026-12-05`, `20261205`, avec ou sans heure (`05/12/2026 15:30`) |
| Départ | `Date départ`, `Date fin`, `Fin`, `Au` | **obligatoire**, même format, après l'arrivée |
| Heure d'arrivée / de départ | `Heure arrivée`, `Heure début` / `Heure départ`, `Heure fin` | facultatives (`15:30`, `15h30`, `1530`) ; sinon heure de la date, sinon **16h00 / 10h00** |
| Tarif TTC | `Tarif`, `Tarif TTC`, `Prix`, `Montant`, `Total TTC` | **obligatoire** : `450`, `450,50`, `1 250,00 €`, `1.250,00` |
| Adultes, Enfants, Bébés | `Adultes`, `Nb adultes`, `Nombre d'enfants`, `Bébés` | facultatifs, vides = 0 |
| Airbnb | `Airbnb`, `Origine`, `Plateforme`, `Source` | `Oui`, `O`, `1`, `X`, `Vrai` ou tout texte contenant « Airbnb » ; sinon non |
| N° de réservation | `N° réservation`, `Réservation`, `Code réservation`, `Référence` | facultatif |

Une date seule avec l'heure `00:00` (fréquent dans les exports Excel) est traitée comme une date sans heure.
**Doublons** : une location est ignorée si son n° de réservation existe déjà, ou si le même client a déjà une location
aux mêmes jours d'arrivée et de départ. Un client introuvable (non importé, ou ligne client rejetée) fait rejeter la location.

## Base de données

Un seul fichier SQLite contient les clients et les locations.

**Clients** (`clients`)

| Champ | Colonne | Règle |
| --- | --- | --- |
| IdClient | `idclient` | numéro automatique unique |
| Nom | `nom` | obligatoire, enregistré en MAJUSCULES |
| Prénom | `prenom` | obligatoire, majuscule initiale (« Jean-Pierre ») |
| Adresse, Code postal, Ville | `adresse`, `code_postal`, `ville` | facultatifs |
| Téléphone | `telephone` | obligatoire, format international : « 06 12 34 56 78 » est enregistré « +33612345678 » et affiché « +33 6 12 34 56 78 » (DOM : +262, +590...) ; un numéro étranger se saisit avec + ou 00 |
| Email | `email` | obligatoire, format d'email vérifié, enregistré en minuscules, unique |
| Plaque d'immatriculation | `plaque_immatriculation` | facultative, enregistrée en majuscules |

**Réglages communs** (`reglages`) : paires clé / valeur partagées par tous les postes : `taux_tva` (10 par défaut),
`telephone_prestataire_principal` et `telephone_prestataire_secondaire` (format international, absents si non renseignés).

**Locations** (`locations`)

| Champ | Colonne | Règle |
| --- | --- | --- |
| IdLocation | `idlocation` | numéro automatique unique |
| Date de début | `date_debut` | obligatoire, date et heure (16h00 par défaut) |
| Date de fin | `date_fin` | obligatoire, date et heure (10h00 par défaut), après le début |
| Client | `idclient` | obligatoire, doit exister |
| Tarif TTC | `tarif_ttc_centimes` | obligatoire, ≥ 0, stocké en centimes pour éviter les arrondis |
| Nombre d'adultes, d'enfants, de bébés | `nombre_adultes`, `nombre_enfants`, `nombre_bebes` | ≥ 0, 0 par défaut |
| Airbnb | `airbnb` | oui / non |
| Numéro de réservation | `numero_reservation` | facultatif |

Un client qui a des locations ne peut pas être supprimé. Les dates sont enregistrées en heure locale
au format `AAAA-MM-JJ HH:MM`. La version du schéma est notée dans `PRAGMA user_version`, ce qui permettra
de faire évoluer la base sans perdre les données (version 2 : ajout de la table `reglages` ;
version 3 : table `clients_origine`, anciens numéros de client retenus par l'import CSV).
Une fois la base mise à jour par une version récente du programme, les anciennes versions refusent de l'ouvrir :
mettez à jour le programme sur tous les postes.

### Emplacement du fichier (réseau local)

Le bouton ⚙ en haut à droite ouvre les **Paramètres**, où l'on choisit le fichier de base
(« Parcourir… » ou saisie directe). Par défaut : `Documents\GestionLocative\gestionlocative.db`.
Le réglage est propre à chaque poste et enregistré dans `%APPDATA%\GestionLocative\parametres.json`.

Pour partager les données entre plusieurs ordinateurs, placer le fichier dans un dossier partagé et indiquer
**le même chemin sur chaque poste**, de préférence en chemin réseau complet (`\\SERVEUR\Partage\gestionlocative.db`)
plutôt qu'une lettre de lecteur, qui peut différer d'un poste à l'autre. Si la base est inaccessible au démarrage
(ordinateur qui partage éteint, réseau coupé), le programme le signale et propose d'en choisir une autre.

### SQLite sur un partage réseau : limites et précautions

SQLite convient très bien à une petite base utilisée par quelques postes, à condition de connaître ses limites :
le verrouillage du fichier repose sur le partage Windows (SMB), et une seule écriture à la fois est possible.
Le programme est réglé de façon prudente :

- **journal en mode `DELETE`** : le mode WAL, plus rapide en local, ne fonctionne pas sur un partage réseau ;
- **aucune connexion gardée ouverte** : chaque lecture ou enregistrement ouvre le fichier puis le referme aussitôt,
  si bien qu'un poste ne bloque jamais les autres en restant ouvert ;
- **attente de 10 secondes** si un autre poste est en train d'écrire, au lieu d'échouer immédiatement ;
- **écriture synchronisée sur disque** (`synchronous = FULL`) et contrôle des clés étrangères.

À éviter :

- placer la base dans un dossier **synchronisé** (OneDrive, Dropbox, Google Drive…) : la copie en arrière-plan peut l'abîmer ;
- éteindre l'ordinateur qui partage le fichier ou couper le réseau pendant un enregistrement ;
- copier le fichier pendant que le programme écrit dedans (pour une sauvegarde, fermer le programme sur tous les postes ou
  faire la copie quand personne ne s'en sert).

Pour 2 à 5 postes qui saisissent de temps en temps, cela fonctionne bien. Si un jour de nombreux postes doivent écrire en même temps,
il faudra passer à un serveur de base de données (PostgreSQL, SQL Server Express…).

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
