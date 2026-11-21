using System.IO;
using System.Text.Json;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public static class StockageService
{
    static string Dossier => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatP2P");
    static string FichierPrm => Path.Combine(Dossier, "parametres.json");
    static string FichierDiscussions => Path.Combine(Dossier, "discussions.json");
    static string FichierAppels => Path.Combine(Dossier, "appels.json");
    static string FichierStatuts => Path.Combine(Dossier, "statuts.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    class PeerSauve
    {
        public string Id { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Ip { get; set; } = "";
        public int Port { get; set; } = 5000;
        public bool EstGroupe { get; set; }
        public List<string> Membres { get; set; } = new();
        public string Bio { get; set; } = "Disponible";
        public string AvatarBase64 { get; set; } = "";
        public bool Favori { get; set; }
        public bool Epingle { get; set; }
        public bool Muet { get; set; }
        public bool Archive { get; set; }
        public bool Bloque { get; set; }
        public bool Ephemere { get; set; }
        public int NonLus { get; set; }
        public string Apercu { get; set; } = "";
        public string MessageEpingleId { get; set; } = "";
        public string MessageEpingleTexte { get; set; } = "";
        public DateTime Derniere { get; set; }
        public List<ChatMessage> Messages { get; set; } = new();
    }

    static void Ecrire(string chemin, string json)
    {
        Directory.CreateDirectory(Dossier);
        var tmp = chemin + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, chemin, true);
    }

    public static Parametres ChargerParametres()
    {
        try { if (File.Exists(FichierPrm)) return JsonSerializer.Deserialize<Parametres>(File.ReadAllText(FichierPrm)) ?? new(); }
        catch { }
        return new();
    }

    public static void SauverParametres(Parametres p)
    {
        try { Ecrire(FichierPrm, JsonSerializer.Serialize(p, Opt)); } catch { }
    }

    public static List<Peer> ChargerDiscussions(string monPseudo)
    {
        var res = new List<Peer>();
        try
        {
            if (!File.Exists(FichierDiscussions)) return res;
            var liste = JsonSerializer.Deserialize<List<PeerSauve>>(File.ReadAllText(FichierDiscussions)) ?? new();
            foreach (var s in liste)
            {
                var p = new Peer
                {
                    Id = string.IsNullOrEmpty(s.Id) ? s.Nom : s.Id,
                    Nom = s.Nom,
                    Ip = s.Ip,
                    Port = s.Port > 0 ? s.Port : 5000,
                    EstGroupe = s.EstGroupe,
                    Bio = s.Bio,
                    AvatarBase64 = s.AvatarBase64,
                    Favori = s.Favori,
                    Epingle = s.Epingle,
                    Muet = s.Muet,
                    Archive = s.Archive,
                    Bloque = s.Bloque,
                    Ephemere = s.Ephemere,
                    NonLus = s.NonLus,
                    Apercu = s.Apercu,
                    MessageEpingleId = s.MessageEpingleId,
                    MessageEpingleTexte = s.MessageEpingleTexte,
                    Derniere = s.Derniere
                };
                foreach (var m in s.Membres) p.Membres.Add(m);
                foreach (var m in s.Messages)
                {
                    try { if (m.Fichier != null && File.Exists(m.Fichier)) FichierService.ChargerImage(m); } catch { }
                    if (m.EstSondage) m.RafraichirSondage(monPseudo);
                    p.Messages.Add(m);
                }
                res.Add(p);
            }
        }
        catch { }
        return res;
    }

    public static void SauverDiscussions(IEnumerable<Peer> peers)
    {
        try
        {
            var liste = peers.Select(p => new PeerSauve
            {
                Id = p.Id, Nom = p.Nom, Ip = p.Ip, Port = p.Port, EstGroupe = p.EstGroupe,
                Membres = p.Membres.ToList(), Bio = p.Bio, AvatarBase64 = p.AvatarBase64,
                Favori = p.Favori, Epingle = p.Epingle, Muet = p.Muet, Archive = p.Archive,
                Bloque = p.Bloque, Ephemere = p.Ephemere, NonLus = p.NonLus, Apercu = p.Apercu,
                MessageEpingleId = p.MessageEpingleId, MessageEpingleTexte = p.MessageEpingleTexte,
                Derniere = p.Derniere, Messages = p.Messages.ToList()
            }).ToList();
            Ecrire(FichierDiscussions, JsonSerializer.Serialize(liste, Opt));
        }
        catch { }
    }

    public static List<AppelInfo> ChargerAppels()
    {
        try { if (File.Exists(FichierAppels)) return JsonSerializer.Deserialize<List<AppelInfo>>(File.ReadAllText(FichierAppels)) ?? new(); }
        catch { }
        return new();
    }

    public static void SauverAppels(IEnumerable<AppelInfo> appels)
    {
        try { Ecrire(FichierAppels, JsonSerializer.Serialize(appels.ToList(), Opt)); } catch { }
    }

    public static List<StatutStory> ChargerStatuts()
    {
        try
        {
            if (!File.Exists(FichierStatuts)) return new();
            var liste = JsonSerializer.Deserialize<List<StatutStory>>(File.ReadAllText(FichierStatuts)) ?? new();
            return liste.Where(s => s.EstActif).ToList();
        }
        catch { return new(); }
    }

    public static void SauverStatuts(IEnumerable<StatutStory> statuts)
    {
        try { Ecrire(FichierStatuts, JsonSerializer.Serialize(statuts.Where(s => s.EstActif).ToList(), Opt)); } catch { }
    }
}
