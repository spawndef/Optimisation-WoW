using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimisationWoW.Core;

/// <summary>Tout ce que l'app sait optimiser — ce qu'on a fait à la main ensemble, en cases à cocher.</summary>
public static class Catalog
{
    const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    public static List<Item> Tweaks() => new()
    {
        // ── Jeu
        new RegTweak { Section = "Jeu", Title = "Enregistrement en arrière-plan (Game DVR)",
            Desc = "Windows filme tes parties en continu pour « capturer ce qui vient de se passer ». Ça coûte des FPS.",
            Values = { new(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                       new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1) } },
        new InvertedTweak { Section = "Jeu", Title = "Mode Jeu de Windows",
            Desc = "Coché = Mode Jeu ACTIVÉ : Windows donne la priorité au jeu et repousse les mises à jour pendant que tu joues.",
            Values = { new(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, 0),
                       new(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, 0) } },

        // ── Pubs et suggestions
        new RegTweak { Section = "Pubs et suggestions", Title = "Pubs et « suggestions » de Windows",
            Desc = "Dans le menu Démarrer, les Paramètres, les notifications et l'écran de verrouillage.",
            Values = new[] { "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
                             "SubscribedContent-310093Enabled", "SubscribedContent-338393Enabled", "SubscribedContent-353694Enabled",
                             "SubscribedContent-353696Enabled", "SubscribedContent-353698Enabled", "SubscribedContent-88000326Enabled",
                             "SystemPaneSuggestionsEnabled", "SoftLandingEnabled", "RotatingLockScreenOverlayEnabled", "ContentDeliveryAllowed",
                             "FeatureManagementEnabled" }
                     .Select(n => new RegVal(Cdm, n, 0, 1)).ToList() },
        new RegTweak { Section = "Pubs et suggestions", Title = "Applis installées en douce",
            Desc = "Windows installe tout seul Candy Crush, TikTok, Spotify… dans le menu Démarrer.",
            Values = new[] { "SilentInstalledAppsEnabled", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled" }
                     .Select(n => new RegVal(Cdm, n, 0, 1)).ToList() },
        new RegTweak { Section = "Pubs et suggestions", Title = "Recommandations du menu Démarrer",
            Desc = "Les fichiers récents « recommandés » et les rappels de compte Microsoft.",
            Values = { new(Adv, "Start_IrisRecommendations", 0, 1), new(Adv, "Start_AccountNotifications", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = "Pub ciblée (identifiant publicitaire)",
            Desc = "L'identifiant qui sert aux applis à te montrer de la pub personnalisée.",
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1),
                       new(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = "Écran « Finissons de configurer votre appareil »",
            Desc = "La page plein écran qui revient après les mises à jour pour te vendre Microsoft 365 et OneDrive.",
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = "Pubs OneDrive dans l'Explorateur",
            Desc = "Les bandeaux « Essayez OneDrive » en haut des dossiers.",
            Values = { new(Adv, "ShowSyncProviderNotifications", 0, 1) } },

        // ── Recherche
        new RegTweak { Section = "Recherche et barre des tâches", Title = "Bing dans la recherche Windows",
            Desc = "Quand tu cherches un fichier, Windows envoie aussi ta recherche à Bing.",
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = "« Faits du jour » dans la recherche",
            Desc = "Les images et les actus qui s'affichent dans la case de recherche.",
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, 1) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = "Bouton Copilot", Reco = Reco.AuChoix,
            Desc = "Le bouton Copilot dans la barre des tâches.",
            Values = { new(Adv, "ShowCopilotButton", 0, 1) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = "Widgets et news", Reco = Reco.AuChoix,
            Desc = "Le panneau météo / actus de la barre des tâches. Windows protège ce réglage : si ça bloque, utilise Paramètres.",
            SettingsUri = "ms-settings:taskbar",
            Values = { new(Adv, "TaskbarDa", 0, 1) } },
    };

    // ── Applis Microsoft (on n'y met jamais les applis Xbox : des jeux en ont besoin)
    static readonly (string pkg, string title, string desc)[] AppList =
    {
        ("Microsoft.BingNews", "Actualités (News)", "Les news Microsoft. Les mêmes que dans les widgets."),
        ("Microsoft.BingWeather", "Météo", "L'appli Météo de Microsoft."),
        ("Microsoft.BingSearch", "Bing Search", "Le moteur Bing intégré à Windows."),
        ("Microsoft.MicrosoftSolitaireCollection", "Solitaire", "Jeux de cartes avec pubs."),
        ("Clipchamp.Clipchamp", "Clipchamp", "Montage vidéo en ligne de Microsoft."),
        ("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office Hub)", "Raccourci qui pousse l'abonnement Office."),
        ("Microsoft.PowerAutomateDesktop", "Power Automate", "Outil d'automatisation pour les entreprises."),
        ("Microsoft.Todos", "Microsoft To Do", "Liste de tâches."),
        ("Microsoft.GetHelp", "Obtenir de l'aide", "Le support en ligne de Microsoft."),
        ("Microsoft.WindowsFeedbackHub", "Hub de commentaires", "Pour envoyer des avis à Microsoft."),
        ("Microsoft.Edge.GameAssist", "Edge Game Assist", "Un navigateur Edge par-dessus tes jeux."),
        ("Microsoft.Getstarted", "Astuces", "Les conseils de démarrage de Windows."),
        ("Microsoft.WindowsMaps", "Cartes", "L'appli Cartes de Microsoft."),
        ("Microsoft.People", "Contacts", "Ancienne appli Contacts."),
        ("Microsoft.ZuneVideo", "Films et TV", "Ancien lecteur vidéo."),
        ("Microsoft.YourPhone", "Lien avec Windows (Phone Link)", "Relie ton téléphone au PC. Garde-le si tu t'en sers."),
        ("MSTeams", "Microsoft Teams", "Garde-le si tu t'en sers pour le travail ou l'école."),
        ("Microsoft.OutlookForWindows", "Outlook (nouveau)", "Garde-le si c'est ta boîte courriel."),
    };

    // Applis que des gens utilisent vraiment : on ne les recommande pas.
    static readonly HashSet<string> AppsAuChoix = new(StringComparer.OrdinalIgnoreCase)
        { "Microsoft.YourPhone", "MSTeams", "Microsoft.OutlookForWindows" };

    public static List<AppItem> Apps() => AppList
        .Select(a => new AppItem
        {
            Section = "Applis Microsoft", Package = a.pkg, Title = a.title, Desc = a.desc,
            Reco = AppsAuChoix.Contains(a.pkg) ? Reco.AuChoix : Reco.Recommande,
        })
        .ToList();

    public static async Task RefreshApps(IEnumerable<AppItem> apps)
    {
        var (_, output) = await Shell.PowerShellAsync("Get-AppxPackage | ForEach-Object { $_.Name }");
        var installed = new HashSet<string>(
            output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
        foreach (var a in apps) a.SetInstalled(installed.Contains(a.Package));
    }

    // ── Programmes au démarrage
    static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Discord"] = "Discord (chat). Tu peux l'ouvrir toi-même quand tu en as besoin.",
        ["Steam"] = "Launcher Steam.",
        ["EpicGamesLauncher"] = "Launcher Epic Games.",
        ["Battle.net"] = "Launcher Battle.net.",
        ["EADM"] = "EA app (launcher EA).",
        ["RiotClient"] = "Launcher Riot Games.",
        ["OneDrive"] = "Synchro OneDrive.",
        ["electron.app.LM Studio"] = "LM Studio (IA locale). Peut occuper de la mémoire de ta carte graphique.",
        ["SecurityHealth"] = "Icône de Sécurité Windows. À garder.",
        ["Riot Vanguard"] = "Anti-triche de Valorant / LoL. Nécessaire pour y jouer.",
        ["RtkAudUService"] = "Service audio Realtek.",
        ["Corsair iCUE5 Software"] = "Corsair iCUE (lumières et ventilateurs).",
    };

    static readonly Dictionary<string, string> StoreNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSTeams"] = "Microsoft Teams",
        ["Microsoft.YourPhone"] = "Lien avec Windows (Phone Link)",
        ["Microsoft.Copilot"] = "Copilot",
        ["Microsoft.OutlookForWindows"] = "Outlook",
        ["Microsoft.GamingApp"] = "Application Xbox",
        ["SpotifyAB.SpotifyMusic"] = "Spotify",
        ["MicrosoftWindows.CrossDevice"] = "Appareils connectés (téléphone)",
        ["OpenAI.ChatGPT-Desktop"] = "ChatGPT",
        ["OpenAI.Codex"] = "Codex (OpenAI)",
        ["Microsoft.WindowsTerminal"] = "Terminal Windows",
    };

    // Launchers et programmes lourds qui n'ont pas besoin de démarrer avec Windows (on les ouvre quand on veut).
    static readonly string[] StartRecommande =
        { "EpicGamesLauncher", "Battle.net", "EADM", "RiotClient", "electron.app.LM Studio", "MicrosoftEdgeAutoLaunch",
          "MSTeams", "OpenAI.Codex", "OpenAI.ChatGPT-Desktop", "Microsoft.GamingApp" };
    // Nécessaires au PC ou aux jeux.
    static readonly string[] StartAGarder =
        { "SecurityHealth", "Riot Vanguard", "RtkAudUService" };

    static Reco StartReco(string key) =>
        StartAGarder.Any(k => key.StartsWith(k, StringComparison.OrdinalIgnoreCase)) ? Reco.AGarder
        : StartRecommande.Any(k => key.StartsWith(k, StringComparison.OrdinalIgnoreCase)) ? Reco.Recommande
        : Reco.AuChoix;

    public static List<StartupItem> Startup()
    {
        var list = new List<StartupItem>();

        void FromRun(RegistryKey root, StartKind kind)
        {
            using var k = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (k == null) return;
            foreach (var n in k.GetValueNames())
            {
                var cmd = k.GetValue(n) as string ?? "";
                if (!cmd.Contains('\\') && !cmd.Contains(".exe", StringComparison.OrdinalIgnoreCase)) continue; // pas une commande
                list.Add(new StartupItem
                {
                    Section = "Démarrage", Kind = kind, ValueName = n, Reco = StartReco(n),
                    Title = n.StartsWith("MicrosoftEdgeAutoLaunch") ? "Microsoft Edge (pré-lancement)" : n,
                    Desc = Hints.TryGetValue(n, out var h) ? h : ShortCmd(cmd),
                });
            }
        }
        FromRun(Registry.CurrentUser, StartKind.RunUser);
        FromRun(Registry.LocalMachine, StartKind.RunMachine);

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(folder))
            foreach (var f in Directory.GetFiles(folder).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                list.Add(new StartupItem
                {
                    Section = "Démarrage", Kind = StartKind.StartupFolder, ValueName = Path.GetFileName(f), Reco = Reco.AuChoix,
                    Title = Path.GetFileNameWithoutExtension(f), Desc = "Raccourci dans le dossier Démarrage.",
                });

        const string sad = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
        using (var root = Registry.CurrentUser.OpenSubKey(sad))
        {
            if (root != null)
                foreach (var fam in root.GetSubKeyNames())
                {
                    using var fk = root.OpenSubKey(fam);
                    if (fk == null) continue;
                    foreach (var task in fk.GetSubKeyNames())
                    {
                        using var tk = fk.OpenSubKey(task);
                        if (tk?.GetValue("State") is not int) continue;
                        var pkg = fam.Split('_')[0];
                        list.Add(new StartupItem
                        {
                            Section = "Démarrage", Kind = StartKind.StoreApp, TaskKey = $@"{sad}\{fam}\{task}", Reco = StartReco(pkg),
                            Title = StoreNames.TryGetValue(pkg, out var nice) ? nice : pkg,
                            Desc = "Appli du Microsoft Store qui peut démarrer avec Windows.",
                        });
                    }
                }
        }

        foreach (var s in list) s.Refresh();
        return list.OrderBy(s => s.Kind == StartKind.RunMachine).ThenBy(s => s.Title).ToList();
    }

    static string ShortCmd(string cmd)
    {
        var c = cmd.Trim().Trim('"');
        var exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe > 0) c = c[..(exe + 4)];
        return c;
    }

    // ── Fichiers temporaires
    public static long TempSize()
    {
        try
        {
            return new DirectoryInfo(Path.GetTempPath())
                .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                .Sum(f => { try { return f.Length; } catch { return 0L; } });
        }
        catch { return 0; }
    }

    public static long CleanTemp()
    {
        var before = TempSize();
        var dir = new DirectoryInfo(Path.GetTempPath());
        foreach (var f in dir.EnumerateFiles()) { try { f.Delete(); } catch { } }
        foreach (var d in dir.EnumerateDirectories()) { try { d.Delete(true); } catch { } }
        return Math.Max(0, before - TempSize());
    }

    // ── Infos du PC (bandeau du haut)
    public static async Task<string> SystemInfo()
    {
        var (_, o) = await Shell.PowerShellAsync(
            "$c=(Get-CimInstance Win32_Processor).Name.Trim(); " +
            "$g=(Get-CimInstance Win32_VideoController | Sort-Object AdapterRAM -Descending | Where-Object Name -notmatch 'UHD|Iris|Basic' | Select-Object -First 1).Name; " +
            "$m=Get-CimInstance Win32_PhysicalMemory; $gb=[math]::Round(($m | Measure-Object Capacity -Sum).Sum/1GB); $sp=($m | Select-Object -First 1).ConfiguredClockSpeed; " +
            "\"$c  ·  $g  ·  $gb Go de RAM à $sp MHz\"");
        return Shell.FirstLine(o.Replace("(R)", "").Replace("(TM)", "").Replace("13th Gen ", "").Replace("NVIDIA GeForce ", ""));
    }
}

/// <summary>Pour le Mode Jeu : la case cochée l'ACTIVE (c'est l'optimisation), au lieu de désactiver quelque chose.</summary>
public class InvertedTweak : RegTweak
{
    public override string LabelOn => "Activé";
    public override string LabelOff => "Éteint";
}
