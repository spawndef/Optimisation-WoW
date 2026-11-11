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
        new RegTweak { Section = "Jeu", Title = L.F("Enregistrement en arrière-plan (Game DVR)", "Background recording (Game DVR)"),
            Desc = L.F("Windows filme tes parties en continu pour « capturer ce qui vient de se passer ». Ça coûte des FPS.", "Windows keeps recording your games to “capture what just happened”. It costs FPS."),
            Values = { new(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                       new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1) } },
        new InvertedTweak { Section = "Jeu", Title = L.F("Mode Jeu de Windows", "Windows Game Mode"),
            Desc = L.F("Coché = Mode Jeu ACTIVÉ : Windows donne la priorité au jeu et repousse les mises à jour pendant que tu joues.", "Checked = Game Mode ON: Windows gives priority to the game and holds updates while you play."),
            Values = { new(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, 0),
                       new(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, 0) } },

        // ── Pubs et suggestions
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Pubs et « suggestions » de Windows", "Windows ads and “suggestions”"),
            Desc = L.F("Dans le menu Démarrer, les Paramètres, les notifications et l'écran de verrouillage.", "In the Start menu, Settings, notifications and the lock screen."),
            Values = new[] { "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
                             "SubscribedContent-310093Enabled", "SubscribedContent-338393Enabled", "SubscribedContent-353694Enabled",
                             "SubscribedContent-353696Enabled", "SubscribedContent-353698Enabled", "SubscribedContent-88000326Enabled",
                             "SystemPaneSuggestionsEnabled", "SoftLandingEnabled", "RotatingLockScreenOverlayEnabled", "ContentDeliveryAllowed",
                             "FeatureManagementEnabled" }
                     .Select(n => new RegVal(Cdm, n, 0, 1)).ToList() },
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Applis installées en douce", "Silently installed apps"),
            Desc = L.F("Windows installe tout seul Candy Crush, TikTok, Spotify… dans le menu Démarrer.", "Windows installs Candy Crush, TikTok, Spotify… in the Start menu by itself."),
            Values = new[] { "SilentInstalledAppsEnabled", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled" }
                     .Select(n => new RegVal(Cdm, n, 0, 1)).ToList() },
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Recommandations du menu Démarrer", "Start menu recommendations"),
            Desc = L.F("Les fichiers récents « recommandés » et les rappels de compte Microsoft.", "“Recommended” recent files and Microsoft account reminders."),
            Values = { new(Adv, "Start_IrisRecommendations", 0, 1), new(Adv, "Start_AccountNotifications", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Pub ciblée (identifiant publicitaire)", "Targeted ads (advertising ID)"),
            Desc = L.F("L'identifiant qui sert aux applis à te montrer de la pub personnalisée.", "The ID apps use to show you personalized ads."),
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1),
                       new(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Écran « Finissons de configurer votre appareil »", "“Let's finish setting up your device” screen"),
            Desc = L.F("La page plein écran qui revient après les mises à jour pour te vendre Microsoft 365 et OneDrive.", "The full-screen page that comes back after updates to sell you Microsoft 365 and OneDrive."),
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1) } },
        new RegTweak { Section = "Pubs et suggestions", Title = L.F("Pubs OneDrive dans l'Explorateur", "OneDrive ads in File Explorer"),
            Desc = L.F("Les bandeaux « Essayez OneDrive » en haut des dossiers.", "The “Try OneDrive” banners at the top of folders."),
            Values = { new(Adv, "ShowSyncProviderNotifications", 0, 1) } },

        // ── Recherche
        new RegTweak { Section = "Recherche et barre des tâches", Title = L.F("Bing dans la recherche Windows", "Bing in Windows Search"),
            Desc = L.F("Quand tu cherches un fichier, Windows envoie aussi ta recherche à Bing.", "When you search for a file, Windows also sends your search to Bing."),
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = L.F("« Faits du jour » dans la recherche", "Search highlights"),
            Desc = L.F("Les images et les actus qui s'affichent dans la case de recherche.", "The pictures and news shown in the search box."),
            Values = { new(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, 1) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = L.F("Bouton Copilot", "Copilot button"), Reco = Reco.AuChoix,
            Desc = L.F("Le bouton Copilot dans la barre des tâches.", "The Copilot button on the taskbar."),
            Values = { new(Adv, "ShowCopilotButton", 0, 1) } },
        new RegTweak { Section = "Recherche et barre des tâches", Title = L.F("Widgets et news", "Widgets and news"), Reco = Reco.AuChoix,
            Desc = L.F("Le panneau météo / actus de la barre des tâches. Windows protège ce réglage : si ça bloque, utilise Paramètres.", "The weather / news panel on the taskbar. Windows protects this setting: if it is blocked, use Settings."),
            SettingsUri = "ms-settings:taskbar",
            Values = { new(Adv, "TaskbarDa", 0, 1) } },
    };

    // ── Applis Microsoft (on n'y met jamais les applis Xbox : des jeux en ont besoin)
    static (string pkg, string title, string desc)[] AppList => new[]
    {
        ("Microsoft.BingNews", L.F("Actualités (News)", "News"), L.F("Les news Microsoft. Les mêmes que dans les widgets.", "Microsoft News. The same as in widgets.")),
        ("Microsoft.BingWeather", L.F("Météo", "Weather"), L.F("L'appli Météo de Microsoft.", "Microsoft's Weather app.")),
        ("Microsoft.BingSearch", "Bing Search", L.F("Le moteur Bing intégré à Windows.", "Bing built into Windows.")),
        ("Microsoft.MicrosoftSolitaireCollection", "Solitaire", L.F("Jeux de cartes avec pubs.", "Card games with ads.")),
        ("Clipchamp.Clipchamp", "Clipchamp", L.F("Montage vidéo en ligne de Microsoft.", "Microsoft's online video editor.")),
        ("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office Hub)", L.F("Raccourci qui pousse l'abonnement Office.", "A shortcut that pushes the Office subscription.")),
        ("Microsoft.PowerAutomateDesktop", "Power Automate", L.F("Outil d'automatisation pour les entreprises.", "Automation tool for businesses.")),
        ("Microsoft.Todos", "Microsoft To Do", L.F("Liste de tâches.", "To-do list.")),
        ("Microsoft.GetHelp", L.F("Obtenir de l'aide", "Get Help"), L.F("Le support en ligne de Microsoft.", "Microsoft's online support.")),
        ("Microsoft.WindowsFeedbackHub", L.F("Hub de commentaires", "Feedback Hub"), L.F("Pour envoyer des avis à Microsoft.", "To send feedback to Microsoft.")),
        ("Microsoft.Edge.GameAssist", "Edge Game Assist", L.F("Un navigateur Edge par-dessus tes jeux.", "An Edge browser on top of your games.")),
        ("Microsoft.Getstarted", L.F("Astuces", "Tips"), L.F("Les conseils de démarrage de Windows.", "Windows getting-started tips.")),
        ("Microsoft.WindowsMaps", L.F("Cartes", "Maps"), L.F("L'appli Cartes de Microsoft.", "Microsoft's Maps app.")),
        ("Microsoft.People", L.F("Contacts", "People"), L.F("Ancienne appli Contacts.", "Old People app.")),
        ("Microsoft.ZuneVideo", L.F("Films et TV", "Movies & TV"), L.F("Ancien lecteur vidéo.", "Old video player.")),
        ("Microsoft.YourPhone", L.F("Lien avec Windows (Phone Link)", "Phone Link"), L.F("Relie ton téléphone au PC. Garde-le si tu t'en sers.", "Links your phone to the PC. Keep it if you use it.")),
        ("MSTeams", "Microsoft Teams", L.F("Garde-le si tu t'en sers pour le travail ou l'école.", "Keep it if you use it for work or school.")),
        ("Microsoft.OutlookForWindows", L.F("Outlook (nouveau)", "Outlook (new)"), L.F("Garde-le si c'est ta boîte courriel.", "Keep it if it is your email.")),
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
    static Dictionary<string, string> Hints => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Discord"] = L.F("Discord (chat). Tu peux l'ouvrir toi-même quand tu en as besoin.", "Discord (chat). You can open it yourself when you need it."),
        ["Steam"] = L.F("Launcher Steam.", "Steam launcher."),
        ["EpicGamesLauncher"] = L.F("Launcher Epic Games.", "Epic Games launcher."),
        ["Battle.net"] = L.F("Launcher Battle.net.", "Battle.net launcher."),
        ["EADM"] = L.F("EA app (launcher EA).", "EA app (EA launcher)."),
        ["RiotClient"] = L.F("Launcher Riot Games.", "Riot Games launcher."),
        ["OneDrive"] = L.F("Synchro OneDrive.", "OneDrive sync."),
        ["electron.app.LM Studio"] = L.F("LM Studio (IA locale). Peut occuper de la mémoire de ta carte graphique.", "LM Studio (local AI). Can use your graphics card memory."),
        ["SecurityHealth"] = L.F("Icône de Sécurité Windows. À garder.", "Windows Security icon. Keep it."),
        ["Riot Vanguard"] = L.F("Anti-triche de Valorant / LoL. Nécessaire pour y jouer.", "Valorant / LoL anti-cheat. Needed to play them."),
        ["RtkAudUService"] = L.F("Service audio Realtek.", "Realtek audio service."),
        ["Corsair iCUE5 Software"] = L.F("Corsair iCUE (lumières et ventilateurs).", "Corsair iCUE (lighting and fans)."),
    };

    static Dictionary<string, string> StoreNames => new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSTeams"] = "Microsoft Teams",
        ["Microsoft.YourPhone"] = L.F("Lien avec Windows (Phone Link)", "Phone Link"),
        ["Microsoft.Copilot"] = "Copilot",
        ["Microsoft.OutlookForWindows"] = "Outlook",
        ["Microsoft.GamingApp"] = L.F("Application Xbox", "Xbox app"),
        ["SpotifyAB.SpotifyMusic"] = "Spotify",
        ["MicrosoftWindows.CrossDevice"] = L.F("Appareils connectés (téléphone)", "Cross-device (phone)"),
        ["OpenAI.ChatGPT-Desktop"] = "ChatGPT",
        ["OpenAI.Codex"] = "Codex (OpenAI)",
        ["Microsoft.WindowsTerminal"] = L.F("Terminal Windows", "Windows Terminal"),
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
                    Title = n.StartsWith("MicrosoftEdgeAutoLaunch") ? L.F("Microsoft Edge (pré-lancement)", "Microsoft Edge (pre-launch)") : n,
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
                    Title = Path.GetFileNameWithoutExtension(f), Desc = L.F("Raccourci dans le dossier Démarrage.", "Shortcut in the Startup folder."),
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
                            Desc = L.F("Appli du Microsoft Store qui peut démarrer avec Windows.", "Microsoft Store app that can start with Windows."),
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
            "\"$c  ·  $g  ·  $gb " + L.F("Go de RAM à", "GB RAM at") + " $sp MHz\"");
        return Shell.FirstLine(o.Replace("(R)", "").Replace("(TM)", "").Replace("13th Gen ", "").Replace("NVIDIA GeForce ", ""));
    }
}

/// <summary>Pour le Mode Jeu : la case cochée l'ACTIVE (c'est l'optimisation), au lieu de désactiver quelque chose.</summary>
public class InvertedTweak : RegTweak
{
    public override string LabelOn => L.F("Activé", "On");
    public override string LabelOff => L.F("Éteint", "Off");
}
