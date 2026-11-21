using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Chatp2pWPF.Models;

public class Peer : ObjetObservable
{
    static readonly Brush[] Palette = new[] { "#25D366", "#128C7E", "#34B7F1", "#E91E63", "#9C27B0", "#FF9800", "#5C6BC0", "#009688" }
        .Select(c => { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(c)!; b.Freeze(); return (Brush)b; }).ToArray();

    string id = "", nom = "", ip = "", apercu = "", bio = "Disponible", avatarBase64 = "",
           messageEpingleId = "", messageEpingleTexte = "";
    int port = 5000, nonLus;
    bool estGroupe, favori, epingle, muet, archive, bloque, enLigne, ecrit, ephemere;
    DateTime derniere = DateTime.MinValue;
    BitmapImage? avatarImage;

    public string Id { get => id; set { id = value; Maj(); } }
    public string Nom { get => nom; set { nom = value; Maj(); Maj(nameof(Initiale)); Maj(nameof(Couleur)); Maj(nameof(EmpreinteSecurite)); } }
    public string Ip { get => ip; set { ip = value; Maj(); Maj(nameof(EmpreinteSecurite)); } }
    public int Port { get => port; set { port = value; Maj(); } }
    public bool EstGroupe { get => estGroupe; set { estGroupe = value; Maj(); Maj(nameof(Initiale)); Maj(nameof(Icones)); Maj(nameof(StatutTexte)); } }
    public ObservableCollection<string> Membres { get; set; } = new();
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    [JsonIgnore] public StreamWriter? Writer { get; set; }
    [JsonIgnore] public SemaphoreSlim Verrou { get; } = new(1, 1);
    [JsonIgnore] public int JetonEcrit { get; set; }

    public string Bio { get => bio; set { bio = value; Maj(); Maj(nameof(StatutTexte)); } }
    public string AvatarBase64 { get => avatarBase64; set { avatarBase64 = value; Maj(); ChargerAvatar(); } }
    [JsonIgnore] public BitmapImage? AvatarImage { get => avatarImage; private set { avatarImage = value; Maj(); Maj(nameof(HasAvatar)); Maj(nameof(PasAvatar)); } }
    [JsonIgnore] public bool HasAvatar => avatarImage != null;
    [JsonIgnore] public bool PasAvatar => avatarImage == null;

    void ChargerAvatar()
    {
        if (string.IsNullOrEmpty(avatarBase64)) { AvatarImage = null; return; }
        try
        {
            var bytes = Convert.FromBase64String(avatarBase64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            AvatarImage = bmp;
        }
        catch { AvatarImage = null; }
    }

    public string Apercu { get => apercu; set { apercu = value; Maj(); } }
    public DateTime Derniere { get => derniere; set { derniere = value; Maj(); Maj(nameof(HeureDerniere)); } }
    public int NonLus { get => nonLus; set { nonLus = value; Maj(); Maj(nameof(NonLusTexte)); Maj(nameof(HasNonLus)); } }
    public bool Favori { get => favori; set { favori = value; Maj(); Maj(nameof(Icones)); } }
    public bool Epingle { get => epingle; set { epingle = value; Maj(); Maj(nameof(Icones)); } }
    public bool Muet { get => muet; set { muet = value; Maj(); Maj(nameof(Icones)); } }
    public bool Bloque { get => bloque; set { bloque = value; Maj(); Maj(nameof(Icones)); } }
    public bool Archive { get => archive; set { archive = value; Maj(); } }
    public bool Ephemere { get => ephemere; set { ephemere = value; Maj(); Maj(nameof(Icones)); } }
    public string MessageEpingleId { get => messageEpingleId; set { messageEpingleId = value; Maj(); } }
    public string MessageEpingleTexte { get => messageEpingleTexte; set { messageEpingleTexte = value; Maj(); Maj(nameof(HasMessageEpingle)); } }

    [JsonIgnore] public bool EnLigne { get => enLigne; set { enLigne = value; Maj(); Maj(nameof(StatutTexte)); } }
    [JsonIgnore] public bool Ecrit { get => ecrit; set { ecrit = value; Maj(); Maj(nameof(StatutTexte)); } }
    [JsonIgnore] public bool HasNonLus => nonLus > 0;
    [JsonIgnore] public bool HasMessageEpingle => !string.IsNullOrEmpty(messageEpingleTexte);
    [JsonIgnore] public string NonLusTexte => nonLus > 99 ? "99+" : nonLus > 0 ? nonLus.ToString() : "";
    [JsonIgnore] public string Initiale => estGroupe ? "👥" : nom.Length > 0 ? nom[0].ToString().ToUpper() : "?";
    [JsonIgnore] public Brush Couleur => Palette[Math.Abs(nom.Sum(c => c)) % Palette.Length];

    [JsonIgnore] public string HeureDerniere =>
        derniere == DateTime.MinValue ? "" :
        derniere.Date == DateTime.Today ? derniere.ToString("HH:mm") :
        derniere.Date == DateTime.Today.AddDays(-1) ? "Hier" : derniere.ToString("dd/MM/yyyy");

    [JsonIgnore] public string StatutTexte =>
        ecrit ? "en train d'écrire…" :
        estGroupe ? $"{Membres.Count} membre(s) · {string.Join(", ", Membres.Take(4))}" :
        enLigne ? (string.IsNullOrEmpty(bio) ? "en ligne" : $"en ligne · {bio}") :
        "hors ligne";

    [JsonIgnore] public string Icones =>
        (estGroupe ? "👥 " : "") + (favori ? "⭐" : "") + (epingle ? "📌" : "") +
        (muet ? "🔇" : "") + (ephemere ? "⏱" : "") + (bloque ? "⛔" : "");

    [JsonIgnore] public string EmpreinteSecurite
    {
        get
        {
            var cle = (nom + "|" + ip).Select((c, i) => ((c * (i + 7)) + 13) % 100000).ToList();
            while (cle.Count < 8) cle.Add((cle.Count * 17321 + 4219) % 100000);
            return string.Join(" ", cle.Take(8).Select(v => v.ToString("D5")));
        }
    }
}
