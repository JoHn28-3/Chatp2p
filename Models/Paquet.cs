namespace Chatp2pWPF.Models;

public class Paquet
{
    public string Type { get; set; } = "msg";
    public string Id { get; set; } = "";
    public string De { get; set; } = "";
    public string Texte { get; set; } = "";
    public string Reponse { get; set; } = "";
    public string ReponseDe { get; set; } = "";
    public string Reaction { get; set; } = "";
    public string NomFichier { get; set; } = "";
    public string Data { get; set; } = "";
    public bool Transfere { get; set; }
    public bool Epingle { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;

    // Profil
    public string Avatar { get; set; } = "";
    public string Bio { get; set; } = "";

    // Vocal / Appel
    public int Duree { get; set; }
    public string TypeAppel { get; set; } = "";

    // Sondage
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string OptionId { get; set; } = "";

    // Groupe
    public string GroupeId { get; set; } = "";
    public string GroupeNom { get; set; } = "";
    public List<string> Membres { get; set; } = new();

    // Statuts / Stories
    public string Couleur { get; set; } = "";
}
