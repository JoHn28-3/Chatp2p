using System.Windows.Media.Imaging;

namespace Chatp2pWPF.Models;

public class ChatMessage : ObjetObservable
{
    string texte = "", heure = "", statut = "", reaction = "", reponse = "", nomFichier = "";
    bool etoile, modifie, lu;
    BitmapImage? image;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool EstMoi { get; set; }
    public bool Supprime { get; set; }
    public bool AttenteLecture { get; set; }
    public string? Fichier { get; set; }

    public string Texte { get => texte; set { texte = value; Maj(); } }
    public string Heure { get => heure; set { heure = value; Maj(); Maj(nameof(Pied)); } }
    public string Statut { get => statut; set { statut = value; Maj(); } }
    public bool Lu { get => lu; set { lu = value; Maj(); } }
    public bool Etoile { get => etoile; set { etoile = value; Maj(); Maj(nameof(Pied)); } }
    public bool Modifie { get => modifie; set { modifie = value; Maj(); Maj(nameof(Pied)); } }
    public string Reaction { get => reaction; set { reaction = value; Maj(); Maj(nameof(HasReaction)); } }
    public string Reponse { get => reponse; set { reponse = value; Maj(); Maj(nameof(HasReponse)); } }
    public string NomFichier { get => nomFichier; set { nomFichier = value; Maj(); Maj(nameof(HasFichier)); } }
    public BitmapImage? Image { get => image; set { image = value; Maj(); Maj(nameof(HasImage)); } }

    public bool HasReaction => reaction != "";
    public bool HasReponse => reponse != "";
    public bool HasFichier => nomFichier != "";
    public bool HasImage => image != null;
    public string Pied => (Modifie ? "modifié · " : "") + Heure + (Etoile ? " ★" : "");
}