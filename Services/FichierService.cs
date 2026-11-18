using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public static class FichierService
{
    public const long TailleMax = 20 * 1024 * 1024;

    static string DossierRecus => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ChatP2P", "Recus");

    public static async Task<string> EncoderAsync(string chemin) =>
        Convert.ToBase64String(await File.ReadAllBytesAsync(chemin));

    public static void Recevoir(ChatMessage m, Paquet pk)
    {
        try
        {
            Directory.CreateDirectory(DossierRecus);
            var prefixe = pk.Id[..Math.Min(6, pk.Id.Length)];
            var chemin = Path.Combine(DossierRecus, prefixe + "_" + Path.GetFileName(pk.NomFichier));
            File.WriteAllBytes(chemin, Convert.FromBase64String(pk.Data));
            m.Fichier = chemin;
            m.NomFichier = pk.NomFichier;
            ChargerImage(m);
        }
        catch { m.Texte = "(fichier illisible)"; }
    }

    public static void ChargerImage(ChatMessage m)
    {
        if (m.Fichier == null) return;
        var ext = Path.GetExtension(m.Fichier).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif")
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(m.Fichier);
            bmp.EndInit();
            bmp.Freeze();
            m.Image = bmp;
        }
    }

    public static void Ouvrir(string chemin)
    {
        try { Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true }); } catch { }
    }
}