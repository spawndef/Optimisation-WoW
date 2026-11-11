using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace OptimisationWoW.Core;

/// <summary>
/// Langue de l'app : français ou anglais.
/// - Dans le code : L.F("texte français", "English text").
/// - Dans la fenêtre : {DynamicResource s.xNN}, rempli par Apply() depuis la table ci-dessous.
/// Au premier lancement, la langue suit celle de Windows ; ensuite, le choix est gardé.
/// </summary>
public static class L
{
    public static bool En { get; private set; }
    public static string F(string fr, string en) => En ? en : fr;
    public static CultureInfo Culture { get; private set; } = new("fr-CA");

    public static string Load()
    {
        var saved = Settings.Get("langue");
        if (saved == "fr" || saved == "en") return saved;
        return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
    }

    public static void Save(string lang) => Settings.Set("langue", lang);

    public static void Apply(Application app, string lang)
    {
        En = lang == "en";
        Culture = new CultureInfo(En ? "en-US" : "fr-CA");
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = Culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = Culture;
        foreach (var (key, t) in Window) app.Resources[key] = En ? t.en : t.fr;
    }

    /// <summary>Textes de la fenêtre (MainWindow.xaml).</summary>
    static readonly Dictionary<string, (string fr, string en)> Window = new()
    {
        ["s.x01"] = ("Optimisation de Windows pour World of Warcraft", "Windows Optimizer for World of Warcraft"),
        ["s.x02"] = ("Coché = on désactive / on retire", "Checked = turn off / remove"),
        ["s.x03"] = ("Ouvrir Paramètres", "Open Settings"),
        ["s.x04"] = ("Voir le message", "Show message"),
        ["s.x05"] = ("Fermer", "Close"),
        ["s.x06"] = ("Réduire", "Minimize"),
        ["s.x07"] = ("Agrandir", "Maximize"),
        ["s.x08"] = ("Optimisation de Windows", "Windows Optimizer"),
        ["s.x09"] = ("pour World of Warcraft", "for World of Warcraft"),
        ["s.x10"] = ("TON PC", "YOUR PC"),
        ["s.x11"] = ("Lecture…", "Reading…"),
        ["s.x12"] = ("Chaque réglage est sauvegardé avant d'être changé.", "Every setting is backed up before it changes."),
        ["s.x13"] = ("Sauvegardes", "Backups"),
        ["s.x14"] = ("World of Warcraft", "World of Warcraft"),
        ["s.x15"] = ("Windows", "Windows"),
        ["s.x16"] = ("Journal Windows", "Windows Event Log"),
        ["s.x17"] = ("Applis ouvertes", "Open apps"),
        ["s.x18"] = ("Choisir le dossier de WoW", "Choose WoW folder"),
        ["s.x19"] = ("Relire", "Refresh"),
        ["s.x20"] = ("Les réglages qui comptent pour WoW. Ceux de Windows sont dans l'onglet « Windows ».", "The settings that matter for WoW. Windows settings are in the “Windows” tab."),
        ["s.x21"] = ("WoW introuvable", "WoW not found"),
        ["s.x22"] = ("Clique « Choisir le dossier de WoW » et montre le dossier « World of Warcraft » (celui qui contient _retail_, _classic_…).", "Click “Choose WoW folder” and pick the “World of Warcraft” folder (the one that contains _retail_, _classic_…)."),
        ["s.x23"] = ("WoW refait son cache tout seul. Ça corrige souvent les bugs d'affichage et les chargements bizarres.", "WoW rebuilds its cache by itself. This often fixes display glitches and weird loading issues."),
        ["s.x24"] = ("Vider le cache de WoW", "Clear WoW cache"),
        ["s.x25"] = ("Cocher les recommandations", "Check recommended"),
        ["s.x26"] = ("Réglages du jeu", "Game settings"),
        ["s.x27"] = ("Écrits dans le fichier Config.wtf de WoW. WoW doit être fermé pour les changer : il réécrit ses réglages en quittant. Une copie du fichier d'origine est gardée à côté.", "Written to WoW's Config.wtf file. WoW must be closed to change them: it rewrites its settings when it exits. A copy of the original file is kept next to it."),
        ["s.x28"] = ("Application gratuite et non officielle, sans lien avec Blizzard Entertainment. World of Warcraft est une marque de Blizzard Entertainment, Inc.", "Free, unofficial application, not affiliated with Blizzard Entertainment. World of Warcraft is a trademark of Blizzard Entertainment, Inc."),
        ["s.x29"] = ("Annuler", "Cancel"),
        ["s.x30"] = ("Appliquer", "Apply"),
        ["s.x31"] = ("Aucun changement en attente.", "No pending changes."),
        ["s.x32"] = ("Relire l'état", "Re-read status"),
        ["s.x33"] = ("Optimisation", "Optimization"),
        ["s.x34"] = ("Coché", "Checked"),
        ["s.x35"] = (" = on fait le ménage (désactivé, bloqué ou retiré). Rien ne change tant que tu n'as pas cliqué « Appliquer ».", " = clean it up (turned off, blocked or removed). Nothing changes until you click “Apply”."),
        ["s.x36"] = ("Tu ne sais pas quoi choisir ?", "Not sure what to pick?"),
        ["s.x37"] = ("Clique sur le bouton : on coche seulement ce qui est", "Click the button: we only check what is"),
        ["s.x38"] = (" sans risque ", " safe "),
        ["s.x39"] = ("pour ton PC et tes jeux. Vérifie la liste, puis clique « Appliquer » en bas.", "for your PC and your games. Review the list, then click “Apply” at the bottom."),
        ["s.x40"] = ("Afficher :", "Show:"),
        ["s.x41"] = ("★ Recommandé seulement", "★ Recommended only"),
        ["s.x42"] = ("Tout voir", "Show all"),
        ["s.x43"] = ("★ Recommandé", "★ Recommended"),
        ["s.x44"] = ("sans risque, à cocher     ", "safe, check it     "),
        ["s.x45"] = ("Au choix", "Your choice"),
        ["s.x46"] = ("dépend si tu t'en sers     ", "depends if you use it     "),
        ["s.x47"] = ("À garder", "Keep"),
        ["s.x48"] = ("utile au PC ou aux jeux, n'y touche pas", "needed by your PC or games, leave it alone"),
        ["s.x49"] = ("Lecture des réglages…", "Reading settings…"),
        ["s.x50"] = ("Tout cocher", "Check all"),
        ["s.x51"] = ("Vider", "Clear"),
        ["s.x52"] = ("Nettoyage", "Cleanup"),
        ["s.x53"] = ("Fichiers temporaires : calcul…", "Temporary files: measuring…"),
        ["s.x54"] = ("24 h", "24 h"),
        ["s.x55"] = ("7 jours", "7 days"),
        ["s.x56"] = ("30 jours", "30 days"),
        ["s.x57"] = ("Relire le journal", "Re-read the log"),
        ["s.x58"] = ("Les erreurs et avertissements de Windows, regroupés et expliqués en français.", "Windows errors and warnings, grouped and explained in plain words."),
        ["s.x59"] = ("RÉSUMÉ", "SUMMARY"),
        ["s.x60"] = ("Lecture du journal…", "Reading the log…"),
        ["s.x61"] = ("Redémarrages imprévus", "Unexpected restarts"),
        ["s.x62"] = ("Écrans bleus", "Blue screens"),
        ["s.x63"] = ("Plantages du pilote graphique", "Graphics driver crashes"),
        ["s.x64"] = ("Erreurs matérielles", "Hardware errors"),
        ["s.x65"] = ("Problèmes de disque", "Disk problems"),
        ["s.x66"] = ("Applis plantées", "Crashed apps"),
        ["s.x67"] = ("Montrer les messages sans gravité", "Show harmless messages"),
        ["s.x68"] = ("Détail", "Details"),
        ["s.x69"] = ("Aucun message pour cette période. 🎉", "No messages for this period. 🎉"),
        ["s.x70"] = ("Gestionnaire des tâches", "Task Manager"),
        ["s.x71"] = ("Actualiser", "Refresh"),
        ["s.x72"] = ("Les programmes hors Microsoft qui prennent le plus de mémoire. Un gros programme « sans fenêtre » a souvent été mal fermé (un jeu, un launcher) : ferme-le avant de jouer.", "Non-Microsoft programs using the most memory. A big program “without a window” was often not closed properly (a game, a launcher): close it before you play."),
        ["s.x73"] = ("MÉMOIRE DU PC (RAM)", "PC MEMORY (RAM)"),
    };
}
