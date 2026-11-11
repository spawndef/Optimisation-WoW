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

    static readonly CultureInfo Fr = new("fr-CA");
    public string CountText => "×" + Count;
    public string LastText => (Count > 1 ? "dernier : " : "") + Last.ToString("d MMM HH:mm", Fr);
    public string GraviteText => Gravite switch
    {
        Gravite.Grave => "Grave",
        Gravite.Attention => "À surveiller",
        Gravite.Faible => "Faible",
        _ => "Sans gravité",
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

    // Ce qu'on sait expliquer en français. Provider = début du nom de la source (sans « Microsoft-Windows- »).
    static readonly Rule[] Rules =
    {
        new("Kernel-Power", new[] { 41 }, Cat.Redemarrage, Gravite.Grave, "Redémarrage imprévu",
            "Le PC s'est éteint sans passer par « Arrêter » : coupure de courant, bouton maintenu ou plantage."),
        new("EventLog", new[] { 6008 }, Cat.Redemarrage, Gravite.Faible, "Arrêt inattendu noté par Windows",
            "Va avec « Redémarrage imprévu » : Windows constate au démarrage qu'il avait été coupé."),
        new("BugCheck", null, Cat.EcranBleu, Gravite.Grave, "Écran bleu (BSOD)",
            "Windows a planté complètement. Le code d'erreur est dans le détail."),
        new("WER-SystemErrorReporting", null, Cat.EcranBleu, Gravite.Grave, "Écran bleu (BSOD)",
            "Windows a planté complètement. Le code d'erreur est dans le détail."),
        new("nvlddmkm", null, Cat.Graphique, Gravite.Attention, "Le pilote Nvidia a planté",
            "La carte graphique a cessé de répondre puis a été relancée. En jeu : gel, écran noir ou crash."),
        new("Display", new[] { 4101 }, Cat.Graphique, Gravite.Attention, "Le pilote graphique a cessé de répondre",
            "Windows a dû relancer le pilote de la carte graphique."),
        new("amdkmdag", null, Cat.Graphique, Gravite.Attention, "Le pilote AMD a planté", "La carte graphique a été relancée."),
        new("igfx", null, Cat.Graphique, Gravite.Faible, "Pilote Intel graphique", "Message du pilote graphique Intel (écran du processeur)."),
        new("WHEA-Logger", new[] { 18, 20, 46 }, Cat.Materiel, Gravite.Grave, "Erreur matérielle fatale",
            "Le processeur, la RAM ou la carte mère a signalé une erreur qui a fait planter le PC."),
        new("WHEA-Logger", null, Cat.Materiel, Gravite.Attention, "Erreur matérielle corrigée",
            "Le matériel a fait une erreur et l'a corrigée tout seul. Rare = normal, souvent = instabilité (RAM, XMP, processeur)."),
        new("Kernel-Processor-Power", new[] { 37 }, Cat.Materiel, Gravite.Attention, "Processeur ralenti par le firmware",
            "Le processeur a été bridé, souvent à cause de la chaleur ou de l'alimentation."),
        new("disk", new[] { 7, 11, 51 }, Cat.Disque, Gravite.Grave, "Erreur de disque",
            "Un disque a eu du mal à lire ou écrire. Vérifie sa santé (CrystalDiskInfo)."),
        new("disk", new[] { 153, 154 }, Cat.Disque, Gravite.Attention, "Disque lent à répondre",
            "Une lecture a dû être recommencée. Si ça revient souvent, le disque ou le câble fatigue."),
        new("Ntfs", null, Cat.Disque, Gravite.Attention, "Problème de système de fichiers",
            "Windows a trouvé une incohérence sur un disque. Un « chkdsk » peut la réparer."),
        new("stornvme", null, Cat.Disque, Gravite.Attention, "Erreur du SSD NVMe", "Le SSD NVMe a signalé un problème."),
        new("volmgr", new[] { 161 }, Cat.Disque, Gravite.Faible, "Fichier de plantage non créé",
            "Windows n'a pas pu enregistrer le détail d'un plantage (souvent lié à une coupure)."),
        new("Application Error", new[] { 1000 }, Cat.Applis, Gravite.Attention, "Appli plantée : {0}",
            "Ce programme s'est fermé tout seul en erreur.", true),
        new("Application Hang", new[] { 1002 }, Cat.Applis, Gravite.Faible, "Appli figée : {0}",
            "Ce programme ne répondait plus et a été fermé.", true),
        new("Service Control Manager", new[] { 7031, 7034 }, Cat.Services, Gravite.Faible, "Service arrêté brutalement : {0}",
            "Un service Windows s'est arrêté tout seul (souvent relancé automatiquement).", true),
        new("Service Control Manager", new[] { 7000, 7009, 7011, 7023, 7024, 7043 }, Cat.Services, Gravite.Faible, "Service en erreur : {0}",
            "Un service n'a pas démarré ou a mis trop de temps. Souvent sans conséquence.", true),
        new("WindowsUpdateClient", new[] { 20 }, Cat.MisesAJour, Gravite.Attention, "Mise à jour Windows échouée",
            "Une mise à jour ne s'est pas installée. Windows réessaiera."),
        new("Tcpip", new[] { 4199 }, Cat.Reseau, Gravite.Attention, "Conflit d'adresse IP",
            "Un autre appareil du réseau a la même adresse que ton PC."),
        new("Kernel-PnP", new[] { 219 }, Cat.Autre, Gravite.Faible, "Pilote non chargé au démarrage",
            "Un pilote d'appareil n'a pas démarré. Souvent un vieil appareil débranché."),
        new("Kernel-Boot", new[] { 29 }, Cat.Autre, Gravite.Faible, "Démarrage rapide raté",
            "Windows a fait un démarrage complet au lieu du démarrage rapide, souvent après une coupure."),
        new("VSS", null, Cat.Autre, Gravite.Faible, "Points de restauration", "Problème avec les copies de sauvegarde de Windows (points de restauration)."),
        new("Volsnap", null, Cat.Autre, Gravite.Faible, "Points de restauration effacés",
            "L'espace réservé aux points de restauration était plein : les plus vieux ont été effacés."),
        new("MsiInstaller", null, Cat.Autre, Gravite.Faible, "Installation de programme ratée", "Un installateur a échoué ou a été annulé."),

        // Bruit connu : Windows écrit ça sur tous les PC, il n'y a rien à faire.
        new("DistributedCOM", null, Cat.Autre, Gravite.SansGravite, "Permissions internes de Windows (DCOM)",
            "Message que Windows écrit sur tous les PC. Rien à faire."),
        new("DNS-Client", null, Cat.Reseau, Gravite.SansGravite, "Réponse Internet en retard (DNS)",
            "Une adresse web a mis du temps à répondre. Normal de temps en temps."),
        new("Time-Service", null, Cat.Autre, Gravite.SansGravite, "Synchro de l'heure", "L'horloge n'a pas pu se synchroniser une fois."),
        new("Windows Error Reporting", null, Cat.Applis, Gravite.SansGravite, "Rapport d'erreur envoyé",
            "Windows a préparé un rapport pour Microsoft après un plantage déjà listé."),
        new("Security-SPP", null, Cat.Autre, Gravite.SansGravite, "Licence Windows", "Vérification de licence, sans gravité."),
        new("Perflib", null, Cat.Autre, Gravite.SansGravite, "Compteurs de performance", "Sans gravité."),
        new("User Device Registration", null, Cat.Autre, Gravite.SansGravite, "Enregistrement de l'appareil", "Sans gravité."),
        new("Kernel-General", null, Cat.Autre, Gravite.SansGravite, "Message du noyau", "Sans gravité."),
        new("Schannel", null, Cat.Reseau, Gravite.SansGravite, "Connexion sécurisée refusée",
            "Un site ou un service avait un certificat qui ne correspondait pas. Normal de temps en temps."),
        new("DeviceAssociationService", null, Cat.Autre, Gravite.SansGravite, "Appairage d'appareil raté",
            "Un appareil (Bluetooth, TV, téléphone) n'a pas pu s'associer une fois."),
        new("WMI", null, Cat.Autre, Gravite.SansGravite, "Message WMI", "Sans gravité."),
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
                                Explain = rule?.Explain ?? (rec.Level == 3 ? "Avertissement de Windows." : "Erreur signalée par Windows."),
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
            catch (Exception e) { res.Error = $"Impossible de lire le journal {log} : {e.Message}"; }
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

        var period = hours <= 24 ? "Dans les dernières 24 h" : $"Sur les {hours / 24} derniers jours";
        var parts = new List<string>();
        string s(int n) => n > 1 ? "s" : "";

        if (r.Restarts > 0) parts.Add($"{r.Restarts} redémarrage{s(r.Restarts)} imprévu{s(r.Restarts)} (coupure de courant ou plantage)");
        if (r.Bsod > 0) parts.Add($"{r.Bsod} écran{s(r.Bsod)} bleu{s(r.Bsod)}");
        if (r.Gpu > 0) parts.Add($"le pilote graphique a planté {r.Gpu} fois");
        if (r.Hardware > 0) parts.Add($"{r.Hardware} erreur{s(r.Hardware)} matérielle{s(r.Hardware)} (processeur, RAM ou carte mère)");
        if (r.Disk > 0) parts.Add($"{r.Disk} problème{s(r.Disk)} de disque");
        if (r.AppCrashes > 0)
        {
            var top = r.Groups.Where(g => g.Cat == Cat.Applis && g.Gravite >= Gravite.Attention).OrderByDescending(g => g.Count).FirstOrDefault();
            var name = top?.Label.Split(':').LastOrDefault()?.Trim();
            parts.Add($"{r.AppCrashes} appli{s(r.AppCrashes)} plantée{s(r.AppCrashes)}" + (name != null ? $" (surtout {name})" : ""));
        }

        var updates = Sum(r, Cat.MisesAJour);
        if (updates > 0) parts.Add($"{updates} échec{s(updates)} de mise à jour Windows");

        var sb = new StringBuilder();
        if (parts.Count == 0) sb.Append($"{period} : rien de grave. ");
        else sb.Append($"{period} : ").Append(string.Join(", ", parts)).Append(". ");

        var grave = r.Bsod > 0 || r.Hardware > 2 || r.Disk > 0 || r.Gpu > 2;
        var watch = r.Restarts > 0 || r.Gpu > 0 || r.Hardware > 0 || r.AppCrashes > 3;
        r.Level = grave ? Gravite.Grave : watch ? Gravite.Attention : Gravite.SansGravite;
        r.Verdict = grave ? "Problème à régler" : watch ? "À surveiller" : "Tout va bien";

        // Le conseil le plus utile en premier.
        if (r.Gpu > 0 && r.Hardware > 0) sb.Append("Plantages graphiques + erreurs matérielles ensemble : pense à la RAM (XMP) ou au processeur avant le pilote. ");
        else if (r.Gpu > 0) sb.Append("Pour le pilote graphique : installation propre du pilote, puis vérifie le câble d'alimentation de la carte. ");
        else if (r.Hardware > 0) sb.Append("Erreurs matérielles : si tu as activé le XMP, essaie une vitesse de RAM un peu plus basse. ");
        if (r.Restarts > 0 && r.Bsod == 0 && r.Hardware == 0) sb.Append("Sans écran bleu ni erreur matérielle, les redémarrages viennent sûrement du courant. ");
        if (r.Disk > 0) sb.Append("Vérifie la santé de tes disques. ");
        if (updates > 20) sb.Append("La même mise à jour échoue en boucle : lance « Résolution des problèmes › Windows Update » dans Paramètres. ");

        var noise = r.Groups.Where(g => g.IsNoise).Sum(g => g.Count);
        if (noise > 0) sb.Append($"{noise} message{s(noise)} sans gravité caché{s(noise)}.");
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
