using System.Text.Json.Serialization;

namespace Chatp2pWPF.Models;

public class AppelInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nom { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Type { get; set; } = "Audio"; // Audio, Vidéo
    public DateTime Date { get; set; } = DateTime.Now;
    public int DureeSecondes { get; set; }
    public bool Entrant { get; set; }
    public bool Manque { get; set; }

    [JsonIgnore] public string IconeType => Type == "Vidéo" ? "📹" : "📞";
    [JsonIgnore] public string DirectionTexte => Manque ? "↙ Appel manqué" : Entrant ? "↙ Entrant" : "↗ Sortant";
    [JsonIgnore] public string CouleurStatut => Manque ? "#EF5350" : "#00A884";
    [JsonIgnore] public string DureeTexte => Manque ? "Manqué" : DureeSecondes > 0 ? TimeSpan.FromSeconds(DureeSecondes).ToString(@"mm\:ss") : "Terminé";
    [JsonIgnore] public string DateTexte => Date.Date == DateTime.Today ? Date.ToString("HH:mm") : Date.ToString("dd/MM HH:mm");
}
