using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OptimisationWoW.Core;

public static class Shell
{
    /// <summary>Lance PowerShell sans fenêtre et renvoie (code, sortie).</summary>
    public static async Task<(int code, string output)> PowerShellAsync(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command",
                     "[Console]::OutputEncoding=[Text.Encoding]::UTF8; $ProgressPreference='SilentlyContinue'; " + script })
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, (await outTask) + (await errTask));
    }

    public static void Run(string exe, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false });
        p?.WaitForExit(10000);
    }

    public static void Open(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
    }

    public static string FirstLine(string s)
    {
        s = s.Trim();
        var i = s.IndexOfAny(new[] { '\r', '\n' });
        return i < 0 ? s : s[..i];
    }
}
