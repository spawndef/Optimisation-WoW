using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OptimisationWoW.Core;

public enum Gravite { SansGravite = 0, Faible = 1, Attention = 2, Grave = 3 }

public enum Cat { Redemarrage, EcranBleu, Graphique, Materiel, Disque, Applis, Services, Reseau, MisesAJour, Autre }

/// <summary>Plusieurs événements identiques regroupés en une ligne.</summary>
public class EventGroup
{
    public Gravite Gravite { get; set; }
    public Cat Cat { get; set; }
    public string Label { get; set; } = "";
    public string Explain { get; set; } = "";
    public string Source { get; set; } = "";
    public string Sample { get; set; } = "";
    public int Count { get; set; }
    public DateTime Last { get; set; }
    public DateTime First { get; set; }
    public bool IsNoise => Gravite == Gravite.SansGravite;

    public string CountText => "×" + Count;
    public string LastText => (Count > 1 ? L.F("dernier : ", "last: ") : "") + Last.ToString(L.En ? "MMM d HH:mm" : "d MMM HH:mm", L.Culture);
    public string GraviteText => Gravite switch
    {
        Gravite.Grave => L.F("Grave", "Serious"),
        Gravite.Attention => L.F("À surveiller", "Watch"),
        Gravite.Faible => L.F("Faible", "Minor"),
        _ => L.F("Sans gravité", "Harmless"),
    };
}

public class JournalResult
{
    public List<EventGroup> Groups { get; } = new();
    public int Total { get; set; }
    public int Restarts { get; set; }
    public int Bsod { get; set; }
    public int Gpu { get; set; }
    public int Hardware { get; set; }
    public int Disk { get; set; }
    public int AppCrashes { get; set; }
    public string Verdict { get; set; } = "";
    public Gravite Level { get; set; }
    public string Summary { get; set; } = "";
    public string? Error { get; set; }
}

public static class Journal
{
    record Rule(string Provider, int[]? Ids, Cat Cat, Gravite G, string Label, string Explain, bool SubjectFromProp0 = false);

    // Ce qu'on sait expliquer. Provider = début du nom de la source (sans « Microsoft-Windows- »).
    // Une table par langue, faite à la première lecture.
    static Rule[]? rulesFr, rulesEn;
    static Rule[] Rules => L.En ? rulesEn ??= BuildRules() : rulesFr ??= BuildRules();

    static Rule[] BuildRules() => new Rule[]
    {
        new("Kernel-Power", new[] { 41 }, Cat.Redemarrage, Gravite.Grave, L.F("Redémarrage imprévu", "Unexpected restart"),
            L.F("Le PC s'est éteint sans passer par « Arrêter » : coupure de courant, bouton maintenu ou plantage.",
                "The PC turned off without “Shut down”: power cut, held power button or crash.")),
        new("EventLog", new[] { 6008 }, Cat.Redemarrage, Gravite.Faible, L.F("Arrêt inattendu noté par Windows", "Unexpected shutdown noted by Windows"),
            L.F("Va avec « Redémarrage imprévu » : Windows constate au démarrage qu'il avait été coupé.",
                "Goes with “Unexpected restart”: at startup, Windows notices it had been cut off.")),
        new("BugCheck", null, Cat.EcranBleu, Gravite.Grave, L.F("Écran bleu (BSOD)", "Blue screen (BSOD)"),
            L.F("Windows a planté complètement. Le code d'erreur est dans le détail.", "Windows crashed completely. The error code is in the details.")),
        new("WER-SystemErrorReporting", null, Cat.EcranBleu, Gravite.Grave, L.F("Écran bleu (BSOD)", "Blue screen (BSOD)"),
            L.F("Windows a planté complètement. Le code d'erreur est dans le détail.", "Windows crashed completely. The error code is in the details.")),
        new("nvlddmkm", null, Cat.Graphique, Gravite.Attention, L.F("Le pilote Nvidia a planté", "Nvidia driver crashed"),
            L.F("La carte graphique a cessé de répondre puis a été relancée. En jeu : gel, écran noir ou crash.",
                "The graphics card stopped responding and was restarted. In game: freeze, black screen or crash.")),
        new("Display", new[] { 4101 }, Cat.Graphique, Gravite.Attention, L.F("Le pilote graphique a cessé de répondre", "Graphics driver stopped responding"),
            L.F("Windows a dû relancer le pilote de la carte graphique.", "Windows had to restart the graphics driver.")),
        new("amdkmdag", null, Cat.Graphique, Gravite.Attention, L.F("Le pilote AMD a planté", "AMD driver crashed"),
            L.F("La carte graphique a été relancée.", "The graphics card was restarted.")),
        new("igfx", null, Cat.Graphique, Gravite.Faible, L.F("Pilote Intel graphique", "Intel graphics driver"),
            L.F("Message du pilote graphique Intel (écran du processeur).", "Message from the Intel graphics driver (CPU's built-in graphics).")),
        new("WHEA-Logger", new[] { 18, 20, 46 }, Cat.Materiel, Gravite.Grave, L.F("Erreur matérielle fatale", "Fatal hardware error"),
            L.F("Le processeur, la RAM ou la carte mère a signalé une erreur qui a fait planter le PC.",
                "The CPU, RAM or motherboard reported an error that crashed the PC.")),
        new("WHEA-Logger", null, Cat.Materiel, Gravite.Attention, L.F("Erreur matérielle corrigée", "Corrected hardware error"),
            L.F("Le matériel a fait une erreur et l'a corrigée tout seul. Rare = normal, souvent = instabilité (RAM, XMP, processeur).",
                "The hardware made an error and fixed it by itself. Rare = normal, often = instability (RAM, XMP, CPU).")),
        new("Kernel-Processor-Power", new[] { 37 }, Cat.Materiel, Gravite.Attention, L.F("Processeur ralenti par le firmware", "CPU slowed down by firmware"),
            L.F("Le processeur a été bridé, souvent à cause de la chaleur ou de l'alimentation.", "The CPU was throttled, often because of heat or power.")),
        new("disk", new[] { 7, 11, 51 }, Cat.Disque, Gravite.Grave, L.F("Erreur de disque", "Disk error"),
            L.F("Un disque a eu du mal à lire ou écrire. Vérifie sa santé (CrystalDiskInfo).", "A disk had trouble reading or writing. Check its health (CrystalDiskInfo).")),
        new("disk", new[] { 153, 154 }, Cat.Disque, Gravite.Attention, L.F("Disque lent à répondre", "Disk slow to respond"),
            L.F("Une lecture a dû être recommencée. Si ça revient souvent, le disque ou le câble fatigue.",
                "A read had to be retried. If it happens often, the disk or cable is wearing out.")),
        new("Ntfs", null, Cat.Disque, Gravite.Attention, L.F("Problème de système de fichiers", "File system problem"),
            L.F("Windows a trouvé une incohérence sur un disque. Un « chkdsk » peut la réparer.", "Windows found an inconsistency on a disk. “chkdsk” can fix it.")),
        new("stornvme", null, Cat.Disque, Gravite.Attention, L.F("Erreur du SSD NVMe", "NVMe SSD error"),
            L.F("Le SSD NVMe a signalé un problème.", "The NVMe SSD reported a problem.")),
        new("volmgr", new[] { 161 }, Cat.Disque, Gravite.Faible, L.F("Fichier de plantage non créé", "Crash dump not created"),
            L.F("Windows n'a pas pu enregistrer le détail d'un plantage (souvent lié à une coupure).", "Windows could not save crash details (often after a power cut).")),
        new("Application Error", new[] { 1000 }, Cat.Applis, Gravite.Attention, L.F("Appli plantée : {0}", "App crashed: {0}"),
            L.F("Ce programme s'est fermé tout seul en erreur.", "This program closed itself with an error."), true),
        new("Application Hang", new[] { 1002 }, Cat.Applis, Gravite.Faible, L.F("Appli figée : {0}", "App froze: {0}"),
            L.F("Ce programme ne répondait plus et a été fermé.", "This program stopped responding and was closed."), true),
        new("Service Control Manager", new[] { 7031, 7034 }, Cat.Services, Gravite.Faible, L.F("Service arrêté brutalement : {0}", "Service stopped unexpectedly: {0}"),
            L.F("Un service Windows s'est arrêté tout seul (souvent relancé automatiquement).", "A Windows service stopped by itself (often restarted automatically)."), true),
        new("Service Control Manager", new[] { 7000, 7009, 7011, 7023, 7024, 7043 }, Cat.Services, Gravite.Faible, L.F("Service en erreur : {0}", "Service error: {0}"),
            L.F("Un service n'a pas démarré ou a mis trop de temps. Souvent sans conséquence.", "A service did not start or took too long. Usually harmless."), true),
        new("WindowsUpdateClient", new[] { 20 }, Cat.MisesAJour, Gravite.Attention, L.F("Mise à jour Windows échouée", "Windows Update failed"),
            L.F("Une mise à jour ne s'est pas installée. Windows réessaiera.", "An update did not install. Windows will try again.")),
        new("Tcpip", new[] { 4199 }, Cat.Reseau, Gravite.Attention, L.F("Conflit d'adresse IP", "IP address conflict"),
            L.F("Un autre appareil du réseau a la même adresse que ton PC.", "Another device on the network has the same address as your PC.")),
        new("Kernel-PnP", new[] { 219 }, Cat.Autre, Gravite.Faible, L.F("Pilote non chargé au démarrage", "Driver not loaded at startup"),
            L.F("Un pilote d'appareil n'a pas démarré. Souvent un vieil appareil débranché.", "A device driver did not start. Often an old unplugged device.")),
        new("Kernel-Boot", new[] { 29 }, Cat.Autre, Gravite.Faible, L.F("Démarrage rapide raté", "Fast startup failed"),
            L.F("Windows a fait un démarrage complet au lieu du démarrage rapide, souvent après une coupure.", "Windows did a full boot instead of fast startup, often after a power cut.")),
        new("VSS", null, Cat.Autre, Gravite.Faible, L.F("Points de restauration", "Restore points"),
            L.F("Problème avec les copies de sauvegarde de Windows (points de restauration).", "Problem with Windows shadow copies (restore points).")),
        new("Volsnap", null, Cat.Autre, Gravite.Faible, L.F("Points de restauration effacés", "Restore points deleted"),
            L.F("L'espace réservé aux points de restauration était plein : les plus vieux ont été effacés.", "Restore point storage was full: the oldest ones were deleted.")),
        new("MsiInstaller", null, Cat.Autre, Gravite.Faible, L.F("Installation de programme ratée", "Program install failed"),
            L.F("Un installateur a échoué ou a été annulé.", "An installer failed or was cancelled.")),

        // Bruit connu : Windows écrit ça sur tous les PC, il n'y a rien à faire.
        new("DistributedCOM", null, Cat.Autre, Gravite.SansGravite, L.F("Permissions internes de Windows (DCOM)", "Internal Windows permissions (DCOM)"),
            L.F("Message que Windows écrit sur tous les PC. Rien à faire.", "Windows writes this on every PC. Nothing to do.")),
        new("DNS-Client", null, Cat.Reseau, Gravite.SansGravite, L.F("Réponse Internet en retard (DNS)", "Slow Internet answer (DNS)"),
            L.F("Une adresse web a mis du temps à répondre. Normal de temps en temps.", "A web address was slow to answer. Normal now and then.")),
        new("Time-Service", null, Cat.Autre, Gravite.SansGravite, L.F("Synchro de l'heure", "Time sync"),
            L.F("L'horloge n'a pas pu se synchroniser une fois.", "The clock could not sync once.")),
        new("Windows Error Reporting", null, Cat.Applis, Gravite.SansGravite, L.F("Rapport d'erreur envoyé", "Error report sent"),
            L.F("Windows a préparé un rapport pour Microsoft après un plantage déjà listé.", "Windows prepared a report for Microsoft after a crash already listed.")),
        new("Security-SPP", null, Cat.Autre, Gravite.SansGravite, L.F("Licence Windows", "Windows license"), L.F("Vérification de licence, sans gravité.", "License check, harmless.")),
        new("Perflib", null, Cat.Autre, Gravite.SansGravite, L.F("Compteurs de performance", "Performance counters"), L.F("Sans gravité.", "Harmless.")),
        new("User Device Registration", null, Cat.Autre, Gravite.SansGravite, L.F("Enregistrement de l'appareil", "Device registration"), L.F("Sans gravité.", "Harmless.")),
        new("Kernel-General", null, Cat.Autre, Gravite.SansGravite, L.F("Message du noyau", "Kernel message"), L.F("Sans gravité.", "Harmless.")),
        new("Schannel", null, Cat.Reseau, Gravite.SansGravite, L.F("Connexion sécurisée refusée", "Secure connection refused"),
            L.F("Un site ou un service avait un certificat qui ne correspondait pas. Normal de temps en temps.", "A site or service had a certificate that did not match. Normal now and then.")),
        new("DeviceAssociationService", null, Cat.Autre, Gravite.SansGravite, L.F("Appairage d'appareil raté", "Device pairing failed"),
            L.F("Un appareil (Bluetooth, TV, téléphone) n'a pas pu s'associer une fois.", "A device (Bluetooth, TV, phone) could not pair once.")),
        new("WMI", null, Cat.Autre, Gravite.SansGravite, L.F("Message WMI", "WMI message"), L.F("Sans gravité.", "Harmless.")),
    };

    static Rule? Find(string provider, int id)
    {
        var p = provider.Replace("Microsoft-Windows-", "");
        return Rules.FirstOrDefault(r => p.StartsWith(r.Provider, StringComparison.OrdinalIgnoreCase)
                                         && (r.Ids == null || r.Ids.Contains(id)));
    }

    public static JournalResult Read(int hours)
    {
        var res = new JournalResult();
        var groups = new Dictionary<string, EventGroup>();
        var ms = (long)hours * 3600 * 1000;
        var query = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";

        foreach (var log in new[] { "System", "Application" })
        {
            try
            {
                using var reader = new EventLogReader(new EventLogQuery(log, PathType.LogName, query) { ReverseDirection = true });
                var n = 0;
                for (var rec = reader.ReadEvent(); rec != null && n < 20000; rec = reader.ReadEvent(), n++)
                {
                    using (rec)
                    {
                        var provider = rec.ProviderName ?? "?";
                        var id = rec.Id;
                        var when = rec.TimeCreated ?? DateTime.MinValue;
                        var rule = Find(provider, id);
                        string? subject = null;
                        if (rule?.SubjectFromProp0 == true)
                        {
                            try { subject = rec.Properties.Count > 0 ? rec.Properties[0].Value?.ToString() : null; } catch { }
                        }
                        var key = $"{provider}|{(rule?.Ids == null && rule != null ? 0 : id)}|{subject}";
                        if (!groups.TryGetValue(key, out var g))
                        {
                            string sample;
                            try { sample = rec.FormatDescription() ?? ""; } catch { sample = ""; }
                            g = new EventGroup
                            {
                                Cat = rule?.Cat ?? Cat.Autre,
                                // Inconnu : « critique » = à surveiller, le reste = faible (sinon tout le bruit des programmes passe devant).
                                Gravite = rule?.G ?? (rec.Level <= 1 ? Gravite.Attention : Gravite.Faible),
                                Label = rule != null ? string.Format(rule.Label, subject ?? "?") : FirstSentence(sample, provider),
                                Explain = rule?.Explain ?? (rec.Level == 3 ? L.F("Avertissement de Windows.", "Windows warning.") : L.F("Erreur signalée par Windows.", "Error reported by Windows.")),
                                Source = $"{provider.Replace("Microsoft-Windows-", "")} · {id} · {log}",
                                Sample = Clip(sample, 600),
                                Last = when,
                            };
                            // Une erreur inconnue de niveau « avertissement » reste faible ; une inconnue « erreur » reste à surveiller seulement si elle revient.
                            groups[key] = g;
                        }
                        g.Count++;
                        g.First = when;
                        res.Total++;
                    }
                }
            }
            catch (Exception e) { res.Error = L.F($"Impossible de lire le journal {log} : {e.Message}", $"Cannot read the {log} log: {e.Message}"); }
        }

        res.Groups.AddRange(groups.Values.OrderByDescending(g => g.Gravite).ThenByDescending(g => g.Count).ThenByDescending(g => g.Last));
        Summarize(res, hours);
        return res;
    }

    static int Sum(JournalResult r, Cat c, Func<EventGroup, bool>? f = null) =>
        r.Groups.Where(g => g.Cat == c && !g.IsNoise && (f == null || f(g))).Sum(g => g.Count);

    static void Summarize(JournalResult r, int hours)
    {
        r.Restarts = Math.Max(Sum(r, Cat.Redemarrage, g => g.Source.StartsWith("Kernel-Power")),
                              Sum(r, Cat.Redemarrage, g => g.Source.StartsWith("EventLog")));
        r.Bsod = Sum(r, Cat.EcranBleu);
        r.Gpu = Sum(r, Cat.Graphique, g => g.Gravite >= Gravite.Attention);
        r.Hardware = Sum(r, Cat.Materiel);
        r.Disk = Sum(r, Cat.Disque, g => g.Gravite >= Gravite.Attention);
        r.AppCrashes = Sum(r, Cat.Applis, g => g.Gravite >= Gravite.Attention);

        var period = hours <= 24 ? L.F("Dans les dernières 24 h", "In the last 24 h") : L.F($"Sur les {hours / 24} derniers jours", $"Over the last {hours / 24} days");
        var parts = new List<string>();
        string s(int n) => n > 1 ? "s" : "";

        if (r.Restarts > 0) parts.Add(L.F($"{r.Restarts} redémarrage{s(r.Restarts)} imprévu{s(r.Restarts)} (coupure de courant ou plantage)",
                                          $"{r.Restarts} unexpected restart{s(r.Restarts)} (power cut or crash)"));
        if (r.Bsod > 0) parts.Add(L.F($"{r.Bsod} écran{s(r.Bsod)} bleu{s(r.Bsod)}", $"{r.Bsod} blue screen{s(r.Bsod)}"));
        if (r.Gpu > 0) parts.Add(L.F($"le pilote graphique a planté {r.Gpu} fois", $"the graphics driver crashed {r.Gpu} time{s(r.Gpu)}"));
        if (r.Hardware > 0) parts.Add(L.F($"{r.Hardware} erreur{s(r.Hardware)} matérielle{s(r.Hardware)} (processeur, RAM ou carte mère)",
                                           $"{r.Hardware} hardware error{s(r.Hardware)} (CPU, RAM or motherboard)"));
        if (r.Disk > 0) parts.Add(L.F($"{r.Disk} problème{s(r.Disk)} de disque", $"{r.Disk} disk problem{s(r.Disk)}"));
        if (r.AppCrashes > 0)
        {
            var top = r.Groups.Where(g => g.Cat == Cat.Applis && g.Gravite >= Gravite.Attention).OrderByDescending(g => g.Count).FirstOrDefault();
            var name = top?.Label.Split(':').LastOrDefault()?.Trim();
            parts.Add(L.F($"{r.AppCrashes} appli{s(r.AppCrashes)} plantée{s(r.AppCrashes)}", $"{r.AppCrashes} app crash{(r.AppCrashes > 1 ? "es" : "")}")
                      + (name != null ? L.F($" (surtout {name})", $" (mostly {name})") : ""));
        }

        var updates = Sum(r, Cat.MisesAJour);
        if (updates > 0) parts.Add(L.F($"{updates} échec{s(updates)} de mise à jour Windows", $"{updates} failed Windows update{s(updates)}"));

        var sb = new StringBuilder();
        var colon = L.F(" : ", ": ");
        if (parts.Count == 0) sb.Append(period + colon + L.F("rien de grave. ", "nothing serious. "));
        else sb.Append(period + colon).Append(string.Join(", ", parts)).Append(". ");

        var grave = r.Bsod > 0 || r.Hardware > 2 || r.Disk > 0 || r.Gpu > 2;
        var watch = r.Restarts > 0 || r.Gpu > 0 || r.Hardware > 0 || r.AppCrashes > 3;
        r.Level = grave ? Gravite.Grave : watch ? Gravite.Attention : Gravite.SansGravite;
        r.Verdict = grave ? L.F("Problème à régler", "Needs fixing") : watch ? L.F("À surveiller", "Keep an eye on it") : L.F("Tout va bien", "All good");

        // Le conseil le plus utile en premier.
        if (r.Gpu > 0 && r.Hardware > 0) sb.Append(L.F("Plantages graphiques + erreurs matérielles ensemble : pense à la RAM (XMP) ou au processeur avant le pilote. ", "Graphics crashes + hardware errors together: suspect RAM (XMP) or the CPU before the driver. "));
        else if (r.Gpu > 0) sb.Append(L.F("Pour le pilote graphique : installation propre du pilote, puis vérifie le câble d'alimentation de la carte. ", "For the graphics driver: do a clean driver install, then check the card's power cable. "));
        else if (r.Hardware > 0) sb.Append(L.F("Erreurs matérielles : si tu as activé le XMP, essaie une vitesse de RAM un peu plus basse. ", "Hardware errors: if you turned on XMP, try a slightly lower RAM speed. "));
        if (r.Restarts > 0 && r.Bsod == 0 && r.Hardware == 0) sb.Append(L.F("Sans écran bleu ni erreur matérielle, les redémarrages viennent sûrement du courant. ", "With no blue screen or hardware error, the restarts most likely come from power cuts. "));
        if (r.Disk > 0) sb.Append(L.F("Vérifie la santé de tes disques. ", "Check your disks' health. "));
        if (updates > 20) sb.Append(L.F("La même mise à jour échoue en boucle : lance « Résolution des problèmes › Windows Update » dans Paramètres. ", "The same update keeps failing: run “Troubleshoot › Windows Update” in Settings. "));

        var noise = r.Groups.Where(g => g.IsNoise).Sum(g => g.Count);
        if (noise > 0) sb.Append(L.F($"{noise} message{s(noise)} sans gravité caché{s(noise)}.", $"{noise} harmless message{s(noise)} hidden."));
        r.Summary = sb.ToString().Trim();
    }

    static string FirstSentence(string text, string provider)
    {
        var t = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (t.Length == 0) return provider.Replace("Microsoft-Windows-", "");
        var dot = t.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 10 && dot < 110) t = t[..(dot + 1)];
        return Clip(t, 110);
    }

    static string Clip(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }
}
