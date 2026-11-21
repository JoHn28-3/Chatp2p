using System.Windows;
using System.Windows.Media;

namespace Chatp2pWPF.Services;

public static class ThemeService
{
    public static void Appliquer(ResourceDictionary r, bool sombre)
    {
        static Brush B(string c) => (Brush)new BrushConverter().ConvertFromString(c)!;
        r["FondApp"] = B(sombre ? "#0B141A" : "#EFEAE2");
        r["FondRail"] = B(sombre ? "#202C33" : "#F0F2F5");
        r["FondPanneau"] = B(sombre ? "#111B21" : "#FFFFFF");
        r["FondEntete"] = B(sombre ? "#202C33" : "#F0F2F5");
        r["FondSaisie"] = B(sombre ? "#2A3942" : "#FFFFFF");
        r["FondHover"] = B(sombre ? "#2A3942" : "#F5F6F6");
        r["FondSelect"] = B(sombre ? "#2A3942" : "#F0F2F5");
        r["Separateur"] = B(sombre ? "#2A3942" : "#E9EDEF");
        r["BulleRecue"] = B(sombre ? "#202C33" : "#FFFFFF");
        r["BulleEnvoyee"] = B(sombre ? "#005C4B" : "#D9FDD3");
        r["Texte"] = B(sombre ? "#E9EDEF" : "#111B21");
        r["Gris"] = B(sombre ? "#8696A0" : "#667781");
        r["Accent"] = B("#00A884");
        r["AccentHover"] = B("#128C7E");
    }
}
