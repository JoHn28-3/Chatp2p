using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Chatp2pWPF.Models;

public class ChatMessage : ObjetObservable
{
    string texte = "", heure = "", statut = "", reaction = "", reponse = "", nomFichier = "";
    bool etoile, modifie, lu, transfere, afficherJour;
    BitmapImage? image;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool EstMoi { get; set; }
    public bool Supprime { get; set; }
    public bool AttenteLecture { get; set; }
    public string? Fichier { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;

    public string Texte { get => texte; set { texte = value; Maj(); } }
    public string Heure { get => heure; set { heure = value; Maj(); Maj(nameof(Pied)); } }
    public string Statut { get => statut; set { statut = value; Maj(); } }
    public bool Lu { get => lu; set { lu = value; Maj(); } }
    public bool Etoile { get => etoile; set { etoile = value; Maj(); Maj(nameof(Pied)); } }
    public bool Modifie { get => modifie; set { modifie = value; Maj(); Maj(nameof(Pied)); } }
    public bool Transfere { get => transfere; set { transfere = value; Maj(); } }
    public bool AfficherJour { get => afficherJour; set { afficherJour = value; Maj(); } }
    public string Reaction { get => reaction; set { reaction = value; Maj(); Maj(nameof(HasReaction)); } }
    public string Reponse { get => reponse; set { reponse = value; Maj(); Maj(nameof(HasReponse)); } }
    public string NomFichier { get => nomFichier; set { nomFichier = value; Maj(); Maj(nameof(HasFichier)); } }
    [JsonIgnore] public BitmapImage? Image { get => image; set { image = value; Maj(); Maj(nameof(HasImage)); } }

    [JsonIgnore] public bool HasReaction => reaction != "";
    [JsonIgnore] public bool HasReponse => reponse != "";
    [JsonIgnore] public bool HasFichier => nomFichier != "";
    [JsonIgnore] public bool HasImage => image != null;
    [JsonIgnore] public string Pied => (Modifie ? "modifié · " : "") + Heure + (Etoile ? " ★" : "");
    [JsonIgnore] public string Jour =>
        Date.Date == DateTime.Today ? "Aujourd'hui" :
        Date.Date == DateTime.Today.AddDays(-1) ? "Hier" :
        Date.ToString("dddd d MMMM yyyy");
}
