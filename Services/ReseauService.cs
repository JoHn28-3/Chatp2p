using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Threading;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public class ReseauService
{
    public const int Port = 5000;
    readonly Dispatcher ui;
    readonly Parametres prm;

    public ObservableCollection<Peer> Peers { get; } = new();
    public event Action<Peer, Paquet>? PaquetRecu;
    public event Action<Peer>? PeerConnecte;

    public ReseauService(Dispatcher ui, Parametres prm)
    {
        this.ui = ui;
        this.prm = prm;
    }

    public bool Demarrer(out string? erreur)
    {
        erreur = null;
        TcpListener l;
        try { l = new TcpListener(IPAddress.Any, Port); l.Start(); }
        catch (Exception ex) { erreur = ex.Message; return false; }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await l.AcceptTcpClientAsync();
                _ = GererConnexion(client);
            }
        });
        return true;
    }

    public async Task ConnecterAsync(string ip)
    {
        var client = new TcpClient();
        await client.ConnectAsync(ip, Port);
        _ = GererConnexion(client);
    }

    public async Task EnvoyerAsync(Peer p, Paquet pk)
    {
        if (p.Writer == null) return;
        await p.Verrou.WaitAsync();
        try { await p.Writer.WriteLineAsync(JsonSerializer.Serialize(pk)); }
        catch { p.EnLigne = false; }
        finally { p.Verrou.Release(); }
    }

    Peer ObtenirPeer(string nom)
    {
        var p = Peers.FirstOrDefault(x => x.Nom == nom);
        if (p == null) { p = new Peer { Nom = nom }; Peers.Add(p); }
        return p;
    }

    async Task GererConnexion(TcpClient client)
    {
        Peer? peer = null;
        try
        {
            var flux = client.GetStream();
            var lecteur = new StreamReader(flux);
            var ecrivain = new StreamWriter(flux) { AutoFlush = true };
            await ecrivain.WriteLineAsync(JsonSerializer.Serialize(new Paquet { Type = "hello", De = prm.Pseudo }));

            string? ligne;
            while ((ligne = await lecteur.ReadLineAsync()) != null)
            {
                var pk = JsonSerializer.Deserialize<Paquet>(ligne);
                if (pk == null) continue;

                if (pk.Type == "hello")
                {
                    await ui.InvokeAsync(() =>
                    {
                        var np = ObtenirPeer(pk.De);
                        np.Writer = ecrivain;
                        np.EnLigne = true;
                        peer = np;
                        PeerConnecte?.Invoke(np);
                    });
                    continue;
                }
                if (peer == null) continue;
                var courant = peer;
                await ui.InvokeAsync(() => PaquetRecu?.Invoke(courant, pk));
            }
        }
        catch { /* connexion coupée */ }

        await ui.InvokeAsync(() =>
        {
            if (peer != null) { peer.EnLigne = false; peer.Writer = null; }
        });
    }
}