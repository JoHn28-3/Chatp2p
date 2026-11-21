using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public class ReseauService
{
    readonly Dispatcher ui;
    readonly Parametres prm;
    TcpListener? listener;

    public ObservableCollection<Peer> Peers { get; } = new();
    public event Action<Peer, Paquet>? PaquetRecu;
    public event Action<Peer>? PeerConnecte;
    public event Action<Peer>? PeerDeconnecte;

    public ReseauService(Dispatcher ui, Parametres prm)
    {
        this.ui = ui;
        this.prm = prm;
    }

    public bool Demarrer(out string? erreur)
    {
        erreur = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, prm.Port);
            listener.Start();
        }
        catch (Exception ex) { erreur = ex.Message; return false; }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    client.NoDelay = true;
                    _ = GererConnexion(client);
                }
                catch { break; }
            }
        });
        return true;
    }

    public void Arreter()
    {
        try { listener?.Stop(); } catch { }
    }

    public async Task ConnecterAsync(string ip, int? port = null)
    {
        var client = new TcpClient { NoDelay = true };
        using var delai = new CancellationTokenSource(4000);
        await client.ConnectAsync(ip, port ?? prm.Port, delai.Token);
        _ = GererConnexion(client);
    }

    public async Task EnvoyerAsync(Peer p, Paquet pk)
    {
        if (p.Writer == null) return;
        await p.Verrou.WaitAsync();
        try { await p.Writer.WriteLineAsync(JsonSerializer.Serialize(pk)); }
        catch
        {
            await ui.InvokeAsync(() => { p.EnLigne = false; p.Writer = null; });
        }
        finally { p.Verrou.Release(); }
    }

    public Task DiffuserProfilAsync()
    {
        var pk = new Paquet { Type = "status_update", De = prm.Pseudo, Bio = prm.Bio, Avatar = prm.AvatarBase64 };
        return Task.WhenAll(Peers.Where(p => !p.EstGroupe && p.EnLigne).Select(p => EnvoyerAsync(p, pk)));
    }

    Peer ObtenirPeer(string nom)
    {
        var p = Peers.FirstOrDefault(x => !x.EstGroupe && x.Nom == nom);
        if (p == null) { p = new Peer { Id = nom, Nom = nom }; Peers.Add(p); }
        return p;
    }

    async Task GererConnexion(TcpClient client)
    {
        Peer? peer = null;
        string ipDistante = "";
        int portDistant = prm.Port;
        try
        {
            if (client.Client.RemoteEndPoint is IPEndPoint ep)
            {
                ipDistante = (ep.Address.IsIPv4MappedToIPv6 ? ep.Address.MapToIPv4() : ep.Address).ToString();
                portDistant = ep.Port;
            }
            var flux = client.GetStream();
            var lecteur = new StreamReader(flux, Encoding.UTF8, leaveOpen: true);
            var ecrivain = new StreamWriter(flux, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await ecrivain.WriteLineAsync(JsonSerializer.Serialize(new Paquet
            {
                Type = "hello",
                De = prm.Pseudo,
                Avatar = prm.AvatarBase64,
                Bio = prm.Bio
            }));

            string? ligne;
            while ((ligne = await lecteur.ReadLineAsync()) != null)
            {
                Paquet? pk;
                try { pk = JsonSerializer.Deserialize<Paquet>(ligne); }
                catch { continue; }
                if (pk == null) continue;

                if (pk.Type == "hello")
                {
                    await ui.InvokeAsync(() =>
                    {
                        var np = ObtenirPeer(pk.De);
                        np.Writer = ecrivain;
                        if (ipDistante != "") np.Ip = ipDistante;
                        if (!string.IsNullOrEmpty(pk.Bio)) np.Bio = pk.Bio;
                        if (!string.IsNullOrEmpty(pk.Avatar)) np.AvatarBase64 = pk.Avatar;
                        np.EnLigne = true;
                        peer = np;
                        PeerConnecte?.Invoke(np);
                    });
                    continue;
                }

                if (pk.Type == "ping" && peer != null)
                {
                    await EnvoyerAsync(peer, new Paquet { Type = "pong", De = prm.Pseudo });
                    continue;
                }

                if (peer == null) continue;
                var courant = peer;
                await ui.InvokeAsync(() => PaquetRecu?.Invoke(courant, pk));
            }
        }
        catch { }
        finally { try { client.Close(); } catch { } }

        await ui.InvokeAsync(() =>
        {
            if (peer != null) { peer.EnLigne = false; peer.Writer = null; PeerDeconnecte?.Invoke(peer); }
        });
    }

    public async Task DiscoveryLanAsync()
    {
        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            var msg = Encoding.UTF8.GetBytes($"CHATP2P_HELLO|{prm.Pseudo}|{prm.Port}");
            await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Broadcast, 5001));
        }
        catch { }
    }

    public void EcouterDiscovery(Action<string, string, int> onPairTrouve)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var udp = new UdpClient(5001);
                while (true)
                {
                    var res = await udp.ReceiveAsync();
                    var parts = Encoding.UTF8.GetString(res.Buffer).Split('|');
                    if (parts.Length == 3 && parts[0] == "CHATP2P_HELLO")
                    {
                        var nom = parts[1];
                        var ip = (res.RemoteEndPoint.Address.IsIPv4MappedToIPv6
                            ? res.RemoteEndPoint.Address.MapToIPv4()
                            : res.RemoteEndPoint.Address).ToString();
                        if (!int.TryParse(parts[2], out var p)) p = 5000;
                        await ui.InvokeAsync(() => onPairTrouve(nom, ip, p));
                    }
                }
            }
            catch { }
        });
    }
}
