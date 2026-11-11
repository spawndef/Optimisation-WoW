using System;
using System.Runtime.InteropServices;

namespace OptimisationWoW;

/// <summary>Fonctions de Windows pour connaître la zone de l'écran sans la barre des tâches.</summary>
static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    // Chemin d'un programme, même protégé (anti-triche) : on demande seulement le droit « lecture limitée ».
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);

    public static string? ProcessPath(int pid)
    {
        const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            var size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    // Sessions Windows ouvertes (un autre compte resté connecté par « Changer d'utilisateur »).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WTS_SESSION_INFO { public int SessionId; public string pWinStationName; public int State; }
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode)]
    public static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr info, out int count);
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode)]
    public static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytes);
    [DllImport("wtsapi32.dll")] public static extern void WTSFreeMemory(IntPtr p);

    /// <summary>(id de session, nom du compte, connectée ?) pour chaque session avec un compte.</summary>
    public static System.Collections.Generic.List<(int id, string user, bool active)> Sessions()
    {
        var list = new System.Collections.Generic.List<(int, string, bool)>();
        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var ptr, out var n)) return list;
        try
        {
            var size = Marshal.SizeOf<WTS_SESSION_INFO>();
            for (var i = 0; i < n; i++)
            {
                var s = Marshal.PtrToStructure<WTS_SESSION_INFO>(ptr + i * size);
                if (!WTSQuerySessionInformation(IntPtr.Zero, s.SessionId, 5 /* WTSUserName */, out var buf, out _)) continue;
                var user = Marshal.PtrToStringUni(buf) ?? "";
                WTSFreeMemory(buf);
                if (user.Length > 0) list.Add((s.SessionId, user, s.State == 0 /* WTSActive */));
            }
        }
        finally { WTSFreeMemory(ptr); }
        return list;
    }

    // Mémoire totale et libre du PC.
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
}
