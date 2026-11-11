using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OptimisationWoW.Core;

/// <summary>Notre conseil pour quelqu'un qui ne sait pas à quoi sert l'option.</summary>
public enum Reco
{
    /// <summary>Sans risque : on le coche pour toi avec « Cocher les recommandations ».</summary>
    Recommande,
    /// <summary>Ça dépend de toi (tu t'en sers peut-être).</summary>
    AuChoix,
    /// <summary>Utile au PC ou aux jeux : on n'y touche pas.</summary>
    AGarder,
}

/// <summary>
/// Une ligne avec une case à cocher.
/// Règle partout dans l'app : COCHÉ = on fait le ménage (désactivé, bloqué ou désinstallé).
/// </summary>
public abstract class Item : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string Desc { get; init; } = "";
    public string Section { get; init; } = "";
    /// <summary>Page des Paramètres à ouvrir quand Windows bloque le réglage.</summary>
    public string? SettingsUri { get; init; }
    public Reco Reco { get; set; } = Reco.Recommande;
    public string RecoText => Reco switch { Reco.Recommande => L.F("★ Recommandé", "★ Recommended"), Reco.AGarder => L.F("À garder", "Keep"), _ => L.F("Au choix", "Your choice") };

    bool _current, _desired, _canChange = true;
    string? _note;

    /// <summary>État réel lu dans Windows (true = déjà optimisé).</summary>
    public bool Current
    {
        get => _current;
        set { _current = value; _desired = value; Notify(nameof(Current), nameof(Desired), nameof(Changed), nameof(Status), nameof(StatusIsGood)); }
    }

    /// <summary>Ce que la case demande.</summary>
    public bool Desired
    {
        get => _desired;
        set { if (_desired == value || !_canChange) return; _desired = value; Notify(nameof(Desired), nameof(Changed)); }
    }

    public bool Changed => _desired != _current;
    public bool CanChange { get => _canChange; set { _canChange = value; Notify(nameof(CanChange)); } }
    public string? Note { get => _note; set { _note = value; Notify(nameof(Note), nameof(HasNote)); } }
    public bool HasNote => !string.IsNullOrEmpty(_note);
    public bool HasSettings => SettingsUri != null;

    public virtual string LabelOn => L.F("Désactivé", "Off");
    public virtual string LabelOff => L.F("Actif", "On");
    public string Status => Current ? LabelOn : LabelOff;
    public bool StatusIsGood => Current;

    /// <summary>Lit l'état réel.</summary>
    public abstract void Refresh();
    /// <summary>Applique la case. Renvoie null si tout va bien, sinon un message en français.</summary>
    public abstract Task<string?> ApplyAsync();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

// ─────────────────────────────── Réglages du registre (compte) ───────────────────────────────

/// <summary>Une valeur DWORD sous HKCU. Off = null veut dire « effacer la valeur » (retour au défaut de Windows).</summary>
public record RegVal(string Path, string Name, int On, int? Off);

public class RegTweak : Item
{
    public List<RegVal> Values { get; init; } = new();

    public override void Refresh()
    {
        Current = Values.All(v =>
        {
            using var k = Registry.CurrentUser.OpenSubKey(v.Path);
            return k?.GetValue(v.Name) is int x && x == v.On;
        });
    }

    public override Task<string?> ApplyAsync()
    {
        Backup.Save(Values.Select(v => v.Path));
        var blocked = 0;
        foreach (var v in Values)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(v.Path, true);
                if (Desired) k.SetValue(v.Name, v.On, RegistryValueKind.DWord);
                else if (v.Off is int off) k.SetValue(v.Name, off, RegistryValueKind.DWord);
                else k.DeleteValue(v.Name, false);
            }
            catch (UnauthorizedAccessException) { blocked++; }
        }
        if (blocked > 0)
            return Task.FromResult<string?>(HasSettings
                ? L.F("Windows bloque ce réglage : fais-le dans Paramètres (bouton à droite).", "Windows blocks this setting: change it in Settings (button on the right).")
                : L.F("Windows a refusé une partie de ce réglage.", "Windows refused part of this setting."));
        return Task.FromResult<string?>(null);
    }
}

// ─────────────────────────────── Applis Microsoft inutiles ───────────────────────────────

public class AppItem : Item
{
    public string Package { get; init; } = "";
    // « Absente » et pas « Retirée » : on ne peut pas savoir si elle a été enlevée ou jamais installée.
    public override string LabelOn => L.F("Absente", "Not installed");
    public override string LabelOff => L.F("Installée", "Installed");

    public override void Refresh() { /* lu en bloc par AppsService.RefreshAll */ }

    public void SetInstalled(bool installed)
    {
        Current = !installed;
        // Une appli déjà retirée ne se réinstalle pas d'ici : c'est le Microsoft Store.
        CanChange = installed;
        Note = installed ? null : L.F("Pas sur ce PC. Pour l'avoir : Microsoft Store.", "Not on this PC. To get it: Microsoft Store.");
    }

    public override async Task<string?> ApplyAsync()
    {
        if (!Desired || Current) return null;
        var (code, output) = await Shell.PowerShellAsync(
            $"$p = Get-AppxPackage -Name '{Package}'; if ($p) {{ $p | Remove-AppxPackage -ErrorAction Stop }}; 'ok'");
        return code == 0 && output.Contains("ok") ? null : L.F("Échec : ", "Failed: ") + Shell.FirstLine(output);
    }
}

// ─────────────────────────────── Programmes au démarrage ───────────────────────────────

public enum StartKind { RunUser, RunMachine, StartupFolder, StoreApp }

public class StartupItem : Item
{
    public StartKind Kind { get; init; }
    public string ValueName { get; init; } = "";
    /// <summary>Pour une appli du Store : la clé qui garde son état.</summary>
    public string? TaskKey { get; init; }
    public override string LabelOn => L.F("Bloqué", "Blocked");
    public override string LabelOff => L.F("Démarre", "Starts");

    const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public override void Refresh()
    {
        switch (Kind)
        {
            case StartKind.StoreApp:
                using (var k = Registry.CurrentUser.OpenSubKey(TaskKey!))
                {
                    var s = k?.GetValue("State") is int x ? x : 0;
                    Current = s != 2 && s != 4; // 2 = activé, 4 = activé par stratégie
                }
                break;
            default:
                var root = Kind == StartKind.RunMachine ? Registry.LocalMachine : Registry.CurrentUser;
                var sub = Kind == StartKind.StartupFolder ? "StartupFolder" : "Run";
                using (var k = root.OpenSubKey(Approved + sub))
                {
                    // Le 1er octet est pair = activé, impair = désactivé (comme le Gestionnaire des tâches).
                    Current = k?.GetValue(ValueName) is byte[] b && b.Length > 0 && (b[0] & 1) == 1;
                }
                break;
        }
        if (Kind == StartKind.RunMachine)
        {
            CanChange = false;
            Note = L.F("Installé pour tout le PC : il faut les droits admin (Gestionnaire des tâches › Démarrage).", "Installed for the whole PC: needs admin rights (Task Manager › Startup).");
        }
    }

    public override Task<string?> ApplyAsync()
    {
        try
        {
            if (Kind == StartKind.StoreApp)
            {
                Backup.Save(new[] { TaskKey! });
                using var k = Registry.CurrentUser.CreateSubKey(TaskKey!, true);
                k.SetValue("State", Desired ? 1 : 2, RegistryValueKind.DWord);
            }
            else if (Kind is StartKind.RunUser or StartKind.StartupFolder)
            {
                var path = Approved + (Kind == StartKind.StartupFolder ? "StartupFolder" : "Run");
                Backup.Save(new[] { path });
                using var k = Registry.CurrentUser.CreateSubKey(path, true);
                var data = new byte[12];
                data[0] = (byte)(Desired ? 3 : 2);
                if (Desired) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
                k.SetValue(ValueName, data, RegistryValueKind.Binary);
            }
            else return Task.FromResult<string?>(L.F("Il faut les droits admin.", "Needs admin rights."));
        }
        catch (Exception e) { return Task.FromResult<string?>(L.F("Échec : ", "Failed: ") + e.Message); }
        return Task.FromResult<string?>(null);
    }
}

// ─────────────────────────────── Sauvegarde automatique ───────────────────────────────

public static class Backup
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimisation WoW", "sauvegardes");

    static readonly HashSet<string> done = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exporte chaque clé une fois par session, avant le premier changement.</summary>
    public static void Save(IEnumerable<string> hkcuPaths)
    {
        var dir = Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd_HH'h'mm"));
        foreach (var p in hkcuPaths.Distinct())
        {
            if (!done.Add(p)) continue;
            try
            {
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, string.Join("_", p.Split('\\').TakeLast(2)) + ".reg");
                Shell.Run("reg.exe", $"export \"HKCU\\{p}\" \"{file}\" /y");
            }
            catch { /* une clé absente n'a rien à sauvegarder */ }
        }
    }
}
