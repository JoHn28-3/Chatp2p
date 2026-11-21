using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using Chatp2pWPF.Services;

namespace Chatp2pWPF.Models;

public class ChatMessage : ObjetObservable
{
    string texte = "", auteur = "", heure = "", statut = "✓", reaction = "",
           reponse = "", reponseDe = "", nomFichier = "", typeMedia = "", questionSondage = "";
    bool etoile, modifie, lu, supprime, transfere, epingle, afficherJour, estVocal, estSondage;
    bool enLecture;
    double progLecture;
    string vitesseLecture = "1x";
    long tailleFichier;
    int dureeVocale, prog = -1;
    BitmapImage? image;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool EstMoi { get; set; }
    public bool AttenteLecture { get; set; }
    public string? Fichier { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;

    public string Auteur { get => auteur; set { auteur = value; Maj(); Maj(nameof(HasAuteur)); } }
    public string Texte { get => texte; set { texte = value; Maj(); Maj(nameof(TexteAffiche)); Maj(nameof(HasTexte)); } }
    public string Heure { get => heure; set { heure = value; Maj(); Maj(nameof(Pied)); } }
    public string Statut { get => statut; set { statut = value; Maj(); } }
    public bool Lu { get => lu; set { lu = value; Maj(); } }
    public bool Supprime { get => supprime; set { supprime = value; Maj(); Maj(nameof(TexteAffiche)); Maj(nameof(HasTexte)); } }
    public bool Etoile { get => etoile; set { etoile = value; Maj(); Maj(nameof(Pied)); } }
    public bool Modifie { get => modifie; set { modifie = value; Maj(); Maj(nameof(Pied)); } }
    public bool Transfere { get => transfere; set { transfere = value; Maj(); } }
    public bool Epingle { get => epingle; set { epingle = value; Maj(); Maj(nameof(Pied)); } }
    public bool AfficherJour { get => afficherJour; set { afficherJour = value; Maj(); } }
    public string Reaction { get => reaction; set { reaction = value; Maj(); Maj(nameof(HasReaction)); } }
    public string Reponse { get => reponse; set { reponse = value; Maj(); Maj(nameof(HasReponse)); } }
    public string ReponseDe { get => reponseDe; set { reponseDe = value; Maj(); } }
    public string NomFichier { get => nomFichier; set { nomFichier = value; Maj(); Maj(nameof(HasFichier)); } }
    public string TypeMedia { get => typeMedia; set { typeMedia = value; Maj(); Maj(nameof(IconeFichier)); } }
    public long TailleFichier { get => tailleFichier; set { tailleFichier = value; Maj(); Maj(nameof(TailleAffichee)); } }

    public bool EstVocal { get => estVocal; set { estVocal = value; Maj(); Maj(nameof(HasFichier)); Maj(nameof(HasTexte)); Maj(nameof(TexteAffiche)); } }
    public int DureeVocale { get => dureeVocale; set { dureeVocale = value; Maj(); Maj(nameof(DureeVocaleTexte)); Maj(nameof(TexteAffiche)); } }

    public bool EstSondage { get => estSondage; set { estSondage = value; Maj(); Maj(nameof(HasTexte)); Maj(nameof(TexteAffiche)); } }
    public string QuestionSondage { get => questionSondage; set { questionSondage = value; Maj(); Maj(nameof(TexteAffiche)); } }
    public ObservableCollection<OptionSondage> OptionsSondage { get; set; } = new();

    [JsonIgnore] public BitmapImage? Image { get => image; set { image = value; Maj(); Maj(nameof(HasImage)); Maj(nameof(HasFichier)); } }
    [JsonIgnore] public bool EnLecture { get => enLecture; set { enLecture = value; Maj(); Maj(nameof(BoutonLecture)); } }
    [JsonIgnore] public double ProgressionLecture { get => progLecture; set { progLecture = value; Maj(); } }
    [JsonIgnore] public string VitesseLecture { get => vitesseLecture; set { vitesseLecture = value; Maj(); } }
    [JsonIgnore] public int Progression { get => prog; set { prog = value; Maj(); Maj(nameof(EnTransfert)); } }

    [JsonIgnore] public bool HasAuteur => !EstMoi && !string.IsNullOrEmpty(auteur);
    [JsonIgnore] public bool HasReaction => reaction != "";
    [JsonIgnore] public bool HasReponse => reponse != "";
    [JsonIgnore] public bool HasFichier => nomFichier != "" && image == null && !estVocal;
    [JsonIgnore] public bool HasImage => image != null;
    [JsonIgnore] public bool HasTexte => texte != "" && !supprime && !estSondage && !estVocal;
    [JsonIgnore] public bool EnTransfert => prog >= 0 && prog < 100;
    [JsonIgnore] public string BoutonLecture => enLecture ? "⏸" : "▶";
    [JsonIgnore] public string DureeVocaleTexte => TimeSpan.FromSeconds(dureeVocale).ToString(@"mm\:ss");
    [JsonIgnore] public string IconeFichier => FichierService.IconeMedia(typeMedia);

    [JsonIgnore] public string TexteAffiche =>
        supprime ? "🚫 Ce message a été supprimé" :
        estSondage ? $"📊 {questionSondage}" :
        estVocal ? $"🎤 Message vocal ({DureeVocaleTexte})" :
        texte;

    [JsonIgnore] public string TailleAffichee => tailleFichier > 0
        ? (tailleFichier < 1024 * 1024 ? $"{Math.Max(1, tailleFichier / 1024)} Ko" : $"{tailleFichier / (1024.0 * 1024):F1} Mo")
        : "";

    [JsonIgnore] public string Pied => (epingle ? "📌 · " : "") + (modifie ? "modifié · " : "") + heure + (etoile ? " ★" : "");

    [JsonIgnore] public string Jour =>
        Date.Date == DateTime.Today ? "Aujourd'hui" :
        Date.Date == DateTime.Today.AddDays(-1) ? "Hier" :
        Date.ToString("dddd d MMMM yyyy");

    [JsonIgnore] public int TotalVotes => OptionsSondage.Sum(o => o.Votes);
    [JsonIgnore] public string TotalVotesTexte => TotalVotes == 1 ? "1 vote" : $"{TotalVotes} votes";

    public void RafraichirSondage(string monNom)
    {
        var total = TotalVotes;
        foreach (var opt in OptionsSondage)
        {
            opt.MessageId = Id;
            opt.MajStats(total, monNom);
        }
        Maj(nameof(TotalVotes));
        Maj(nameof(TotalVotesTexte));
    }
}
