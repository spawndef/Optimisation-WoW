using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimisationWoW.Core;

/// <summary>Une version de WoW installée (Retail, Classic, Forever…), c.-à-d. un dossier « _xxx_ ».</summary>
public class WowFlavor
{
    public string Folder { get; init; } = "";   // ex. E:\World of Warcraft\_retail_
    public string Exe { get; init; } = "";
    public string Name { get; init; } = "";
    public string ConfigPath => Path.Combine(Folder, "WTF", "Config.wtf");
    public string CacheDir => Path.Combine(Folder, "Cache");
    public int AddOns => Directory.Exists(Path.Combine(Folder, "Interface", "AddOns"))
        ? Directory.GetDirectories(Path.Combine(Folder, "Interface", "AddOns")).Length : 0;

    /// <summary>WoW est-il ouvert ? (il réécrit Config.wtf en quittant, donc on ne le modifie que WoW fermé)</summary>
    public bool Running => Process.GetProcesses().Any(p =>
    {
        using (p)
        {
            if (!p.ProcessName.StartsWith("Wow", StringComparison.OrdinalIgnoreCase)) return false;
            var path = Native.ProcessPath(p.Id);
            return path != null && path.StartsWith(Folder, StringComparison.OrdinalIgnoreCase);
        }
    });

    public long CacheSize()
    {
        try
        {
            return Directory.Exists(CacheDir)
                ? new DirectoryInfo(CacheDir).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Sum(f => f.Length)
                : 0;
        }
        catch { return 0; }
    }

    /// <summary>Vide le dossier Cache (WoW le refait tout seul). Corrige souvent les bugs d'affichage et de chargement.</summary>
    public string? ClearCache()
    {
        if (Running) return L.F("Ferme WoW avant de vider son cache.", "Close WoW before clearing its cache.");
        try
        {
            if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true);
            return null;
        }
        catch (Exception e) { return L.F("Échec : ", "Failed: ") + e.Message; }
    }
}

public static class Wow
{
    static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_retail_"] = "Retail", ["_classic_"] = "Classic", ["_classic_era_"] = "Classic Era",
        ["_anniversary_"] = "Classic Anniversary", ["_forever_"] = "Forever", ["_ptr_"] = "PTR",
        ["_xptr_"] = "PTR", ["_beta_"] = "Bêta", ["_classic_ptr_"] = "Classic PTR",
    };

    /// <summary>Cherche WoW sur tous les disques, aux endroits habituels.</summary>
    public static List<WowFlavor> Find()
    {
        var roots = new List<string>();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            roots.Add(Path.Combine(d.RootDirectory.FullName, "World of Warcraft"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files (x86)", "World of Warcraft"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files", "World of Warcraft"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Games", "World of Warcraft"));
            roots.Add(Path.Combine(d.RootDirectory.FullName, "Jeux", "World of Warcraft"));
        }
        var custom = Settings.Get("wowRoot");
        if (custom != null) roots.Insert(0, custom);

        var list = new List<WowFlavor>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
            list.AddRange(FlavorsIn(root));
        return list;
    }

    public static List<WowFlavor> FlavorsIn(string root)
    {
        var list = new List<WowFlavor>();
        try
        {
            foreach (var dir in Directory.GetDirectories(root, "_*_"))
            {
                var exe = Directory.GetFiles(dir, "Wow*.exe").OrderBy(f => f.Length).FirstOrDefault();
                if (exe == null) continue;
                var key = Path.GetFileName(dir);
                list.Add(new WowFlavor { Folder = dir, Exe = exe, Name = Names.TryGetValue(key, out var n) ? n : key.Trim('_') });
            }
        }
        catch { }
        return list;
    }

    /// <summary>Les réglages proposés pour une version de WoW.</summary>
    public static List<Item> Tweaks(WowFlavor f, int hz) => new()
    {
        new WtfTweak
        {
            Flavor = f, Title = L.F($"Limite de FPS = ton écran ({hz} Hz)", $"FPS cap = your screen ({hz} Hz)"),
            Desc = L.F("Sans limite, la carte graphique tourne à 100 % pour des images que ton écran n'affiche même pas : plus de chaleur, de bruit et de saccades.", "Without a cap, the graphics card runs at 100% for frames your screen can't even show: more heat, noise and stutter."),
            Values = { ("useMaxFPS", "1", "0"), ("maxFPS", hz.ToString(), null) },
        },
        new WtfTweak
        {
            Flavor = f, Title = L.F("30 FPS quand WoW est en arrière-plan", "30 FPS when WoW is in the background"),
            Desc = L.F("Quand tu passes sur Discord ou le navigateur, WoW ralentit et laisse la place au reste du PC.", "When you switch to Discord or the browser, WoW slows down and leaves room for the rest of the PC."),
            Values = { ("useMaxFPSBk", "1", "0"), ("maxFPSBk", "30", null) },
        },
        new WtfTweak
        {
            Flavor = f, Title = "DirectX 12",
            Desc = L.F("Le moteur graphique le plus récent de WoW : il utilise mieux les processeurs à plusieurs cœurs.", "WoW's newest graphics engine: it makes better use of multi-core CPUs."),
            Values = { ("GxApi", "D3D12", null) },
        },
        new GpuPrefTweak
        {
            Flavor = f, Title = L.F("Carte graphique puissante pour WoW", "High-performance GPU for WoW"),
            Desc = L.F("Dit à Windows d'utiliser ta vraie carte graphique pour WoW, jamais l'écran intégré au processeur.", "Tells Windows to use your real graphics card for WoW, never the CPU's built-in graphics."),
        },
        new WtfTweak
        {
            Flavor = f, Reco = Reco.AuChoix, Title = L.F("Échelle de rendu 80 %", "Render scale 80%"),
            Desc = L.F("Le jeu calcule l'image un peu plus petite puis l'agrandit. Gros gain de FPS en 1440p et 4K, image un peu plus douce.", "The game renders a slightly smaller image, then upscales it. Big FPS gain at 1440p and 4K, slightly softer image."),
            Values = { ("renderScale", "0.8", null) },
        },
        new WtfTweak
        {
            Flavor = f, Reco = Reco.AuChoix, Title = L.F("VSync désactivée", "VSync off"),
            Desc = L.F("Avec la limite de FPS, la VSync ajoute surtout du délai. Remets-la si tu vois l'image se « déchirer ».", "With the FPS cap, VSync mostly adds input lag. Turn it back on if you see screen tearing."),
            Values = { ("vsync", "0", "1") },
        },
    };

    /// <summary>Fréquence de l'écran principal (Hz), 60 si on ne la trouve pas.</summary>
    public static async Task<int> RefreshRate()
    {
        var (_, o) = await Shell.PowerShellAsync(
            "(Get-CimInstance Win32_VideoController | Where-Object CurrentRefreshRate -gt 0 | Sort-Object CurrentRefreshRate -Descending | Select-Object -First 1).CurrentRefreshRate");
        return int.TryParse(Shell.FirstLine(o), out var hz) && hz >= 30 && hz <= 500 ? hz : 60;
    }
}

/// <summary>Un réglage du fichier Config.wtf de WoW (lignes « SET clé "valeur" »).</summary>
public class WtfTweak : Item
{
    public WowFlavor Flavor { get; init; } = null!;
    /// <summary>(clé, valeur optimisée, valeur par défaut ; null = enlever la ligne et laisser WoW décider)</summary>
    public List<(string key, string on, string? off)> Values { get; init; } = new();
    public override string LabelOn => L.F("Activé", "Done");
    public override string LabelOff => L.F("Pas fait", "Not done");

    static readonly HashSet<string> Backed = new(StringComparer.OrdinalIgnoreCase);

    public override void Refresh()
    {
        var cfg = Read(Flavor.ConfigPath);
        Current = Values.All(v => cfg.TryGetValue(v.key, out var x) && x == v.on);
    }

    public override Task<string?> ApplyAsync()
    {
        if (Flavor.Running) return Task.FromResult<string?>(L.F("Ferme WoW d'abord : il réécrit ses réglages en quittant.", "Close WoW first: it rewrites its settings when it exits."));
        var path = Flavor.ConfigPath;
        try
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            // Une copie du fichier d'origine, une fois, avant le premier changement.
            if (File.Exists(path) && Backed.Add(path))
                File.Copy(path, path + $".avant-optimisation-{DateTime.Now:yyyy-MM-dd_HH'h'mm}.bak", true);

            foreach (var (key, on, off) in Values)
            {
                var idx = lines.FindIndex(l => KeyOf(l)?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
                var value = Desired ? on : off;
                if (value == null) { if (idx >= 0) lines.RemoveAt(idx); continue; }
                var line = $"SET {key} \"{value}\"";
                if (idx >= 0) lines[idx] = line; else lines.Add(line);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            return Task.FromResult<string?>(null);
        }
        catch (Exception e) { return Task.FromResult<string?>(L.F("Échec : ", "Failed: ") + e.Message); }
    }

    static string? KeyOf(string line)
    {
        var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0].Equals("SET", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
    }

    public static Dictionary<string, string> Read(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return d;
        foreach (var l in File.ReadAllLines(path))
        {
            var parts = l.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && parts[0].Equals("SET", StringComparison.OrdinalIgnoreCase))
                d[parts[1]] = parts[2].Trim().Trim('"');
        }
        return d;
    }
}

/// <summary>Préférence de carte graphique de Windows pour l'exe de WoW (Paramètres › Écran › Graphiques).</summary>
public class GpuPrefTweak : Item
{
    public WowFlavor Flavor { get; init; } = null!;
    const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";
    public override string LabelOn => L.F("Activé", "Done");
    public override string LabelOff => L.F("Pas fait", "Not done");

    public override void Refresh()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        Current = (k?.GetValue(Flavor.Exe) as string)?.Contains("GpuPreference=2") == true;
    }

    public override Task<string?> ApplyAsync()
    {
        try
        {
            Backup.Save(new[] { Key });
            using var k = Registry.CurrentUser.CreateSubKey(Key, true);
            if (Desired) k.SetValue(Flavor.Exe, "GpuPreference=2;", RegistryValueKind.String);
            else k.DeleteValue(Flavor.Exe, false);
            return Task.FromResult<string?>(null);
        }
        catch (Exception e) { return Task.FromResult<string?>(L.F("Échec : ", "Failed: ") + e.Message); }
    }
}

/// <summary>Petits réglages de l'app (dossier de WoW choisi à la main).</summary>
public static class Settings
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimisation WoW");

    public static string? Get(string name)
    {
        try { var f = Path.Combine(Dir, name + ".txt"); return File.Exists(f) ? File.ReadAllText(f).Trim() : null; }
        catch { return null; }
    }

    public static void Set(string name, string value)
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, name + ".txt"), value); } catch { }
    }
}
