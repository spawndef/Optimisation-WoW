using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OptimisationWoW.Core;

/// <summary>Un programme ouvert (tous ses processus regroupés), hors Microsoft.</summary>
public class OpenApp
{
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Path { get; set; }
    public List<int> Pids { get; } = new();
    public long Ram { get; set; }        // en RAM en ce moment (comme le Gestionnaire des tâches)
    public long Reserved { get; set; }   // mémoire réservée par le programme (peut déborder sur le disque)
    public bool HasWindow { get; set; }
    public string? KeepReason { get; set; }

    const long Go = 1L << 30;
    public bool CanClose => KeepReason == null;
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";
    /// <summary>Gros programme sans fenêtre : souvent un jeu ou un launcher mal fermé.</summary>
    public bool Suspect => CanClose && !HasWindow && Reserved >= 3 * Go / 2;
    public string Badge => KeepReason != null ? "À garder" : Suspect ? "⚠ Probablement mal fermé" : HasWindow ? "Fenêtre ouverte" : "En arrière-plan";
    public string BadgeKind => KeepReason != null ? "keep" : Suspect ? "suspect" : "normal";
    public string RamText => Size(Ram);
    public string ReservedText => Size(Reserved) + " réservés";
    public string Details =>
        (string.IsNullOrWhiteSpace(Company) ? "Éditeur inconnu" : Company)
        + $"  ·  {Pids.Count} processus"
        + (KeepReason != null ? "  ·  " + KeepReason : "");

    static string Size(long b) => b >= Go ? $"{b / (double)Go:0.0} Go" : $"{b / (double)(1 << 20):0} Mo";
}

public static class OpenApps
{
    static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    static readonly int Me = Environment.ProcessId;
    static readonly int MySession = Process.GetCurrentProcess().SessionId;

    // Programmes non-Microsoft qu'il ne faut pas fermer d'ici.
    static readonly (string match, string reason)[] Keep =
    {
        ("claude", "c'est Claude, avec qui tu parles"),
        ("Optimisation-WoW", "c'est cette app"),
        ("vgtray", "anti-triche Riot (Vanguard)"),
        ("vgc", "anti-triche Riot (Vanguard)"),
        ("EasyAntiCheat", "anti-triche"),
        ("BEService", "anti-triche"),
        ("RtkAud", "son Realtek"),
        ("nvcontainer", "pilote NVIDIA"),
        ("NVDisplay", "pilote NVIDIA"),
        ("nvsphelper", "pilote NVIDIA"),
    };

    /// <summary>Liste les programmes de ta session, hors Microsoft, du plus gourmand au moins gourmand.</summary>
    public static List<OpenApp> Scan(long minReserved = 80L << 20)
    {
        var groups = new Dictionary<string, OpenApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.SessionId != MySession || p.Id == 0) continue;
                    var path = Native.ProcessPath(p.Id);
                    if (path != null && path.StartsWith(WinDir, StringComparison.OrdinalIgnoreCase)) continue; // Windows lui-même

                    string company = "", name = p.ProcessName;
                    if (path != null)
                    {
                        try
                        {
                            var vi = FileVersionInfo.GetVersionInfo(path);
                            company = (vi.CompanyName ?? "").Trim();
                            if (!string.IsNullOrWhiteSpace(vi.FileDescription)) name = vi.FileDescription.Trim();
                            else if (!string.IsNullOrWhiteSpace(vi.ProductName)) name = vi.ProductName.Trim();
                        }
                        catch { }
                    }
                    if (company.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path == null && company == "") continue; // protégé et inconnu : sûrement un processus système

                    // Regrouper par programme : un launcher et ses « helpers » du même dossier comptent ensemble.
                    var key = path != null ? System.IO.Path.GetDirectoryName(path) + "|" + company : p.ProcessName;
                    if (!groups.TryGetValue(key, out var app))
                    {
                        app = new OpenApp { Name = name, Company = company, Path = path };
                        groups[key] = app;
                    }
                    // Le nom du processus principal (celui qui a une fenêtre, ou le plus gros) donne le nom du groupe.
                    var hasWin = p.MainWindowHandle != IntPtr.Zero;
                    if (hasWin && !app.HasWindow) app.Name = name;
                    app.HasWindow |= hasWin;
                    app.Pids.Add(p.Id);
                    app.Ram += p.WorkingSet64;
                    app.Reserved += p.PrivateMemorySize64;

                    var raw = p.ProcessName;
                    if (p.Id == Me) app.KeepReason = "c'est cette app";
                    var k = Keep.FirstOrDefault(x => raw.StartsWith(x.match, StringComparison.OrdinalIgnoreCase));
                    if (k.reason != null) app.KeepReason = k.reason;
                    if (company.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) app.KeepReason ??= "pilote NVIDIA";
                    if (company.Contains("Realtek", StringComparison.OrdinalIgnoreCase)) app.KeepReason ??= "son Realtek";
                    if (company.Contains("Anthropic", StringComparison.OrdinalIgnoreCase)) app.KeepReason ??= "c'est Claude, avec qui tu parles";
                }
                catch { /* processus fermé entre-temps */ }
            }
        }
        return groups.Values
            .Where(a => a.Reserved >= minReserved || a.Ram >= minReserved)
            .OrderByDescending(a => a.Suspect)
            .ThenByDescending(a => Math.Max(a.Reserved, a.Ram))
            .ToList();
    }

    /// <summary>Ferme proprement (comme la croix), puis force ce qui reste. Renvoie null si tout est fermé.</summary>
    public static async Task<string?> Close(OpenApp app)
    {
        foreach (var pid in app.Pids)
        {
            try { using var p = Process.GetProcessById(pid); if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); } catch { }
        }
        await Task.Delay(3000);

        var refused = 0;
        foreach (var pid in app.Pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (!p.HasExited) p.Kill(entireProcessTree: true);
            }
            catch (ArgumentException) { /* déjà fermé */ }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { refused++; }
        }
        await Task.Delay(800);

        var alive = app.Pids.Count(pid => { try { using var p = Process.GetProcessById(pid); return !p.HasExited; } catch { return false; } });
        if (alive == 0) return null;
        return refused > 0
            ? "Windows refuse de le fermer d'ici (programme protégé, souvent par un anti-triche). Ferme-le avec le Gestionnaire des tâches."
            : $"{alive} processus ne se sont pas encore fermés. Réessaie dans quelques secondes.";
    }

    /// <summary>Un autre compte Windows resté ouvert sur ce PC, et ce qu'il garde en mémoire.</summary>
    public record OtherSession(string User, bool Active, long Reserved, string TopApps);

    public static List<OtherSession> OtherSessions()
    {
        var result = new List<OtherSession>();
        foreach (var (id, user, active) in Native.Sessions())
        {
            if (id == MySession) continue;
            long total = 0;
            var byName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.SessionId != id) continue;
                        total += p.PrivateMemorySize64;
                        byName[p.ProcessName] = byName.GetValueOrDefault(p.ProcessName) + p.PrivateMemorySize64;
                    }
                    catch { }
                }
            }
            // Les 3 plus gros, avec un nom lisible (« FortniteClient-Win64-Shipping » → « FortniteClient »).
            var top = byName.Where(kv => kv.Value >= 200L << 20)
                .OrderByDescending(kv => kv.Value).Take(3)
                .Select(kv => $"{kv.Key.Split('-')[0]} ({kv.Value / (double)(1L << 30):0.0} Go)");
            result.Add(new OtherSession(user, active, total, string.Join(", ", top)));
        }
        return result.OrderByDescending(s => s.Reserved).ToList();
    }

    public static (double used, double total) Memory()
    {
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        Native.GlobalMemoryStatusEx(ref m);
        const double go = 1L << 30;
        return ((m.ullTotalPhys - m.ullAvailPhys) / go, m.ullTotalPhys / go);
    }
}
