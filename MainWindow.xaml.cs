using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OptimisationWoW.Core;

namespace OptimisationWoW;

public class Section
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public bool AllowAll { get; init; } = true;
    public List<Item> Items { get; init; } = new();
}

public partial class MainWindow : Window
{
    List<Item> tweaks = new();
    List<AppItem> apps = new();
    List<StartupItem> startup = new();
    IEnumerable<Item> All => tweaks.Concat(apps).Concat(startup);

    JournalResult? journal;
    bool busy;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) =>
            MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
        appsTimer.Tick += async (_, _) => await LoadApps();
        Loaded += async (_, _) =>
        {
            SysInfo.Text = await Catalog.SystemInfo();
        };
        Loaded += async (_, _) => await LoadOptimisation();
        Loaded += async (_, _) => await UpdateTemp();
        Loaded += async (_, _) => await LoadWow();

        // Faction et langue actuelles : cochées sans déclencher de changement.
        loadingFaction = true;
        (Theme.Load() == Theme.Alliance ? FactionAlliance : FactionHorde).IsChecked = true;
        (L.En ? LangEn : LangFr).IsChecked = true;
        loadingFaction = false;
    }

    static string Pending(int n) => n == 0
        ? L.F("Aucun changement en attente.", "No pending changes.")
        : L.F($"{n} changement{(n > 1 ? "s" : "")} en attente", $"{n} pending change{(n > 1 ? "s" : "")}");

    // ═════════════════════════════ Langue ═════════════════════════════

    /// <summary>FR ↔ EN tout de suite : les textes de la fenêtre changent seuls, ceux faits par le code sont refaits.</summary>
    async void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (loadingFaction || LangFr == null) return;
        var lang = LangEn.IsChecked == true ? "en" : "fr";
        if ((lang == "en") == L.En) return;
        L.Save(lang);
        L.Apply(Application.Current, lang);

        ResultText.Visibility = WowResultText.Visibility = AppsResult.Visibility = Visibility.Collapsed;
        SysInfo.Text = await Catalog.SystemInfo();
        await LoadWow();
        await LoadOptimisation();
        await UpdateTemp();
        if (journal != null) await LoadJournal();
        if (AppsPage.Visibility == Visibility.Visible) await LoadApps();
    }

    // ═════════════════════════════ Faction ═════════════════════════════

    bool loadingFaction;

    void Faction_Checked(object sender, RoutedEventArgs e)
    {
        if (loadingFaction || FactionHorde == null) return;
        var faction = FactionAlliance.IsChecked == true ? Theme.Alliance : Theme.Horde;
        if (faction != Theme.Load()) Theme.SwitchTo(faction);
    }

    // ═════════════════════════════ World of Warcraft ═════════════════════════════

    List<WowFlavor> flavors = new();
    WowFlavor? flavor;
    List<Item> wowItems = new();
    int hz;

    async Task LoadWow()
    {
        flavors = await Task.Run(Wow.Find);
        if (hz == 0) hz = await Wow.RefreshRate();

        FlavorPills.Children.Clear();
        WowNone.Visibility = flavors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        WowCard.Visibility = WowTweaksCard.Visibility = flavors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (flavors.Count == 0) { wowItems = new(); UpdateWowPending(); return; }

        // Si plusieurs versions (Retail, Classic…), une pastille par version.
        var saved = Settings.Get("wowFlavor");
        var pick = flavors.FirstOrDefault(f => f.Folder.Equals(saved, StringComparison.OrdinalIgnoreCase))
                   ?? flavors.FirstOrDefault(f => f.Running) ?? flavors[0];
        if (flavors.Count > 1)
            foreach (var f in flavors)
            {
                var rb = new RadioButton { Content = f.Name, Tag = f, GroupName = "flavor", Style = (Style)FindResource("Pill"), IsChecked = f == pick };
                rb.Checked += (s, _) => { if ((s as RadioButton)?.Tag is WowFlavor ff) { Settings.Set("wowFlavor", ff.Folder); ShowFlavor(ff); } };
                FlavorPills.Children.Add(rb);
            }
        ShowFlavor(pick);
    }

    void ShowFlavor(WowFlavor f)
    {
        flavor = f;
        foreach (var i in wowItems) i.PropertyChanged -= WowItem_Changed;
        wowItems = Wow.Tweaks(f, hz);
        foreach (var i in wowItems) { i.Refresh(); i.PropertyChanged += WowItem_Changed; }
        WowList.ItemsSource = wowItems;
        RefreshWowCard();
        UpdateWowPending();
    }

    void RefreshWowCard()
    {
        if (flavor == null) return;
        var running = flavor.Running;
        WowTitle.Text = "World of Warcraft · " + flavor.Name;
        WowPath.Text = flavor.Folder;
        WowState.Text = running ? L.F("● WoW est ouvert : ferme-le pour appliquer", "● WoW is running: close it to apply") : L.F("● WoW est fermé : prêt", "● WoW is closed: ready");
        WowState.Foreground = (System.Windows.Media.Brush)FindResource(running ? "Gold" : "Ok");
        WowStatePill.Background = (System.Windows.Media.Brush)FindResource(running ? "GoldSoft" : "OkSoft");
        var cache = flavor.CacheSize();
        WowInfo.Text = $"{flavor.AddOns} addon{(flavor.AddOns > 1 ? "s" : "")}  ·  cache {Size(cache)}  ·  " + L.F("écran", "screen") + $" {hz} Hz";
        CacheBtnWowText.Text = L.F($"Vider le cache ({Size(cache)})", $"Clear cache ({Size(cache)})");
    }

    void WowItem_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Item.Desired)) UpdateWowPending();
    }

    void UpdateWowPending()
    {
        var n = wowItems.Count(i => i.Changed);
        WowPendingText.Text = Pending(n);
        WowApplyBtn.IsEnabled = n > 0;
        WowCancelBtn.IsEnabled = n > 0;
        WowApplyBtn.Content = n > 0 ? L.F($"Appliquer ({n})", $"Apply ({n})") : L.F("Appliquer", "Apply");
    }

    void WowRecommend_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in wowItems.Where(i => i.Reco == Reco.Recommande && i.CanChange)) i.Desired = true;
        WowResultText.Visibility = Visibility.Visible;
        WowResultText.Foreground = (System.Windows.Media.Brush)FindResource("Brand2");
        WowResultText.Text = wowItems.Any(i => i.Changed)
            ? L.F("Les lignes colorées vont changer. Vérifie, puis clique « Appliquer ».", "The highlighted lines will change. Review, then click “Apply”.")
            : L.F("Tout ce qui est recommandé est déjà fait. 👍", "Everything recommended is already done. 👍");
    }

    void WowCancel_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in wowItems.Where(i => i.Changed)) i.Desired = i.Current;
    }

    async void WowApply_Click(object sender, RoutedEventArgs e)
    {
        if (flavor == null) return;
        var todo = wowItems.Where(i => i.Changed).ToList();
        // Config.wtf : WoW doit être fermé. La préférence de carte graphique, elle, peut se faire tout de suite.
        if (flavor.Running && todo.OfType<WtfTweak>().Any())
        {
            MessageBox.Show(this, L.F("Ferme World of Warcraft avant d'appliquer : il réécrit ses réglages en quittant et effacerait les changements.", "Close World of Warcraft before applying: it rewrites its settings when it exits and would erase the changes."),
                L.F("WoW est ouvert", "WoW is running"), MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshWowCard();
            return;
        }
        WowApplyBtn.IsEnabled = false;
        var errors = new List<string>();
        foreach (var item in todo)
        {
            var err = await item.ApplyAsync();
            if (err != null) { errors.Add(item.Title); item.Note = err; }
        }
        foreach (var i in wowItems) i.Refresh();
        UpdateWowPending();
        RefreshWowCard();
        var ok = todo.Count - errors.Count;
        WowResultText.Visibility = Visibility.Visible;
        WowResultText.Foreground = (System.Windows.Media.Brush)FindResource(errors.Count == 0 ? "Ok" : "Gold");
        WowResultText.Text = errors.Count == 0
            ? L.F($"✓ {ok} réglage{(ok > 1 ? "s" : "")} appliqué{(ok > 1 ? "s" : "")}. Lance WoW : c'est pris en compte au démarrage.", $"✓ {ok} setting{(ok > 1 ? "s" : "")} applied. Launch WoW: it takes effect at startup.")
            : L.F($"{ok} appliqué(s), {errors.Count} refusé(s) : voir les notes en jaune.", $"{ok} applied, {errors.Count} refused: see the yellow notes.");
    }

    void WowCache_Click(object sender, RoutedEventArgs e)
    {
        if (flavor == null) return;
        var before = flavor.CacheSize();
        var err = flavor.ClearCache();
        RefreshWowCard();
        WowResultText.Visibility = Visibility.Visible;
        WowResultText.Foreground = (System.Windows.Media.Brush)FindResource(err == null ? "Ok" : "Gold");
        WowResultText.Text = err ?? L.F($"✓ Cache vidé ({Size(before)}). WoW le refera au prochain lancement, le premier chargement sera un peu plus long.", $"✓ Cache cleared ({Size(before)}). WoW will rebuild it next launch; the first load will be a bit longer.");
    }

    async void WowPick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.F("Choisis le dossier « World of Warcraft »", "Choose the “World of Warcraft” folder") };
        if (dlg.ShowDialog(this) != true) return;
        var dir = dlg.FolderName;
        // On accepte aussi un dossier de version (_retail_…) : on remonte d'un cran.
        if (Wow.FlavorsIn(dir).Count == 0 && Directory.GetParent(dir) is { } parent && Wow.FlavorsIn(parent.FullName).Count > 0) dir = parent.FullName;
        if (Wow.FlavorsIn(dir).Count == 0)
        {
            MessageBox.Show(this, L.F("Je ne trouve pas WoW dans ce dossier. Choisis celui qui contient _retail_, _classic_…", "WoW was not found in this folder. Pick the one that contains _retail_, _classic_…"),
                L.F("Dossier de WoW", "WoW folder"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Settings.Set("wowRoot", dir);
        await LoadWow();
    }

    async void WowReload_Click(object sender, RoutedEventArgs e) => await LoadWow();

    // ═════════════════════════════ Optimisation ═════════════════════════════

    async Task LoadOptimisation()
    {
        OptiLoading.Visibility = Visibility.Visible;
        foreach (var i in All) i.PropertyChanged -= Item_Changed;

        tweaks = Catalog.Tweaks();
        foreach (var t in tweaks) t.Refresh();
        startup = await Task.Run(Catalog.Startup);
        apps = Catalog.Apps();
        await Catalog.RefreshApps(apps);

        foreach (var i in All) i.PropertyChanged += Item_Changed;
        BuildSections();
        OptiLoading.Visibility = Visibility.Collapsed;
        UpdatePending();
    }

    /// <summary>Mode simple = seulement les options recommandées (et pas celles déjà faites qui n'ont plus rien à faire).</summary>
    bool Simple => ViewSimple.IsChecked == true;

    void BuildSections()
    {
        bool Show(Item i) => !Simple || (i.Reco == Reco.Recommande && i.CanChange);
        List<Item> Pick(IEnumerable<Item> items) => items.Where(Show).ToList();

        var removed = apps.Count(a => a.Current);
        var sections = new List<Section>
        {
            new() { Title = L.F("Jeu", "Gaming"), Subtitle = L.F("Les réglages qui comptent pour les FPS.", "The settings that matter for FPS."), Items = Pick(tweaks.Where(t => t.Section == "Jeu")) },
            new() { Title = L.F("Pubs et suggestions", "Ads and suggestions"), Subtitle = L.F("Tout ce que Windows affiche pour te vendre quelque chose.", "Everything Windows shows you to sell you something."), Items = Pick(tweaks.Where(t => t.Section == "Pubs et suggestions")) },
            new() { Title = L.F("Recherche et barre des tâches", "Search and taskbar"), Subtitle = L.F("Bing, Copilot, widgets et news.", "Bing, Copilot, widgets and news."), Items = Pick(tweaks.Where(t => t.Section == "Recherche et barre des tâches")) },
            new() { Title = L.F("Démarrage", "Startup"), AllowAll = Simple,
                    Subtitle = L.F("Programmes lancés avec Windows. Cocher = bloqué au démarrage (le programme reste installé, tu peux l'ouvrir toi-même).", "Programs launched with Windows. Checked = blocked at startup (the program stays installed, you can open it yourself)."),
                    Items = Pick(startup) },
            new() { Title = L.F("Applis Microsoft", "Microsoft apps"), AllowAll = Simple,
                    Subtitle = L.F($"Cocher = désinstaller. {removed} déjà absente{(removed > 1 ? "s" : "")} de ce PC. Les applis Xbox ne sont pas listées : des jeux en ont besoin.", $"Checked = uninstall. {removed} already missing from this PC. Xbox apps are not listed: some games need them."),
                    Items = Pick(apps.OrderBy(a => a.Current)) },
        };
        SectionsList.ItemsSource = sections.Where(s => s.Items.Count > 0).ToList();
    }

    void View_Checked(object sender, RoutedEventArgs e)
    {
        if (SectionsList != null && tweaks.Count > 0) BuildSections();
    }

    void Recommend_Click(object sender, RoutedEventArgs e)
    {
        // Coche seulement : on ne décoche jamais ce que tu as choisi toi-même.
        foreach (var i in All.Where(i => i.Reco == Reco.Recommande && i.CanChange)) i.Desired = true;
        var n = All.Count(i => i.Changed);
        ResultText.Visibility = Visibility.Visible;
        ResultText.Foreground = (System.Windows.Media.Brush)FindResource("Brand2");
        ResultText.Text = n == 0
            ? L.F("Tout ce qui est recommandé est déjà fait. 👍", "Everything recommended is already done. 👍")
            : L.F("Les lignes colorées vont changer. Vérifie, puis clique « Appliquer ».", "The highlighted lines will change. Review, then click “Apply”.");
    }

    void Item_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Item.Desired)) UpdatePending();
    }

    void UpdatePending()
    {
        var n = All.Count(i => i.Changed);
        PendingText.Text = Pending(n);
        ApplyBtn.IsEnabled = n > 0 && !busy;
        CancelBtn.IsEnabled = n > 0 && !busy;
        ApplyBtn.Content = n > 0 ? L.F($"Appliquer ({n})", $"Apply ({n})") : L.F("Appliquer", "Apply");
    }

    async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var todo = All.Where(i => i.Changed).ToList();
        var removing = todo.OfType<AppItem>().Where(a => a.Desired).Select(a => a.Title).ToList();
        if (removing.Count > 0 && MessageBox.Show(this,
                L.F("Ces applis vont être désinstallées :", "These apps will be uninstalled:") + "\n\n• " + string.Join("\n• ", removing) +
                "\n\n" + L.F("Tu pourras les réinstaller depuis le Microsoft Store.", "You can reinstall them from the Microsoft Store."),
                L.F("Désinstaller ?", "Uninstall?"), MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        busy = true;
        UpdatePending();
        ApplyBtn.Content = L.F("Application…", "Applying…");
        var errors = new List<string>();
        foreach (var item in todo)
        {
            var err = await item.ApplyAsync();
            if (err != null) errors.Add($"{item.Title} : {err}");
        }

        // On relit l'état réel pour montrer ce qui a vraiment changé.
        foreach (var t in tweaks) t.Refresh();
        foreach (var s in startup) s.Refresh();
        await Catalog.RefreshApps(apps);
        foreach (var item in todo)
        {
            var err = errors.FirstOrDefault(x => x.StartsWith(item.Title + " :"));
            if (err != null) item.Note = err[(item.Title.Length + 3)..];
        }

        busy = false;
        UpdatePending();
        var ok = todo.Count - errors.Count;
        ResultText.Visibility = Visibility.Visible;
        ResultText.Foreground = (System.Windows.Media.Brush)FindResource(errors.Count == 0 ? "Ok" : "Gold");
        ResultText.Text = (errors.Count == 0 ? L.F($"✓ {ok} changement{(ok > 1 ? "s" : "")} appliqué{(ok > 1 ? "s" : "")}. ", $"✓ {ok} change{(ok > 1 ? "s" : "")} applied. ") : L.F($"{ok} appliqué(s), {errors.Count} refusé(s) — voir les notes en jaune. ", $"{ok} applied, {errors.Count} refused — see the yellow notes. "))
                          + L.F("Certains réglages (barre des tâches, menu Démarrer) s'affichent après une déconnexion ou un redémarrage.", "Some settings (taskbar, Start menu) show up after signing out or restarting.");
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in All.Where(i => i.Changed)) i.Desired = i.Current;
    }

    async void Reload_Click(object sender, RoutedEventArgs e)
    {
        ResultText.Visibility = Visibility.Collapsed;
        await LoadOptimisation();
        await UpdateTemp();
    }

    void CheckSection_Click(object sender, RoutedEventArgs e)
    {
        // « Tout cocher » coche, point. Avant, un 2e clic décochait tout, et « Appliquer » remettait les pubs.
        if ((sender as FrameworkElement)?.Tag is Section s)
            foreach (var i in s.Items.Where(i => i.CanChange && i.Reco != Reco.AGarder)) i.Desired = true;
    }

    void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Item { SettingsUri: { } uri }) Shell.Open(uri);
    }

    void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Backup.Folder);
        Shell.Open(Backup.Folder);
    }

    async Task UpdateTemp()
    {
        var size = await Task.Run(Catalog.TempSize);
        TempText.Text = L.F($"Fichiers temporaires de ton compte : {Size(size)}. Les fichiers utilisés par un programme ouvert sont laissés en place.", $"Temporary files in your account: {Size(size)}. Files used by an open program are left alone.");
        CleanBtnText.Text = L.F($"Vider ({Size(size)})", $"Clear ({Size(size)})");
        CleanBtn.IsEnabled = size > 0;
    }

    async void Clean_Click(object sender, RoutedEventArgs e)
    {
        CleanBtn.IsEnabled = false;
        CleanBtnText.Text = L.F("Nettoyage…", "Cleaning…");
        var freed = await Task.Run(Catalog.CleanTemp);
        await UpdateTemp();
        TempText.Text = L.F($"✓ {Size(freed)} libérés. ", $"✓ {Size(freed)} freed. ") + TempText.Text;
    }

    static string Size(long bytes) => bytes >= 1L << 30
        ? $"{bytes / (double)(1L << 30):0.0} " + L.F("Go", "GB")
        : $"{bytes / (double)(1L << 20):0} " + L.F("Mo", "MB");

    // ═════════════════════════════ Journal ═════════════════════════════

    int Hours => P24.IsChecked == true ? 24 : P30.IsChecked == true ? 24 * 30 : 24 * 7;

    async Task LoadJournal()
    {
        VerdictText.Text = L.F("Lecture du journal…", "Reading the log…");
        SummaryText.Text = "";
        EventsList.ItemsSource = null;
        var hours = Hours;
        journal = await Task.Run(() => Journal.Read(hours));

        StatsGrid.DataContext = journal;
        VerdictText.Text = journal.Verdict;
        var key = journal.Level switch { Gravite.Grave => "Bad", Gravite.Attention => "Gold", _ => "Ok" };
        VerdictBar.Background = (System.Windows.Media.Brush)FindResource(key);
        VerdictText.Foreground = VerdictBar.Background;
        SummaryText.Text = journal.Summary + (journal.Error != null ? "\n⚠ " + journal.Error : "");
        ShowEvents();
    }

    void ShowEvents()
    {
        if (journal == null) return;
        var list = journal.Groups.Where(g => ShowNoise.IsChecked == true || !g.IsNoise).ToList();
        EventsList.ItemsSource = list;
        EventsEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DetailSub.Text = L.F($"{journal.Total} message{(journal.Total > 1 ? "s" : "")} regroupés en {journal.Groups.Count} sortes, du plus grave au moins grave.", $"{journal.Total} message{(journal.Total > 1 ? "s" : "")} grouped into {journal.Groups.Count} kinds, most serious first.");
    }

    async void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (OptiPage == null || AppsPage == null || WowPage == null) return; // pendant InitializeComponent
        var journalTab = NavJournal.IsChecked == true;
        var appsTab = NavApps.IsChecked == true;
        WowPage.Visibility = NavWow.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OptiPage.Visibility = NavOpti.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        JournalPage.Visibility = journalTab ? Visibility.Visible : Visibility.Collapsed;
        AppsPage.Visibility = appsTab ? Visibility.Visible : Visibility.Collapsed;

        if (appsTab) { await LoadApps(); appsTimer.Start(); } else appsTimer.Stop();
        if (journalTab && journal == null) await LoadJournal();
    }

    // ═════════════════════════════ Applis ouvertes ═════════════════════════════

    // Rafraîchit la liste toutes les 5 s tant que la page est affichée.
    readonly System.Windows.Threading.DispatcherTimer appsTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    bool closingApp;

    async Task LoadApps()
    {
        if (closingApp) return;
        var list = await Task.Run(() => OpenApps.Scan());
        var others = await Task.Run(OpenApps.OtherSessions);
        var (used, total) = OpenApps.Memory();

        var big = others.Where(o => o.Reserved >= 500L << 20).ToList();
        OtherSessionsCard.Visibility = big.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (big.Count > 0)
        {
            var o = big[0];
            OtherSessionsTitle.Text = big.Count == 1
                ? L.F($"⚠ Le compte « {o.User} » est resté ouvert : {o.Reserved / (double)(1L << 30):0.0} Go", $"⚠ The “{o.User}” account is still signed in: {o.Reserved / (double)(1L << 30):0.0} GB")
                : L.F($"⚠ {big.Count} autres comptes Windows sont restés ouverts : {big.Sum(x => x.Reserved) / (double)(1L << 30):0.0} Go", $"⚠ {big.Count} other Windows accounts are still signed in: {big.Sum(x => x.Reserved) / (double)(1L << 30):0.0} GB");
            OtherSessionsText.Text =
                string.Join("\n", big.Select(x => $"• {x.User}{(x.Active ? "" : L.F(" (en arrière-plan)", " (in background)"))}{L.F(" : ", ": ")}{x.TopApps}"))
                + "\n" + L.F("Ses programmes tournent encore, même si on ne les voit pas, et prennent la mémoire de ton jeu. "
                + "Pour libérer la mémoire, ce compte doit se déconnecter (menu Démarrer › son nom › Se déconnecter), "
                + "au lieu de « Changer d'utilisateur ». Ou : Gestionnaire des tâches › Utilisateurs › clic droit sur le compte › Se déconnecter. "
                + "Attention : ce qui n'est pas sauvegardé dans ce compte sera perdu.",
                "Its programs are still running, even if you can't see them, and take your game's memory. "
                + "To free the memory, that account must sign out (Start menu › its name › Sign out) "
                + "instead of “Switch user”. Or: Task Manager › Users › right-click the account › Sign off. "
                + "Warning: anything unsaved in that account will be lost.");
        }
        var pct = used / total;

        MemText.Text = L.F($"{used:0.0} Go utilisés sur {total:0} Go", $"{used:0.0} GB used of {total:0} GB");
        MemPercent.Text = $"{pct:P0}";
        MemUsedCol.Width = new GridLength(Math.Max(used, 0.01), GridUnitType.Star);
        MemFreeCol.Width = new GridLength(Math.Max(total - used, 0.01), GridUnitType.Star);
        var key = pct >= .85 ? "Bad" : pct >= .70 ? "Gold" : "Ok";
        MemPercent.Foreground = (System.Windows.Media.Brush)FindResource(key);
        MemTip.Text = pct >= .85
            ? L.F("Presque pleine : Windows doit écrire sur le disque pour faire de la place, et les jeux saccadent. Ferme ce qui ne sert pas.", "Almost full: Windows has to write to disk to make room, and games stutter. Close what you don't need.")
            : pct >= .70
                ? L.F("Bien remplie. Avant de jouer, ferme ce qui ne sert pas.", "Quite full. Before playing, close what you don't need.")
                : L.F("Assez de place pour jouer.", "Enough room to play.");

        AppsList.ItemsSource = list;
        var suspects = list.Count(a => a.Suspect);
        AppsSub.Text = L.F($"{list.Count} programmes hors Microsoft", $"{list.Count} non-Microsoft programs")
                       + (suspects > 0 ? L.F($"  ·  {suspects} probablement mal fermé{(suspects > 1 ? "s" : "")} (en jaune)", $"  ·  {suspects} probably not closed properly (in yellow)") : L.F("  ·  aucun programme suspect", "  ·  nothing suspicious"))
                       + L.F("  ·  mis à jour toutes les 5 secondes", "  ·  updated every 5 seconds");
    }

    async void AppsReload_Click(object sender, RoutedEventArgs e) => await LoadApps();

    void OpenTaskManager_Click(object sender, RoutedEventArgs e) => Shell.Open("taskmgr.exe");

    async void CloseApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not OpenApp app) return;
        if (MessageBox.Show(this,
                L.F($"Fermer « {app.Name} » ?\n\nCe qui n'est pas sauvegardé dans ce programme sera perdu.", $"Close “{app.Name}”?\n\nAnything unsaved in this program will be lost."),
                L.F("Fermer le programme", "Close program"), MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        closingApp = true;
        ((Button)sender).IsEnabled = false;
        ((Button)sender).Content = L.F("Fermeture…", "Closing…");
        var before = OpenApps.Memory().used;
        var err = await OpenApps.Close(app);
        closingApp = false;
        await LoadApps();

        var freed = before - OpenApps.Memory().used;
        AppsResult.Visibility = Visibility.Visible;
        AppsResult.Foreground = (System.Windows.Media.Brush)FindResource(err == null ? "Ok" : "Gold");
        AppsResult.Text = err == null
            ? L.F($"✓ « {app.Name} » est fermé.", $"✓ “{app.Name}” is closed.") + (freed > 0.05 ? L.F($" {freed:0.0} Go de mémoire libérés.", $" {freed:0.0} GB of memory freed.") : "")
            : L.F($"« {app.Name} » : {err}", $"“{app.Name}”: {err}");
    }

    async void Period_Checked(object sender, RoutedEventArgs e)
    {
        if (journal != null) await LoadJournal();
    }

    async void JournalReload_Click(object sender, RoutedEventArgs e) => await LoadJournal();

    void ShowNoise_Changed(object sender, RoutedEventArgs e) => ShowEvents();

    // ═════════════════════════════ Fenêtre ═════════════════════════════

    // Une fenêtre sans bordure Windows couvre la barre des tâches une fois agrandie.
    // On dit à Windows d'utiliser la « zone de travail » de l'écran (l'écran moins la barre des tâches).
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
    }

    static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024, MONITOR_DEFAULTTONEAREST = 2;
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;
        var mon = Native.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (mon == IntPtr.Zero || !Native.GetMonitorInfo(mon, ref info)) return IntPtr.Zero;
        var mmi = Marshal.PtrToStructure<Native.MINMAXINFO>(lParam);
        var wa = info.rcWork; var m = info.rcMonitor;
        mmi.ptMaxPosition = new Native.POINT { x = wa.left - m.left, y = wa.top - m.top };
        mmi.ptMaxSize = new Native.POINT { x = wa.right - wa.left, y = wa.bottom - wa.top };
        Marshal.StructureToPtr(mmi, lParam, true);
        return IntPtr.Zero;
    }

    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Max_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
