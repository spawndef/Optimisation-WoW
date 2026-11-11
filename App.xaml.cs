using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OptimisationWoW.Core;

namespace OptimisationWoW;

public partial class App : Application
{
    // Langue (textes + format des nombres) et couleurs de la faction, posées avant la première fenêtre.
    protected override void OnStartup(StartupEventArgs e)
    {
        L.Apply(this, L.Load());
        Theme.Apply(this, Theme.Load());
        base.OnStartup(e);
    }
}

/// <summary>Thème Horde ou Alliance, changé tout de suite et gardé en mémoire.</summary>
public static class Theme
{
    public const string Horde = "Horde", Alliance = "Alliance";
    static readonly string File = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimisation WoW", "faction.txt");

    public static string Load()
    {
        try { return System.IO.File.ReadAllText(File).Trim() == Alliance ? Alliance : Horde; }
        catch { return Horde; } // Lok'tar ogar !
    }

    public static void Save(string faction)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(File)!);
            System.IO.File.WriteAllText(File, faction);
        }
        catch { }
    }

    public static void Apply(Application app, string faction)
    {
        var horde = faction != Alliance;
        Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        // Les pinceaux du XAML sont figés : on les REMPLACE (avant que les styles les lisent).
        void Set(string key, string hex) => app.Resources[key] = new SolidColorBrush(C(hex));

        // Horde : rouge sang et fer. Alliance : bleu royal et argent. L'or reste commun.
        Set("Brand", horde ? "#B3121B" : "#1F5FBF");
        Set("Brand2", horde ? "#F06A5A" : "#7FB2FF");
        Set("BrandSoft", horde ? "#2EB3121B" : "#2E1F5FBF");
        Set("Glow", horde ? "#55A0141C" : "#55245FC8");
        Set("Bg", horde ? "#0D0807" : "#070A10");
        Set("Bg2", horde ? "#140C0A" : "#0B1018");
        Set("Panel", horde ? "#1C1210" : "#111827");
        Set("Panel2", horde ? "#261815" : "#182134");
        app.Resources["Grad"] = new LinearGradientBrush(C(horde ? "#8C1616" : "#163C8C"), C(horde ? "#C8961E" : "#C8A64A"), new Point(0, 0), new Point(1, 1));
    }

    /// <summary>Change de faction tout de suite (les couleurs de faction sont des DynamicResource) et s'en souvient.</summary>
    public static void SwitchTo(string faction)
    {
        Save(faction);
        Apply(Application.Current, faction);
    }
}

public class BoolToVis : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var b = value is true;
        if (p as string == "not") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Couleur du point selon la gravité d'un événement.</summary>
public class GraviteBrush : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        Application.Current.FindResource(value switch
        {
            Gravite.Grave => "Bad",
            Gravite.Attention => "Gold",
            Gravite.Faible => "Cyan",
            _ => "Ink3",
        });
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>0 = vert ; plus = doré (ou rouge si le paramètre est « bad »).</summary>
public class CountBrush : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        Application.Current.FindResource(value is int n && n > 0 ? (p as string == "bad" ? "Bad" : "Gold") : "Ok");
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
