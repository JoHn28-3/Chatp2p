using System.Text.Json.Serialization;

namespace Chatp2pWPF.Models;

public class OptionSondage : ObjetObservable
{
    double pourcentage;
    bool estVoteParMoi;

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Texte { get; set; } = "";
    public List<string> Votants { get; set; } = new();

    [JsonIgnore] public string MessageId { get; set; } = "";
    [JsonIgnore] public int Votes => Votants.Count;
    [JsonIgnore] public double Pourcentage { get => pourcentage; private set { pourcentage = value; Maj(); Maj(nameof(PourcentageTexte)); } }
    [JsonIgnore] public string PourcentageTexte => $"{pourcentage:0}%";
    [JsonIgnore] public string VotantsTexte => Votes == 1 ? "1 vote" : $"{Votes} votes";
    [JsonIgnore] public bool EstVoteParMoi { get => estVoteParMoi; private set { estVoteParMoi = value; Maj(); Maj(nameof(PrefixeVote)); } }
    [JsonIgnore] public string PrefixeVote => estVoteParMoi ? "☑ " : "☐ ";

    public void MajStats(int totalVotes, string monNom)
    {
        Pourcentage = totalVotes > 0 ? Votes * 100.0 / totalVotes : 0;
        EstVoteParMoi = Votants.Contains(monNom);
        Maj(nameof(Votes));
        Maj(nameof(VotantsTexte));
    }
}
