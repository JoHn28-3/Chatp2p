using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Chatp2pWPF.Models;

public class StatutStory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Auteur { get; set; } = "";
    public string Texte { get; set; } = "";
    public string CouleurFond { get; set; } = "#00A884";
    public string ImageBase64 { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;

    [JsonIgnore] public bool EstActif => (DateTime.Now - Date).TotalHours < 24;
    [JsonIgnore] public string Initiale => Auteur.Length > 0 ? Auteur[0].ToString().ToUpper() : "?";
    [JsonIgnore] public string HeureTexte => Date.Date == DateTime.Today ? $"Aujourd'hui à {Date:HH:mm}" : $"Hier à {Date:HH:mm}";
    [JsonIgnore] public bool HasImage => Image != null;

    BitmapImage? _img;
    [JsonIgnore] public BitmapImage? Image
    {
        get
        {
            if (_img != null || string.IsNullOrEmpty(ImageBase64)) return _img;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(Convert.FromBase64String(ImageBase64));
                bmp.EndInit();
                bmp.Freeze();
                _img = bmp;
            }
            catch { }
            return _img;
        }
    }
}
