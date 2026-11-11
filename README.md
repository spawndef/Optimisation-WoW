# ⚔ Optimisation de Windows pour World of Warcraft

Application **gratuite** pour Windows 10 et 11 qui prépare ton PC pour **World of Warcraft** : moins de saccades, moins de pubs Windows, et un œil sur ce qui mange ta mémoire pendant que tu joues.

Thème **Horde** ou **Alliance** au choix, en un clic. 🛡

| Horde | Alliance |
|---|---|
| ![Thème Horde](docs/horde.png) | ![Thème Alliance](docs/alliance.png) |

> Application non officielle, sans lien avec Blizzard Entertainment. *World of Warcraft* est une marque de Blizzard Entertainment, Inc.

---

## Ce que fait l'app

### 🎮 World of Warcraft
Trouve WoW tout seul (Retail, Classic, Classic Era, Forever…) et propose, avec une case à cocher pour chaque :

| Réglage | Pourquoi |
|---|---|
| **Limite de FPS = ton écran** (ex. 120 Hz) | La carte graphique ne tourne plus à 100 % pour rien : moins de chaleur, moins de saccades. |
| **30 FPS en arrière-plan** | WoW laisse la place au reste quand tu vas sur Discord ou le navigateur. |
| **DirectX 12** | Le moteur le plus récent de WoW. |
| **Carte graphique puissante pour WoW** | Windows n'utilise jamais l'écran intégré au processeur pour WoW. |
| **Échelle de rendu 80 %** *(au choix)* | Gros gain de FPS en 1440p et 4K. |
| **VSync désactivée** *(au choix)* | Moins de délai avec la limite de FPS. |
| **Vider le cache de WoW** | Corrige souvent les bugs d'affichage et de chargement. |

Les réglages sont écrits dans `WTF\Config.wtf`. **WoW doit être fermé** (il réécrit ses réglages en quittant). Une copie du fichier d'origine est gardée à côté.

### 🪟 Windows
- Game DVR (enregistrement en arrière-plan), Mode Jeu
- Pubs et « suggestions » de Windows, applis installées en douce, recommandations du menu Démarrer, pub ciblée
- Bing et « faits du jour » dans la recherche, bouton Copilot, widgets
- Programmes lancés au démarrage (bloquer / débloquer)
- Désinstallation des applis Microsoft inutiles (jamais les applis Xbox : des jeux en ont besoin)
- Nettoyage des fichiers temporaires
- **★ Cocher les recommandations** : pour ceux qui ne savent pas quoi choisir, seulement ce qui est sans risque

### 📋 Journal Windows
Les erreurs de Windows regroupées et **expliquées en français** : redémarrages imprévus, écrans bleus, plantages du pilote graphique, erreurs matérielles, disques, applis plantées. Avec un résumé et un verdict.

### 📦 Applis ouvertes
Les programmes **hors Microsoft** qui prennent le plus de mémoire, avec un bouton **Fermer**. Signale en jaune :
- les gros programmes **sans fenêtre**, souvent un jeu ou un launcher mal fermé ;
- un **autre compte Windows resté ouvert** en arrière-plan (« Changer d'utilisateur ») qui garde sa mémoire.

---

## Sécurité
- **Pas besoin des droits administrateur** : l'app ne change que les réglages de ton compte Windows et les fichiers de WoW.
- **Rien ne change sans ton clic** sur « Appliquer ». Chaque case montre l'état réel.
- **Sauvegarde automatique** des réglages avant chaque changement (bouton « Sauvegardes »), et copie de `Config.wtf`.
- Aucune donnée envoyée sur Internet. Aucune pub. Code ouvert.

## Installer
1. Télécharge `Optimisation-WoW.exe` dans les [Releases](../../releases).
2. Lance-le. C'est tout.

## Compiler soi-même
Il faut le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Crédits
- Fait par **GhostWalk Studio**.
- Police **Cinzel** et **Cinzel Decorative** © The Cinzel Project Authors, sous [SIL Open Font License 1.1](Fonts/OFL.txt).
- Icônes : Segoe Fluent Icons (incluses dans Windows).

## Licence
[MIT](LICENSE) : libre d'utiliser, modifier et partager.

---

### English
Free, unofficial Windows optimizer for World of Warcraft (French UI). FPS cap, background FPS, DirectX 12, GPU preference, WoW cache cleanup, Windows debloat, event log explained, memory hogs and forgotten Windows sessions. Horde or Alliance theme. No admin rights needed. Not affiliated with Blizzard Entertainment.
