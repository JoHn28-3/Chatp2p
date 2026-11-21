using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public static class FichierService
{
    public const long TailleMax = 50L * 1024 * 1024; // 50 Mo

    static string DossierRecus => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ChatP2P", "Recus");

    public static async Task<string> EncoderAsync(string chemin) =>
        Convert.ToBase64String(await File.ReadAllBytesAsync(chemin));

    public static void Recevoir(ChatMessage m, Paquet pk)
    {
        try
        {
            var ext = Path.GetExtension(pk.NomFichier).ToLowerInvariant();
            var type = ClassifierMedia(ext);
            var sousDossier = type switch
            {
                "image" => "Images",
                "audio" => "Audio",
                "video" => "Videos",
                _ => "Documents"
            };
            var dossier = Path.Combine(DossierRecus, sousDossier);
            Directory.CreateDirectory(dossier);
            var prefixe = pk.Id[..Math.Min(6, pk.Id.Length)];
            var nomPropre = string.IsNullOrEmpty(pk.NomFichier) ? "fichier.bin" : Path.GetFileName(pk.NomFichier);
            var chemin = Path.Combine(dossier, prefixe + "_" + nomPropre);
            var bytes = Convert.FromBase64String(pk.Data);
            File.WriteAllBytes(chemin, bytes);
            m.Fichier = chemin;
            m.NomFichier = nomPropre;
            m.TailleFichier = bytes.LongLength;
            m.TypeMedia = type;
            ChargerImage(m);
        }
        catch { m.Texte = "(fichier illisible)"; }
    }

    public static void RecevoirVocal(ChatMessage m, Paquet pk)
    {
        try
        {
            var dossier = Path.Combine(DossierRecus, "Vocaux");
            Directory.CreateDirectory(dossier);
            var prefixe = pk.Id[..Math.Min(6, pk.Id.Length)];
            var chemin = Path.Combine(dossier, prefixe + "_vocal.wav");
            var bytes = Convert.FromBase64String(pk.Data);
            File.WriteAllBytes(chemin, bytes);
            m.Fichier = chemin;
            m.NomFichier = "Message vocal.wav";
            m.TailleFichier = bytes.LongLength;
            m.TypeMedia = "voice";
            m.EstVocal = true;
            m.DureeVocale = Math.Max(1, pk.Duree);
        }
        catch { m.Texte = "(message vocal illisible)"; }
    }

    public static void ChargerImage(ChatMessage m)
    {
        if (m.Fichier == null || !File.Exists(m.Fichier)) return;
        var ext = Path.GetExtension(m.Fichier).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp")
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 480;
                bmp.UriSource = new Uri(m.Fichier);
                bmp.EndInit();
                bmp.Freeze();
                m.Image = bmp;
                if (m.TypeMedia == "") m.TypeMedia = "image";
            }
            catch { }
        }
    }

    public static string ClassifierMedia(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" => "image",
        ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" => "audio",
        ".mp4" or ".avi" or ".mkv" or ".mov" or ".webm" => "video",
        _ => "doc"
    };

    public static string IconeMedia(string type) => type switch
    {
        "image" => "🖼",
        "audio" => "🎵",
        "video" => "🎬",
        "voice" => "🎤",
        _ => "📄"
    };

    public static void Ouvrir(string chemin)
    {
        try { if (File.Exists(chemin)) Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true }); }
        catch { }
    }

    public static void OuvrirDossier(string chemin)
    {
        try { if (File.Exists(chemin)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{chemin}\"") { UseShellExecute = true }); }
        catch { }
    }
}
