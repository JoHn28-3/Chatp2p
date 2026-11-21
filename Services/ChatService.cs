using System.Collections.ObjectModel;
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
    public ObservableCollection<AppelInfo> Appels { get; } = new();
    public ObservableCollection<StatutStory> Statuts { get; } = new();

    public event Action<Peer, ChatMessage>? MessageAjoute;
    public event Action? ListeChangee;
    public event Action<string>? Notification;
    public event Action<Peer, string, string>? AppelEntrant; // peer, callId, typeAppel
    public event Action<Peer, string, bool>? AppelReponse;
    public event Action<Peer, string>? AppelTermine;

    public ChatService(ReseauService reseau, Parametres prm, Dispatcher ui)
    {
        this.reseau = reseau;
        this.prm = prm;
        this.ui = ui;
        reseau.PaquetRecu += Traiter;
        reseau.PeerDeconnecte += p => { p.Ecrit = false; ListeChangee?.Invoke(); };
    }

    public static bool PeutEnvoyer(Peer? p, out string erreur)
    {
        erreur = "";
        if (p == null) erreur = "Aucun contact sélectionné : cliquez sur ＋ ou entrez une adresse IP.";
        else if (p.Bloque) erreur = "Ce contact est bloqué.";
        else if (!p.EstGroupe && !p.EnLigne) erreur = $"{p.Nom} est hors ligne.";
        return erreur == "";
    }

    IEnumerable<Peer> Destinataires(Peer cible) =>
        cible.EstGroupe
            ? reseau.Peers.Where(x => !x.EstGroupe && x.EnLigne && cible.Membres.Contains(x.Nom))
            : new[] { cible };

    Task DiffuserPaquetAsync(Peer cible, Paquet pk)
    {
        if (cible.EstGroupe) pk.GroupeId = cible.Id;
        return Task.WhenAll(Destinataires(cible).Select(d => reseau.EnvoyerAsync(d, pk)));
    }

    // ---------- Envois ----------
    public Task EnvoyerTexteAsync(Peer p, string texte, ChatMessage? reponseA, bool transfere = false)
    {
        var m = new ChatMessage
        {
            EstMoi = true,
            Auteur = prm.Pseudo,
            Texte = texte,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            Reponse = reponseA?.TexteAffiche ?? "",
            ReponseDe = reponseA != null ? (reponseA.EstMoi ? "Vous" : (string.IsNullOrEmpty(reponseA.Auteur) ? p.Nom : reponseA.Auteur)) : "",
            Transfere = transfere
        };
        Ajouter(p, m, "Vous : " + texte);
        var pk = new Paquet
        {
            Type = p.EstGroupe ? "group_msg" : "msg",
            Id = m.Id,
            De = prm.Pseudo,
            Texte = texte,
            Reponse = m.Reponse,
            ReponseDe = m.ReponseDe,
            Transfere = transfere,
            Date = DateTime.Now
        };
        return DiffuserPaquetAsync(p, pk);
    }

    public async Task EnvoyerFichierAsync(Peer p, string chemin, bool transfere = false)
    {
        if (!File.Exists(chemin)) return;
        var info = new FileInfo(chemin);
        var nom = info.Name;
        var ext = Path.GetExtension(nom).ToLowerInvariant();
        var type = FichierService.ClassifierMedia(ext);

        var m = new ChatMessage
        {
            EstMoi = true,
            Auteur = prm.Pseudo,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            NomFichier = nom,
            Fichier = chemin,
            TailleFichier = info.Length,
            TypeMedia = type,
            Transfere = transfere,
            Progression = 0
        };
        FichierService.ChargerImage(m);
        Ajouter(p, m, $"Vous : {FichierService.IconeMedia(type)} {nom}");

        var data = await FichierService.EncoderAsync(chemin);
        m.Progression = 60;
        var pk = new Paquet
        {
            Type = "file",
            Id = m.Id,
            De = prm.Pseudo,
            NomFichier = nom,
            Data = data,
            Transfere = transfere,
            Date = DateTime.Now
        };
        await DiffuserPaquetAsync(p, pk);
        m.Progression = 100;
    }

    public async Task EnvoyerVocalAsync(Peer p, string cheminWav, int dureeSec)
    {
        if (!File.Exists(cheminWav)) return;
        var m = new ChatMessage
        {
            EstMoi = true,
            Auteur = prm.Pseudo,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            Fichier = cheminWav,
            NomFichier = "Message vocal.wav",
            TailleFichier = new FileInfo(cheminWav).Length,
            TypeMedia = "voice",
            EstVocal = true,
            DureeVocale = Math.Max(1, dureeSec)
        };
        Ajouter(p, m, $"Vous : 🎤 Message vocal ({m.DureeVocaleTexte})");
        var data = await FichierService.EncoderAsync(cheminWav);
        var pk = new Paquet
        {
            Type = "voice",
            Id = m.Id,
            De = prm.Pseudo,
            Data = data,
            Duree = m.DureeVocale,
            Date = DateTime.Now
        };
        await DiffuserPaquetAsync(p, pk);
    }

    public Task EnvoyerSondageAsync(Peer p, string question, List<string> options)
    {
        var m = new ChatMessage
        {
            EstMoi = true,
            Auteur = prm.Pseudo,
            Statut = "✓",
            Heure = DateTime.Now.ToString("HH:mm"),
            EstSondage = true,
            QuestionSondage = question
        };
        foreach (var opt in options)
            m.OptionsSondage.Add(new OptionSondage { Texte = opt });
        m.RafraichirSondage(prm.Pseudo);

        Ajouter(p, m, $"Vous : 📊 {question}");
        return DiffuserPaquetAsync(p, new Paquet
        {
            Type = "poll",
            Id = m.Id,
            De = prm.Pseudo,
            Question = question,
            Options = options,
            Date = DateTime.Now
        });
    }

    public Task VoterSondageAsync(Peer p, ChatMessage m, OptionSondage opt)
    {
        if (opt.Votants.Contains(prm.Pseudo)) opt.Votants.Remove(prm.Pseudo);
        else opt.Votants.Add(prm.Pseudo);
        m.RafraichirSondage(prm.Pseudo);
        ListeChangee?.Invoke();

        return DiffuserPaquetAsync(p, new Paquet
        {
            Type = "poll_vote",
            Id = m.Id,
            De = prm.Pseudo,
            OptionId = opt.Id
        });
    }

    public Task EpinglerMessageAsync(Peer p, ChatMessage m)
    {
        bool etat = !m.Epingle;
        foreach (var item in p.Messages) item.Epingle = false;
        m.Epingle = etat;
        p.MessageEpingleId = etat ? m.Id : "";
        p.MessageEpingleTexte = etat ? m.TexteAffiche : "";
        ListeChangee?.Invoke();
        return DiffuserPaquetAsync(p, new Paquet
        {
            Type = "pin",
            Id = m.Id,
            De = prm.Pseudo,
            Epingle = etat,
            Texte = m.TexteAffiche
        });
    }

    public Task ModifierAsync(Peer p, ChatMessage m, string texte)
    {
        m.Texte = texte;
        m.Modifie = true;
        return DiffuserPaquetAsync(p, new Paquet { Type = "edit", Id = m.Id, De = prm.Pseudo, Texte = texte });
    }

    public Task SupprimerPourTousAsync(Peer p, ChatMessage m)
    {
        Effacer(m);
        return DiffuserPaquetAsync(p, new Paquet { Type = "delete", Id = m.Id, De = prm.Pseudo });
    }

    public Task ReagirAsync(Peer p, ChatMessage m, string emoji)
    {
        m.Reaction = m.Reaction == emoji ? "" : emoji;
        return DiffuserPaquetAsync(p, new Paquet { Type = "react", Id = m.Id, De = prm.Pseudo, Reaction = m.Reaction });
    }

    public void Frappe(Peer p)
    {
        if (p.EstGroupe || !p.EnLigne || !prm.IndicateurSaisie) return;
        if ((DateTime.Now - dernierSaisie).TotalSeconds < 2) return;
        dernierSaisie = DateTime.Now;
        _ = reseau.EnvoyerAsync(p, new Paquet { Type = "typing", De = prm.Pseudo });
    }

    public void EnvoyerLus(Peer p)
    {
        if (p.EstGroupe) return;
        foreach (var m in p.Messages.Where(x => !x.EstMoi && x.AttenteLecture).ToList())
        {
            m.AttenteLecture = false;
            if (prm.AccusesLecture)
                _ = reseau.EnvoyerAsync(p, new Paquet { Type = "read", Id = m.Id, De = prm.Pseudo });
        }
    }

    public void Vider(Peer p)
    {
        p.Messages.Clear();
        p.Apercu = "";
        p.MessageEpingleId = "";
        p.MessageEpingleTexte = "";
        ListeChangee?.Invoke();
    }

    // ---------- Groupes ----------
    public async Task<Peer> CreerGroupeAsync(string nomGroupe, List<Peer> membres)
    {
        var grp = new Peer
        {
            Id = "grp_" + Guid.NewGuid().ToString("N")[..8],
            Nom = nomGroupe,
            EstGroupe = true,
            Derniere = DateTime.Now,
            Apercu = "Groupe créé"
        };
        grp.Membres.Add(prm.Pseudo);
        foreach (var m in membres)
            if (!grp.Membres.Contains(m.Nom)) grp.Membres.Add(m.Nom);

        reseau.Peers.Add(grp);
        ListeChangee?.Invoke();

        var pk = new Paquet
        {
            Type = "group_invite",
            De = prm.Pseudo,
            GroupeId = grp.Id,
            GroupeNom = nomGroupe,
            Membres = grp.Membres.ToList()
        };
        foreach (var m in membres.Where(x => x.EnLigne))
            await reseau.EnvoyerAsync(m, pk);

        return grp;
    }

    // ---------- Statuts / Stories ----------
    public async Task PublierStatutAsync(string texte, string couleurFond, string? cheminImage)
    {
        var imgB64 = "";
        if (!string.IsNullOrEmpty(cheminImage) && File.Exists(cheminImage))
            imgB64 = await FichierService.EncoderAsync(cheminImage);

        var s = new StatutStory
        {
            Auteur = prm.Pseudo,
            Texte = texte,
            CouleurFond = couleurFond,
            ImageBase64 = imgB64,
            Date = DateTime.Now
        };
        Statuts.Insert(0, s);
        ListeChangee?.Invoke();

        var pk = new Paquet
        {
            Type = "story_post",
            Id = s.Id,
            De = prm.Pseudo,
            Texte = texte,
            Couleur = couleurFond,
            Data = imgB64,
            Date = s.Date
        };
        foreach (var p in reseau.Peers.Where(x => !x.EstGroupe && x.EnLigne))
            await reseau.EnvoyerAsync(p, pk);
    }

    public async Task SupprimerStatutAsync(StatutStory st)
    {
        if (Statuts.Remove(st)) ListeChangee?.Invoke();
        var pk = new Paquet { Type = "story_delete", Id = st.Id, De = prm.Pseudo };
        foreach (var p in reseau.Peers.Where(x => !x.EstGroupe && x.EnLigne))
            await reseau.EnvoyerAsync(p, pk);
    }

    // ---------- Appels ----------
    public string LancerAppel(Peer p, string typeAppel)
    {
        var callId = Guid.NewGuid().ToString("N")[..8];
        _ = reseau.EnvoyerAsync(p, new Paquet { Type = "call_offer", Id = callId, De = prm.Pseudo, TypeAppel = typeAppel });
        return callId;
    }

    public Task RepondreAppelAsync(Peer p, string callId, bool accepter) =>
        reseau.EnvoyerAsync(p, new Paquet { Type = accepter ? "call_accept" : "call_reject", Id = callId, De = prm.Pseudo });

    public Task TerminerAppelAsync(Peer p, string callId) =>
        reseau.EnvoyerAsync(p, new Paquet { Type = "call_end", Id = callId, De = prm.Pseudo });

    public void EnregistrerAppel(AppelInfo info)
    {
        Appels.Insert(0, info);
        ListeChangee?.Invoke();
    }

    public void SupprimerAppel(AppelInfo info)
    {
        if (Appels.Remove(info)) ListeChangee?.Invoke();
    }

    // ---------- Réception des paquets ----------
    void Traiter(Peer p, Paquet pk)
    {
        if (p.Bloque) return;

        var ciblePeer = !string.IsNullOrEmpty(pk.GroupeId)
            ? reseau.Peers.FirstOrDefault(x => x.EstGroupe && x.Id == pk.GroupeId) ?? p
            : p;

        var cible = ciblePeer.Messages.FirstOrDefault(m => m.Id == pk.Id);

        switch (pk.Type)
        {
            case "msg":
            case "group_msg":
            case "file":
            case "voice":
            case "poll":
                {
                    var m = new ChatMessage
                    {
                        Id = pk.Id,
                        EstMoi = false,
                        Auteur = pk.De,
                        Texte = pk.Texte,
                        Reponse = pk.Reponse,
                        ReponseDe = pk.ReponseDe,
                        Transfere = pk.Transfere,
                        Date = pk.Date,
                        Heure = pk.Date.ToString("HH:mm"),
                        AttenteLecture = true
                    };
                    if (pk.Type == "file") FichierService.Recevoir(m, pk);
                    else if (pk.Type == "voice") FichierService.RecevoirVocal(m, pk);
                    else if (pk.Type == "poll")
                    {
                        m.EstSondage = true;
                        m.QuestionSondage = pk.Question;
                        foreach (var opt in pk.Options) m.OptionsSondage.Add(new OptionSondage { Texte = opt });
                        m.RafraichirSondage(prm.Pseudo);
                    }

                    var apercu = pk.Type switch
                    {
                        "file" => $"{FichierService.IconeMedia(m.TypeMedia)} {pk.NomFichier}",
                        "voice" => $"🎤 Message vocal ({m.DureeVocaleTexte})",
                        "poll" => $"📊 {pk.Question}",
                        _ => pk.Texte
                    };
                    if (ciblePeer.EstGroupe) apercu = $"{pk.De} : {apercu}";

                    Ajouter(ciblePeer, m, apercu);
                    _ = reseau.EnvoyerAsync(p, new Paquet { Type = "ack", Id = pk.Id, De = prm.Pseudo, GroupeId = pk.GroupeId });

                    if (Selection == ciblePeer && FenetreActive()) EnvoyerLus(ciblePeer);
                    else
                    {
                        ciblePeer.NonLus++;
                        if (!ciblePeer.Muet && prm.NotifSon) try { SystemSounds.Asterisk.Play(); } catch { }
                        Notification?.Invoke($"{ciblePeer.Nom} : {apercu}");
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
            case "pin":
                if (cible != null)
                {
                    foreach (var item in ciblePeer.Messages) item.Epingle = false;
                    cible.Epingle = pk.Epingle;
                    ciblePeer.MessageEpingleId = pk.Epingle ? cible.Id : "";
                    ciblePeer.MessageEpingleTexte = pk.Epingle ? cible.TexteAffiche : "";
                }
                break;
            case "poll_vote":
                if (cible is { EstSondage: true })
                {
                    var opt = cible.OptionsSondage.FirstOrDefault(o => o.Id == pk.OptionId)
                              ?? cible.OptionsSondage.FirstOrDefault(o => o.Texte == pk.Texte);
                    if (opt != null)
                    {
                        if (opt.Votants.Contains(pk.De)) opt.Votants.Remove(pk.De);
                        else opt.Votants.Add(pk.De);
                        cible.RafraichirSondage(prm.Pseudo);
                    }
                }
                break;
            case "typing":
                if (!prm.IndicateurSaisie) break;
                p.Ecrit = true;
                var jeton = ++p.JetonEcrit;
                _ = Task.Delay(2500).ContinueWith(_ => ui.Invoke(() => { if (p.JetonEcrit == jeton) p.Ecrit = false; }));
                break;
            case "status_update":
                if (!string.IsNullOrEmpty(pk.Bio)) p.Bio = pk.Bio;
                if (!string.IsNullOrEmpty(pk.Avatar)) p.AvatarBase64 = pk.Avatar;
                break;
            case "group_invite":
                {
                    var exist = reseau.Peers.FirstOrDefault(x => x.EstGroupe && x.Id == pk.GroupeId);
                    if (exist == null)
                    {
                        exist = new Peer
                        {
                            Id = pk.GroupeId,
                            Nom = pk.GroupeNom,
                            EstGroupe = true,
                            Apercu = $"Créé par {pk.De}",
                            Derniere = DateTime.Now
                        };
                        foreach (var m in pk.Membres) exist.Membres.Add(m);
                        reseau.Peers.Add(exist);
                    }
                    break;
                }
            case "story_post":
                {
                    var s = new StatutStory
                    {
                        Id = pk.Id,
                        Auteur = pk.De,
                        Texte = pk.Texte,
                        CouleurFond = string.IsNullOrEmpty(pk.Couleur) ? "#00A884" : pk.Couleur,
                        ImageBase64 = pk.Data,
                        Date = pk.Date
                    };
                    Statuts.Insert(0, s);
                    Notification?.Invoke($"Nouveau statut de {pk.De}");
                    break;
                }
            case "story_delete":
                {
                    var existant = Statuts.FirstOrDefault(x => x.Id == pk.Id);
                    if (existant != null) Statuts.Remove(existant);
                    break;
                }
            case "call_offer":
                AppelEntrant?.Invoke(p, pk.Id, string.IsNullOrEmpty(pk.TypeAppel) ? "Audio" : pk.TypeAppel);
                break;
            case "call_accept":
                AppelReponse?.Invoke(p, pk.Id, true);
                break;
            case "call_reject":
                AppelReponse?.Invoke(p, pk.Id, false);
                break;
            case "call_end":
                AppelTermine?.Invoke(p, pk.Id);
                break;
        }
        ListeChangee?.Invoke();
    }

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
            _ = Task.Delay(60_000).ContinueWith(_ => ui.Invoke(() => { if (p.Messages.Contains(m)) p.Messages.Remove(m); }));
    }

    static void Effacer(ChatMessage m)
    {
        m.Texte = "";
        m.Supprime = true;
        m.Image = null;
        m.NomFichier = "";
        m.Reponse = "";
        m.Reaction = "";
        m.Fichier = null;
        m.EstVocal = false;
        m.EstSondage = false;
    }
}
