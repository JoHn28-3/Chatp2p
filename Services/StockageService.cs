using System.IO;
using System.Text.Json;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

/// <summary>Sauvegarde locale (AppData\ChatP2P) : réglages + discussions.</summary>
public static class StockageService
{
    static string Dossier => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatP2P");
    static string FichierPrm => Path.Combine(Dossier, "parametres.json");
    static string FichierDiscussions => Path.Combine(Dossier, "discussions.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    class PeerSauve
    {
        public string Nom { get; set; } = "";
        public string Ip { get; set; } = "";
        public bool Epingle { get; set; }
        public bool Muet { get; set; }
        public bool Archive { get; set; }
        public bool Bloque { get; set; }
        public bool Ephemere { get; set; }
        public int NonLus { get; set; }
        public string Apercu { get; set; } = "";
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

    public static List<Peer> ChargerDiscussions()
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
                    Nom = s.Nom, Ip = s.Ip, Epingle = s.Epingle, Muet = s.Muet, Archive = s.Archive,
                    Bloque = s.Bloque, Ephemere = s.Ephemere, NonLus = s.NonLus, Apercu = s.Apercu, Derniere = s.Derniere
                };
                foreach (var m in s.Messages)
                {
                    try { if (m.Fichier != null && File.Exists(m.Fichier)) FichierService.ChargerImage(m); } catch { }
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
                Nom = p.Nom, Ip = p.Ip, Epingle = p.Epingle, Muet = p.Muet, Archive = p.Archive,
                Bloque = p.Bloque, Ephemere = p.Ephemere, NonLus = p.NonLus, Apercu = p.Apercu,
                Derniere = p.Derniere, Messages = p.Messages.ToList()
            }).ToList();
            Ecrire(FichierDiscussions, JsonSerializer.Serialize(liste, Opt));
        }
        catch { }
    }
}
