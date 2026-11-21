using System.IO;
using System.Media;
using System.Windows.Threading;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public class ChatService
{
    readonly ReseauService reseau;
    readonly Parametres prm;
    readonly Dispatcher ui;
    DateTime dernierSaisie = DateTime.MinValue;

    public Peer? Selection { get; set; }
    public Func<bool> FenetreActive { get; set; } = () => true;
    public event Action<Peer, ChatMessage>? MessageAjoute;
    public event Action? ListeChangee;

    public ChatService(ReseauService reseau, Parametres prm, Dispatcher ui)
    {
        this.reseau = reseau;
        this.prm = prm;
        this.ui = ui;
        reseau.PaquetRecu += Traiter;
    }

    public static bool PeutEnvoyer(Peer? p, out string erreur)
    {
        erreur = "";
        if (p == null) erreur = "Aucun contact : cliquez d'abord sur « Connecter » (l'autre PC doit avoir l'application ouverte).";
        else if (p.Bloque) erreur = "Ce contact est bloqué.";
        else if (!p.EnLigne) erreur = "Ce contact est hors ligne.";
        return erreur == "";
    }

    // ---------- Actions de l'utilisateur ----------
    public Task EnvoyerTexteAsync(Peer p, string texte, ChatMessage? reponseA, bool transfere = false)
    {
        var m = new ChatMessage
        {
            Texte = texte,
            EstMoi = true,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            Reponse = reponseA?.Texte ?? "",
            Transfere = transfere
        };
        Ajouter(p, m, "Vous : " + texte);
        return reseau.EnvoyerAsync(p, new Paquet
        { Type = "msg", Id = m.Id, De = prm.Pseudo, Texte = texte, Reponse = m.Reponse, Transfere = transfere });
    }

    public async Task EnvoyerFichierAsync(Peer p, string chemin, bool transfere = false)
    {
        var nom = new FileInfo(chemin).Name;
        var data = await FichierService.EncoderAsync(chemin);
        var m = new ChatMessage
        {
            EstMoi = true,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            NomFichier = nom,
            Fichier = chemin,
            Transfere = transfere
        };
        FichierService.ChargerImage(m);
        Ajouter(p, m, "Vous : 📎 " + nom);
        await reseau.EnvoyerAsync(p, new Paquet
        { Type = "file", Id = m.Id, De = prm.Pseudo, NomFichier = nom, Data = data, Transfere = transfere });
    }

    public Task ModifierAsync(Peer p, ChatMessage m, string texte)
    {
        m.Texte = texte;
        m.Modifie = true;
        return reseau.EnvoyerAsync(p, new Paquet { Type = "edit", Id = m.Id, De = prm.Pseudo, Texte = texte });
    }

    public Task SupprimerPourTousAsync(Peer p, ChatMessage m)
    {
        Effacer(m);
        return reseau.EnvoyerAsync(p, new Paquet { Type = "delete", Id = m.Id, De = prm.Pseudo });
    }

    public Task ReagirAsync(Peer p, ChatMessage m, string emoji)
    {
        m.Reaction = emoji;
        return reseau.EnvoyerAsync(p, new Paquet { Type = "react", Id = m.Id, De = prm.Pseudo, Reaction = emoji });
    }

    public void Frappe(Peer p)
    {
        if (!p.EnLigne || !prm.IndicateurSaisie) return;
        if ((DateTime.Now - dernierSaisie).TotalSeconds < 2) return;
        dernierSaisie = DateTime.Now;
        _ = reseau.EnvoyerAsync(p, new Paquet { Type = "typing", De = prm.Pseudo });
    }

    public void Vider(Peer p)
    {
        p.Messages.Clear();
        p.Apercu = "";
        ListeChangee?.Invoke();
    }

    public void EnvoyerLus(Peer p)
    {
        foreach (var m in p.Messages.Where(x => !x.EstMoi && x.AttenteLecture).ToList())
        {
            m.AttenteLecture = false;
            if (prm.AccusesLecture)
                _ = reseau.EnvoyerAsync(p, new Paquet { Type = "read", Id = m.Id, De = prm.Pseudo });
        }
    }

    // ---------- Paquets reçus ----------
    void Traiter(Peer p, Paquet pk)
    {
        if (p.Bloque) return;
        var cible = p.Messages.FirstOrDefault(m => m.Id == pk.Id);

        switch (pk.Type)
        {
            case "msg":
            case "file":
                {
                    var m = new ChatMessage
                    {
                        Id = pk.Id,
                        Texte = pk.Texte,
                        Reponse = pk.Reponse,
                        Heure = pk.Date.ToString("HH:mm"),
                        Date = pk.Date,
                        Transfere = pk.Transfere,
                        AttenteLecture = true
                    };
                    if (pk.Type == "file") FichierService.Recevoir(m, pk);
                    Ajouter(p, m, pk.Type == "file" ? "📎 " + pk.NomFichier : pk.Texte);
                    _ = reseau.EnvoyerAsync(p, new Paquet { Type = "ack", Id = pk.Id, De = prm.Pseudo });
                    if (Selection == p && FenetreActive()) EnvoyerLus(p);
                    else
                    {
                        p.NonLus++;
                        if (!p.Muet && prm.NotifSon) SystemSounds.Asterisk.Play();
                    }
                    break;
                }
            case "ack":
                if (cible is { EstMoi: true } && !cible.Lu) cible.Statut = "✓✓";
                break;
            case "read":
                if (cible is { EstMoi: true }) { cible.Statut = "✓✓"; cible.Lu = true; }
                break;
            case "edit":
                if (cible is { EstMoi: false, Supprime: false }) { cible.Texte = pk.Texte; cible.Modifie = true; }
                break;
            case "delete":
                if (cible is { EstMoi: false }) Effacer(cible);
                break;
            case "react":
                if (cible != null) cible.Reaction = pk.Reaction;
                break;
            case "typing":
                {
                    if (!prm.IndicateurSaisie) break;
                    p.Ecrit = true;
                    var jeton = ++p.JetonEcrit;
                    _ = Task.Delay(2500).ContinueWith(_ => ui.Invoke(() =>
                    {
                        if (p.JetonEcrit == jeton) p.Ecrit = false;
                    }));
                    break;
                }
        }
        ListeChangee?.Invoke();
    }

    // ---------- Utilitaires ----------
    void Ajouter(Peer p, ChatMessage m, string apercu)
    {
        var prec = p.Messages.LastOrDefault();
        m.AfficherJour = prec == null || prec.Date.Date != m.Date.Date;
        p.Messages.Add(m);
        p.Apercu = apercu;
        p.Derniere = DateTime.Now;
        MessageAjoute?.Invoke(p, m);
        ListeChangee?.Invoke();
        if (p.Ephemere)
            _ = Task.Delay(60000).ContinueWith(_ => ui.Invoke(() => { p.Messages.Remove(m); }));
    }

    static void Effacer(ChatMessage m)
    {
        m.Texte = "🚫 Message supprimé";
        m.Image = null; m.NomFichier = ""; m.Reponse = ""; m.Reaction = "";
        m.Fichier = null; m.Supprime = true;
    }
}
