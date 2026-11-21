using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Chatp2pWPF.Models;
using Chatp2pWPF.Services;
using Chatp2pWPF.Views;
using Microsoft.Win32;

namespace Chatp2pWPF;

public partial class MainWindow : Window
{
    static readonly string[] Emojis =
    {
        "😀","😃","😄","😁","😆","😅","😂","🤣","😊","😇","🙂","😉","😍","🥰","😘","😋",
        "😎","🤩","🥳","😏","😒","😞","😢","😭","😤","😡","🤯","😱","🤔","🤗","🙄","😴",
        "👍","👎","👏","🙌","🙏","💪","👋","✌️","🤝","👌","❤️","💔","💯","🔥","✨","🎉",
        "🎂","🎁","☕","🍕","🍔","⚽","🎵","📷","💻","📱","🚀","🌍","☀️","🌙","⭐","✅"
    };

    readonly Parametres prm = StockageService.ChargerParametres();
    readonly ReseauService reseau;
    readonly ChatService chat;
    readonly AudioService audio = new();
    readonly ICollectionView vue;
    readonly DispatcherTimer minuterieSauvegarde = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly DispatcherTimer timerRec = new() { Interval = TimeSpan.FromSeconds(1) };

    Peer? selection;
    ChatMessage? reponse, modification;
    bool voirArchives;
    string onglet = "chats"; // chats, appels, statuts
    string filtre = "toutes"; // toutes, nonlus, favoris, groupes
    int secRec;
    AppelWindow? appelOuvert;

    public MainWindow()
    {
        InitializeComponent();

        reseau = new ReseauService(Dispatcher, prm);
        chat = new ChatService(reseau, prm, Dispatcher) { FenetreActive = () => IsActive };

        foreach (var p in StockageService.ChargerDiscussions(prm.Pseudo)) reseau.Peers.Add(p);
        foreach (var a in StockageService.ChargerAppels()) chat.Appels.Add(a);
        foreach (var s in StockageService.ChargerStatuts()) chat.Statuts.Add(s);

        lstChats.ItemsSource = reseau.Peers;
        lstAppels.ItemsSource = chat.Appels;
        lstStatuts.ItemsSource = chat.Statuts;

        vue = CollectionViewSource.GetDefaultView(reseau.Peers);
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Epingle), ListSortDirection.Descending));
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Derniere), ListSortDirection.Descending));
        vue.Filter = FiltreChats;

        minuterieSauvegarde.Tick += (_, _) => { minuterieSauvegarde.Stop(); Sauver(); };
        timerRec.Tick += (_, _) =>
        {
            secRec++;
            lblRecTemps.Text = $"🔴 Enregistrement… {TimeSpan.FromSeconds(secRec):mm\\:ss}";
        };

        chat.ListeChangee += () => { vue.Refresh(); MajTitre(); MajMediasPartages(); PlanifierSauvegarde(); };
        chat.MessageAjoute += (p, m) => { if (p == selection) DefilerEnBas(); };
        chat.Notification += msg => NotifFenetre(msg);
        chat.AppelEntrant += SurAppelEntrant;
        chat.AppelReponse += (_, id, ok) => { if (appelOuvert?.CallId == id) appelOuvert.SurReponseDistante(ok); };
        chat.AppelTermine += (_, id) => { if (appelOuvert?.CallId == id) appelOuvert.SurFinDistante(); };

        reseau.PeerConnecte += p => { if (selection == null) Selectionner(p); vue.Refresh(); };

        ThemeService.Appliquer(Resources, prm.Sombre);
        AppliquerFond(prm.Fond);
        MajMonProfil();
        MajTitre();
        lblMonReseau.Text = $"Votre IP : {MesIp()}  ·  Port : {prm.Port}";
        txtPort.Text = prm.Port.ToString();

        Activated += (_, _) =>
        {
            if (selection != null) { selection.NonLus = 0; chat.EnvoyerLus(selection); MajTitre(); }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { BasculerRechercheChat(); e.Handled = true; }
        };
        Closing += (_, _) => { audio.ArreterLecture(); audio.AnnulerEnregistrement(); reseau.Arreter(); Sauver(); };
        Loaded += async (_, _) =>
        {
            if (!reseau.Demarrer(out var err))
                MessageBox.Show($"Impossible d'écouter sur le port {prm.Port} : {err}");

            reseau.EcouterDiscovery(SurPairDecouvert);
            _ = reseau.DiscoveryLanAsync();

            foreach (var ip in reseau.Peers.Where(p => !p.EstGroupe && p.Ip != "").Select(p => (p.Ip, p.Port)).Distinct().ToList())
            {
                try { await reseau.ConnecterAsync(ip.Ip, ip.Port); } catch { }
            }
        };
    }

    // ---------- Filtres et Sauvegarde ----------
    bool FiltreChats(object o)
    {
        if (o is not Peer p) return false;
        if (p.Archive != voirArchives) return false;
        if (filtre == "nonlus" && p.NonLus <= 0) return false;
        if (filtre == "favoris" && !p.Favori) return false;
        if (filtre == "groupes" && !p.EstGroupe) return false;
        var t = txtRecherche.Text;
        return t == "" || p.Nom.Contains(t, StringComparison.OrdinalIgnoreCase) || p.Apercu.Contains(t, StringComparison.OrdinalIgnoreCase);
    }

    void Sauver()
    {
        StockageService.SauverDiscussions(reseau.Peers);
        StockageService.SauverAppels(chat.Appels);
        StockageService.SauverStatuts(chat.Statuts);
        StockageService.SauverParametres(prm);
    }

    void PlanifierSauvegarde() { minuterieSauvegarde.Stop(); minuterieSauvegarde.Start(); }

    void MajTitre()
    {
        var n = reseau.Peers.Sum(p => p.NonLus);
        Title = n > 0 ? $"ChatP2P Desktop ({n})" : "ChatP2P Desktop";
    }

    void NotifFenetre(string msg)
    {
        if (!prm.NotifBureau || IsActive) return;
        Title = $"🔔 {msg}";
    }

    void DefilerEnBas() => Dispatcher.BeginInvoke(() => scroll.ScrollToEnd(), DispatcherPriority.Background);

    void AppliquerFond(string chemin)
    {
        if (chemin != "" && File.Exists(chemin))
            scroll.Background = new ImageBrush(new BitmapImage(new Uri(chemin))) { Stretch = Stretch.UniformToFill };
        else
            scroll.SetResourceReference(BackgroundProperty, "FondApp");
    }

    void MajMonProfil()
    {
        lblMonInitiale.Text = prm.Pseudo.Length > 0 ? prm.Pseudo[0].ToString().ToUpper() : "M";
        if (!string.IsNullOrEmpty(prm.AvatarBase64))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(Convert.FromBase64String(prm.AvatarBase64));
                bmp.EndInit();
                bmp.Freeze();
                brushMonAvatar.ImageSource = bmp;
                photoMonAvatar.Visibility = Visibility.Visible;
                lblMonInitiale.Visibility = Visibility.Collapsed;
                return;
            }
            catch { }
        }
        photoMonAvatar.Visibility = Visibility.Collapsed;
        lblMonInitiale.Visibility = Visibility.Visible;
    }

    void MajMediasPartages()
    {
        if (selection == null) { lstMediasPartages.ItemsSource = null; return; }
        lstMediasPartages.ItemsSource = selection.Messages
            .Where(m => !m.Supprime && m.Fichier != null && File.Exists(m.Fichier))
            .Select(m => m)
            .Reverse()
            .ToList();
        lstMediasPartages.DisplayMemberPath = nameof(ChatMessage.NomFichier);
    }

    // ---------- Utilitaires dialogues ----------
    Window Fenetre(string titre, double largeur, UIElement contenu) => new()
    {
        Title = titre, Width = largeur, SizeToContent = SizeToContent.Height,
        Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
        Background = (Brush)Resources["FondPanneau"], Foreground = (Brush)Resources["Texte"], Content = contenu
    };

    TextBox ChampDialogue(string texte) => new()
    {
        Text = texte, Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(8),
        Background = (Brush)Resources["FondEntete"], Foreground = (Brush)Resources["Texte"],
        CaretBrush = (Brush)Resources["Texte"], BorderThickness = new Thickness(0)
    };

    CheckBox CaseDialogue(string texte, bool val) => new()
    {
        Content = texte, IsChecked = val, Margin = new Thickness(0, 0, 0, 8), Foreground = (Brush)Resources["Texte"]
    };

    string? Demander(string titre, string label, string defaut)
    {
        var txt = ChampDialogue(defaut);
        var ok = new Button { Content = "OK", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre(titre, 350, new StackPanel { Margin = new Thickness(16), Children = { new TextBlock { Text = label }, txt, ok } });
        string? res = null;
        ok.Click += (_, _) => { res = txt.Text.Trim(); fen.Close(); };
        fen.Loaded += (_, _) => { txt.Focus(); txt.SelectAll(); };
        fen.ShowDialog();
        return string.IsNullOrEmpty(res) ? null : res;
    }

    static string MesIp()
    {
        try
        {
            var ips = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Select(a => a.ToString()).ToList();
            return ips.Count > 0 ? string.Join(", ", ips) : "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    // ---------- Navigation Rail ----------
    void AfficherOnglet(string nom)
    {
        onglet = nom;
        voirArchives = false;
        lstChats.Visibility = nom == "chats" ? Visibility.Visible : Visibility.Collapsed;
        lstAppels.Visibility = nom == "appels" ? Visibility.Visible : Visibility.Collapsed;
        pnlStatuts.Visibility = nom == "statuts" ? Visibility.Visible : Visibility.Collapsed;
        pnlFiltres.Visibility = nom == "chats" ? Visibility.Visible : Visibility.Collapsed;
        lblTitre.Text = nom == "appels" ? "Appels" : nom == "statuts" ? "Statuts" : "Discussions";

        MajStyleBoutonRail(btnNavChats, nom == "chats");
        MajStyleBoutonRail(btnNavAppels, nom == "appels");
        MajStyleBoutonRail(btnNavStatuts, nom == "statuts");
        MajStyleBoutonRail(btnNavArchives, false);
        vue.Refresh();
    }

    void MajStyleBoutonRail(Button btn, bool actif)
    {
        if (actif)
        {
            btn.SetResourceReference(BackgroundProperty, "Accent");
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.SetResourceReference(ForegroundProperty, "Gris");
        }
    }

    void NavChats_Click(object s, RoutedEventArgs e) => AfficherOnglet("chats");
    void NavAppels_Click(object s, RoutedEventArgs e) => AfficherOnglet("appels");
    void NavStatuts_Click(object s, RoutedEventArgs e) => AfficherOnglet("statuts");

    void BtnArchives_Click(object s, RoutedEventArgs e)
    {
        if (onglet != "chats") AfficherOnglet("chats");
        voirArchives = !voirArchives;
        lblTitre.Text = voirArchives ? "Archivées" : "Discussions";
        MajStyleBoutonRail(btnNavArchives, voirArchives);
        MajStyleBoutonRail(btnNavChats, !voirArchives);
        vue.Refresh();
    }

    void BtnTheme_Click(object s, RoutedEventArgs e)
    {
        prm.Sombre = !prm.Sombre;
        ThemeService.Appliquer(Resources, prm.Sombre);
        AppliquerFond(prm.Fond);
        StockageService.SauverParametres(prm);
    }

    void FiltreChip_Click(object s, RoutedEventArgs e)
    {
        filtre = chipNonLus.IsChecked == true ? "nonlus" :
                 chipFavoris.IsChecked == true ? "favoris" :
                 chipGroupes.IsChecked == true ? "groupes" : "toutes";
        vue.Refresh();
    }

    // ---------- Connexion & Découverte LAN ----------
    async void BtnNouveau_Click(object sender, RoutedEventArgs e)
    {
        var ip = Demander("Nouvelle discussion", "Adresse IP du contact (ex: 192.168.1.25 ou 127.0.0.1) :", "127.0.0.1");
        if (ip == null) return;
        try { await reseau.ConnecterAsync(ip, prm.Port); }
        catch (Exception ex) { MessageBox.Show("Connexion impossible : " + ex.Message); }
    }

    async void BtnConnecter_Click(object sender, RoutedEventArgs e)
    {
        var ip = txtIP.Text.Trim();
        if (ip == "") return;
        if (!int.TryParse(txtPort.Text.Trim(), out var port)) port = prm.Port;
        try { await reseau.ConnecterAsync(ip, port); }
        catch (Exception ex) { MessageBox.Show($"Connexion impossible à {ip}:{port} : " + ex.Message); }
    }

    void BtnDiscovery_Click(object sender, RoutedEventArgs e)
    {
        _ = reseau.DiscoveryLanAsync();
        MessageBox.Show("Signal de découverte envoyé sur le réseau local (UDP 5001).\nLes appareils avec ChatP2P ouvert seront détectés automatiquement.",
            "Découverte LAN");
    }

    void SurPairDecouvert(string nom, string ip, int port)
    {
        if (nom == prm.Pseudo) return;
        if (reseau.Peers.Any(p => !p.EstGroupe && p.Nom == nom && p.EnLigne)) return;
        if (MessageBox.Show($"Appareil détecté : {nom} ({ip}:{port})\nVoulez-vous vous connecter ?",
            "Découverte LAN", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _ = reseau.ConnecterAsync(ip, port);
        }
    }

    // ---------- Groupes ----------
    async void BtnGroupe_Click(object sender, RoutedEventArgs e)
    {
        var contacts = reseau.Peers.Where(p => !p.EstGroupe).ToList();
        if (contacts.Count == 0)
        {
            MessageBox.Show("Connectez d'abord au moins un contact avant de créer un groupe.", "Nouveau groupe");
            return;
        }
        var txtNom = ChampDialogue("Mon groupe");
        var cases = contacts.Select(c => new CheckBox
        {
            Content = $"{c.Nom} ({(c.EnLigne ? "en ligne" : "hors ligne")})",
            Tag = c, IsChecked = c.EnLigne, Margin = new Thickness(0, 3, 0, 3), Foreground = (Brush)Resources["Texte"]
        }).ToList();
        var pnl = new StackPanel();
        foreach (var cb in cases) pnl.Children.Add(cb);
        var ok = new Button { Content = "Créer le groupe", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var fen = Fenetre("Créer un groupe", 360, new StackPanel
        {
            Margin = new Thickness(16),
            Children = { new TextBlock { Text = "Nom du groupe :" }, txtNom, new TextBlock { Text = "Sélectionner les participants :" }, new ScrollViewer { MaxHeight = 180, Content = pnl }, ok }
        });
        bool valider = false;
        ok.Click += (_, _) => { valider = true; fen.Close(); };
        fen.ShowDialog();
        if (!valider || txtNom.Text.Trim() == "") return;
        var choisis = cases.Where(c => c.IsChecked == true).Select(c => (Peer)c.Tag).ToList();
        if (choisis.Count == 0) return;
        var grp = await chat.CreerGroupeAsync(txtNom.Text.Trim(), choisis);
        AfficherOnglet("chats");
        Selectionner(grp);
    }

    // ---------- Statuts / Stories 24h ----------
    async void BtnPublierStatut_Click(object sender, RoutedEventArgs e)
    {
        var txt = ChampDialogue("");
        txt.AcceptsReturn = true;
        txt.Height = 70;
        var couleurs = new[] { "#00A884", "#128C7E", "#34B7F1", "#E91E63", "#7C4DFF", "#FF7043", "#202C33" };
        string couleurChoisie = couleurs[0];
        string? imgChemin = null;
        var lblImg = new TextBlock { Text = "Aucune image", Foreground = (Brush)Resources["Gris"], FontSize = 11.5, Margin = new Thickness(0, 4, 0, 10) };

        var pnlCouleurs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
        foreach (var col in couleurs)
        {
            var b = new Button { Width = 28, Height = 28, Margin = new Thickness(0, 0, 6, 0), Background = (Brush)new BrushConverter().ConvertFromString(col)!, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            b.Click += (_, _) => couleurChoisie = col;
            pnlCouleurs.Children.Add(b);
        }
        var btnImg = new Button { Content = "🖼 Ajouter une image…", Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = HorizontalAlignment.Left };
        btnImg.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
            if (d.ShowDialog() == true) { imgChemin = d.FileName; lblImg.Text = Path.GetFileName(imgChemin); }
        };
        var ok = new Button { Content = "Publier le statut", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre("Publier un statut (visible 24h)", 380, new StackPanel
        {
            Margin = new Thickness(16),
            Children = { new TextBlock { Text = "Votre texte de statut :" }, txt, new TextBlock { Text = "Couleur de fond :" }, pnlCouleurs, btnImg, lblImg, ok }
        });
        bool publier = false;
        ok.Click += (_, _) => { publier = true; fen.Close(); };
        fen.ShowDialog();
        if (!publier || (txt.Text.Trim() == "" && imgChemin == null)) return;
        await chat.PublierStatutAsync(txt.Text.Trim(), couleurChoisie, imgChemin);
    }

    void LstStatuts_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (lstStatuts.SelectedItem is StatutStory st)
        {
            AfficherStatut(st);
            lstStatuts.SelectedItem = null;
        }
    }

    void LstStatuts_Click(object s, MouseButtonEventArgs e)
    {
        if (lstStatuts.SelectedItem is StatutStory st) AfficherStatut(st);
    }

    void AfficherStatut(StatutStory st)
    {
        var contenu = new Grid
        {
            Width = 420, Height = 520,
            Background = (Brush)new BrushConverter().ConvertFromString(st.CouleurFond)!
        };
        if (st.Image != null)
            contenu.Children.Add(new Image { Source = st.Image, Stretch = Stretch.Uniform });
        contenu.Children.Add(new StackPanel
        {
            Margin = new Thickness(16), VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new TextBlock { Text = st.Auteur, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White },
                new TextBlock { Text = st.HeureTexte, FontSize = 12, Foreground = Brushes.WhiteSmoke }
            }
        });
        if (!string.IsNullOrEmpty(st.Texte))
        {
            contenu.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                Padding = new Thickness(16, 12, 16, 12),
                VerticalAlignment = st.Image != null ? VerticalAlignment.Bottom : VerticalAlignment.Center,
                Child = new TextBlock { Text = st.Texte, FontSize = 20, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }
            });
        }
        Fenetre($"Statut de {st.Auteur}", 436, contenu).ShowDialog();
    }

    // ---------- Appels Audio / Vidéo ----------
    void BtnAppelAudio_Click(object s, RoutedEventArgs e) => LancerAppel("Audio");
    void BtnAppelVideo_Click(object s, RoutedEventArgs e) => LancerAppel("Vidéo");

    void BtnRappelerAudio_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not AppelInfo info) return;
        var p = reseau.Peers.FirstOrDefault(x => !x.EstGroupe && x.Nom == info.Nom);
        if (p != null) { Selectionner(p); LancerAppel("Audio"); }
        else MessageBox.Show($"{info.Nom} n'est pas connecté actuellement.");
    }

    void LancerAppel(string typeAppel)
    {
        if (!ChatService.PeutEnvoyer(selection, out var err)) { MessageBox.Show(err); return; }
        if (selection!.EstGroupe) { MessageBox.Show("Les appels P2P sont disponibles en discussion directe 1-à-1."); return; }
        var id = chat.LancerAppel(selection, typeAppel);
        appelOuvert = new AppelWindow(this, selection, chat, id, typeAppel, false);
        appelOuvert.Closed += (_, _) => appelOuvert = null;
        appelOuvert.Show();
    }

    void SurAppelEntrant(Peer p, string callId, string typeAppel)
    {
        appelOuvert?.Close();
        appelOuvert = new AppelWindow(this, p, chat, callId, typeAppel, true);
        appelOuvert.Closed += (_, _) => appelOuvert = null;
        appelOuvert.Show();
    }

    // ---------- Envoi de messages, fichiers, vocaux, sondages, captures ----------
    async void BtnEnvoyer_Click(object sender, RoutedEventArgs e)
    {
        var texte = txtMessage.Text.Trim();
        if (texte == "") return;
        if (!ChatService.PeutEnvoyer(selection, out var erreur)) { MessageBox.Show(erreur); return; }
        var p = selection!;

        if (modification != null)
        {
            var cible = modification;
            AnnulerContexte();
            await chat.ModifierAsync(p, cible, texte);
            return;
        }
        var rep = reponse;
        txtMessage.Clear();
        AnnulerContexte();
        await chat.EnvoyerTexteAsync(p, texte, rep);
    }

    async void TxtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            BtnEnvoyer_Click(sender, e);
        }
        else if (e.Key == Key.Escape) AnnulerContexte();
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsImage())
        {
            e.Handled = true;
            var img = Clipboard.GetImage();
            if (img == null) return;
            var dossier = Path.Combine(Path.GetTempPath(), "ChatP2P");
            Directory.CreateDirectory(dossier);
            var chemin = Path.Combine(dossier, $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(chemin)) enc.Save(fs);
            await EnvoyerFichiersAsync(new[] { chemin });
        }
    }

    void TxtMessage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (selection == null || modification != null || txtMessage.Text == "") return;
        chat.Frappe(selection);
    }

    void BtnJoindre_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Top };
        void Add(string label, Action act)
        {
            var mi = new MenuItem { Header = label, FontSize = 14 };
            mi.Click += (_, _) => act();
            menu.Items.Add(mi);
        }
        Add("🖼 Photo ou Vidéo", async () =>
        {
            var d = new OpenFileDialog { Multiselect = true, Filter = "Médias|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.mp4;*.mov;*.avi;*.mkv|Tous|*.*" };
            if (d.ShowDialog() == true) await EnvoyerFichiersAsync(d.FileNames);
        });
        Add("📄 Document / Fichier", async () =>
        {
            var d = new OpenFileDialog { Multiselect = true };
            if (d.ShowDialog() == true) await EnvoyerFichiersAsync(d.FileNames);
        });
        Add("📊 Créer un sondage", CreerSondage);
        Add("📷 Capture d'écran", CapturerEcranAsync);
        menu.IsOpen = true;
    }

    async Task EnvoyerFichiersAsync(IEnumerable<string> chemins)
    {
        if (!ChatService.PeutEnvoyer(selection, out var erreur)) { MessageBox.Show(erreur); return; }
        var p = selection!;
        foreach (var c in chemins)
        {
            if (!File.Exists(c)) continue;
            if (new FileInfo(c).Length > FichierService.TailleMax)
            {
                MessageBox.Show($"« {Path.GetFileName(c)} » est trop volumineux (max 50 Mo).");
                continue;
            }
            await chat.EnvoyerFichierAsync(p, c);
        }
    }

    async void Fenetre_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] fichiers) await EnvoyerFichiersAsync(fichiers);
    }

    // ---------- Sondages ----------
    async void CreerSondage()
    {
        if (!ChatService.PeutEnvoyer(selection, out var err)) { MessageBox.Show(err); return; }
        var q = ChampDialogue("On se retrouve à quelle heure ?");
        var o1 = ChampDialogue("18h00");
        var o2 = ChampDialogue("19h30");
        var o3 = ChampDialogue("20h30");
        var o4 = ChampDialogue("");
        var ok = new Button { Content = "Envoyer le sondage", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre("Créer un sondage", 360, new StackPanel
        {
            Margin = new Thickness(16),
            Children = { new TextBlock { Text = "Question :" }, q, new TextBlock { Text = "Options (au moins 2) :" }, o1, o2, o3, o4, ok }
        });
        bool envoyer = false;
        ok.Click += (_, _) => { envoyer = true; fen.Close(); };
        fen.ShowDialog();
        if (!envoyer || q.Text.Trim() == "") return;
        var opts = new[] { o1.Text, o2.Text, o3.Text, o4.Text }.Select(x => x.Trim()).Where(x => x != "").ToList();
        if (opts.Count < 2) { MessageBox.Show("Entrez au moins 2 options."); return; }
        await chat.EnvoyerSondageAsync(selection!, q.Text.Trim(), opts);
    }

    void BtnVoterSondage_Click(object sender, RoutedEventArgs e)
    {
        if (selection == null || (sender as FrameworkElement)?.DataContext is not OptionSondage opt) return;
        var msg = selection.Messages.FirstOrDefault(m => m.Id == opt.MessageId);
        if (msg != null) _ = chat.VoterSondageAsync(selection, msg, opt);
    }

    // ---------- Messages vocaux ----------
    void BtnVocal_Click(object sender, RoutedEventArgs e)
    {
        if (!ChatService.PeutEnvoyer(selection, out var err)) { MessageBox.Show(err); return; }
        secRec = 0;
        lblRecTemps.Text = "🔴 Enregistrement… 00:00";
        pnlSaisie.Visibility = Visibility.Collapsed;
        pnlEnregistrement.Visibility = Visibility.Visible;
        audio.DemarrerEnregistrement();
        timerRec.Start();
    }

    void BtnAnnulerVocal_Click(object sender, RoutedEventArgs e)
    {
        timerRec.Stop();
        audio.AnnulerEnregistrement();
        pnlEnregistrement.Visibility = Visibility.Collapsed;
        pnlSaisie.Visibility = Visibility.Visible;
    }

    async void BtnEnvoyerVocal_Click(object sender, RoutedEventArgs e)
    {
        timerRec.Stop();
        pnlEnregistrement.Visibility = Visibility.Collapsed;
        pnlSaisie.Visibility = Visibility.Visible;
        var wav = audio.ArreterEnregistrement(out var duree);
        if (wav != null && selection != null)
            await chat.EnvoyerVocalAsync(selection, wav, duree);
    }

    void BtnLireVocal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ChatMessage { EstVocal: true } m)
            audio.BasculerLecture(m);
    }

    void BtnVitesseVocal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ChatMessage { EstVocal: true } m)
            audio.ChangerVitesse(m);
    }

    // ---------- Capture d'écran P/Invoke ----------
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    async void CapturerEcranAsync()
    {
        if (!ChatService.PeutEnvoyer(selection, out var err)) { MessageBox.Show(err); return; }
        WindowState = WindowState.Minimized;
        await Task.Delay(350);
        try
        {
            var src = PresentationSource.FromVisual(this);
            double dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            int w = (int)(SystemParameters.PrimaryScreenWidth * dpiX);
            int h = (int)(SystemParameters.PrimaryScreenHeight * dpiY);

            var hdcSrc = GetDC(IntPtr.Zero);
            var hdcDst = CreateCompatibleDC(hdcSrc);
            var hBmp = CreateCompatibleBitmap(hdcSrc, w, h);
            var old = SelectObject(hdcDst, hBmp);
            BitBlt(hdcDst, 0, 0, w, h, hdcSrc, 0, 0, 0x00CC0020);
            SelectObject(hdcDst, old);
            DeleteDC(hdcDst);
            ReleaseDC(IntPtr.Zero, hdcSrc);

            var bmpSrc = Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hBmp);

            var dossier = Path.Combine(Path.GetTempPath(), "ChatP2P");
            Directory.CreateDirectory(dossier);
            var chemin = Path.Combine(dossier, $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmpSrc));
            using (var fs = File.Create(chemin)) enc.Save(fs);

            WindowState = WindowState.Normal;
            Activate();
            await EnvoyerFichiersAsync(new[] { chemin });
        }
        catch (Exception ex)
        {
            WindowState = WindowState.Normal;
            MessageBox.Show("Capture impossible : " + ex.Message);
        }
    }

    // ---------- Interactions messages & sélection ----------
    void Bulle_Down(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && (sender as FrameworkElement)?.DataContext is ChatMessage { Fichier: not null } m)
            FichierService.Ouvrir(m.Fichier!);
    }

    void Image_Click(object s, MouseButtonEventArgs e)
    {
        if (MsgDe(s) is { Fichier: not null } m) FichierService.Ouvrir(m.Fichier!);
    }

    void Fichier_Click(object s, MouseButtonEventArgs e)
    {
        if (MsgDe(s) is { Fichier: not null } m) FichierService.Ouvrir(m.Fichier!);
    }

    void LstMediasPartages_Click(object s, MouseButtonEventArgs e)
    {
        if (lstMediasPartages.SelectedItem is ChatMessage { Fichier: not null } m) FichierService.Ouvrir(m.Fichier!);
    }

    void LstChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstChats.SelectedItem is Peer p) Selectionner(p);
    }

    void Selectionner(Peer? p)
    {
        if (p == selection) return;
        FermerRechercheChat();
        selection = p;
        chat.Selection = p;
        icMessages.ItemsSource = p?.Messages;
        entete.DataContext = p;
        bandeauEpingle.DataContext = p;
        pnlInfos.DataContext = p;
        lstChats.SelectedItem = p;
        accueil.Visibility = p == null ? Visibility.Visible : Visibility.Collapsed;
        if (p == null) pnlInfos.Visibility = Visibility.Collapsed;
        AnnulerContexte();
        if (p != null) { p.NonLus = 0; chat.EnvoyerLus(p); MajTitre(); }
        MajMediasPartages();
        DefilerEnBas();
    }

    void AnnulerContexte()
    {
        if (modification != null) txtMessage.Clear();
        reponse = null;
        modification = null;
        bandeau.Visibility = Visibility.Collapsed;
    }

    void BtnAnnulerContexte_Click(object sender, RoutedEventArgs e) => AnnulerContexte();

    // ---------- Panneau infos & recherche interne ----------
    void BtnToggleInfos_Click(object sender, RoutedEventArgs e)
    {
        if (selection == null) return;
        pnlInfos.Visibility = pnlInfos.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        MajMediasPartages();
    }

    void EnteteContact_Click(object sender, MouseButtonEventArgs e) => BtnToggleInfos_Click(sender, e);

    void BasculerRechercheChat()
    {
        if (selection == null) return;
        if (barreRech.Visibility == Visibility.Visible) { FermerRechercheChat(); return; }
        barreRech.Visibility = Visibility.Visible;
        txtRechChat.Focus();
    }

    void FermerRechercheChat()
    {
        barreRech.Visibility = Visibility.Collapsed;
        txtRechChat.Clear();
        if (selection != null) CollectionViewSource.GetDefaultView(selection.Messages).Filter = null;
    }

    void BtnRechChat_Click(object sender, RoutedEventArgs e) => BasculerRechercheChat();
    void BtnFermerRech_Click(object sender, RoutedEventArgs e) => FermerRechercheChat();

    void TxtRechChat_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (selection == null) return;
        var t = txtRechChat.Text;
        CollectionViewSource.GetDefaultView(selection.Messages).Filter = t == "" ? null : o =>
            o is ChatMessage m && (m.Texte.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || m.NomFichier.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || m.QuestionSondage.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Menus discussions & messages ----------
    void BtnMenuChat_Click(object sender, RoutedEventArgs e)
    {
        if (selection == null) return;
        var p = selection;
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Bottom };
        void Ajout(string titre, Action action)
        {
            var it = new MenuItem { Header = titre };
            it.Click += (_, _) => action();
            menu.Items.Add(it);
        }
        Ajout("Infos du contact", () => BtnToggleInfos_Click(sender, e));
        Ajout("Rechercher (Ctrl+F)", BasculerRechercheChat);
        Ajout("Messages importants ★", () => AfficherImportants(p));
        Ajout("Exporter la discussion…", () => Exporter(p));
        menu.Items.Add(new Separator());
        Ajout("Vider la discussion", () => ViderChat(p));
        menu.IsOpen = true;
    }

    void BtnExporter_Click(object s, RoutedEventArgs e) { if (selection != null) Exporter(selection); }
    void BtnViderChat_Click(object s, RoutedEventArgs e) { if (selection != null) ViderChat(selection); }

    void ViderChat(Peer p)
    {
        if (MessageBox.Show($"Supprimer tous les messages avec {p.Nom} sur cet ordinateur ?",
            "Vider la discussion", MessageBoxButton.YesNo) == MessageBoxResult.Yes) chat.Vider(p);
    }

    void Exporter(Peer p)
    {
        var d = new SaveFileDialog { Filter = "Texte|*.txt", FileName = $"Discussion_{p.Nom}.txt" };
        if (d.ShowDialog() != true) return;
        File.WriteAllLines(d.FileName, p.Messages.Select(m =>
            $"[{m.Date:dd/MM/yyyy HH:mm}] {(m.EstMoi ? prm.Pseudo : (m.Auteur != "" ? m.Auteur : p.Nom))} : {(m.HasFichier ? "📎 " + m.NomFichier + " " : "")}{m.TexteAffiche}"));
        MessageBox.Show("Discussion exportée.");
    }

    void BtnImportants_Click(object sender, RoutedEventArgs e) => AfficherImportants(null);

    void AfficherImportants(Peer? seulement)
    {
        var source = seulement != null ? new[] { seulement } : reseau.Peers.ToArray();
        var items = source.SelectMany(p => p.Messages.Where(x => x.Etoile && !x.Supprime).Select(x =>
            $"{p.Nom} · {x.Date:dd/MM/yyyy HH:mm}\n{x.TexteAffiche}")).ToList();
        UIElement contenu = items.Count == 0
            ? new TextBlock { Text = "Aucun message important.\nClic droit sur un message → « Message important ».", Margin = new Thickness(16) }
            : new ListBox
            {
                ItemsSource = items, MaxHeight = 400, MinHeight = 120, Margin = new Thickness(8),
                Background = (Brush)Resources["FondPanneau"], Foreground = (Brush)Resources["Texte"]
            };
        Fenetre("Messages importants ★", 440, contenu).ShowDialog();
    }

    static ChatMessage? MsgDe(object s) => (s as FrameworkElement)?.DataContext as ChatMessage;

    void MenuRepondre_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { Supprime: false } m) return;
        AnnulerContexte();
        reponse = m;
        txtBandeau.Text = "Réponse à : " + m.TexteAffiche;
        bandeau.Visibility = Visibility.Visible;
        txtMessage.Focus();
    }

    void MenuCopier_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m && m.TexteAffiche != "") Clipboard.SetText(m.TexteAffiche);
    }

    void MenuEpingler_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { Supprime: false } m && selection is { } p) _ = chat.EpinglerMessageAsync(p, m);
    }

    void BtnDesepingler_Click(object s, RoutedEventArgs e)
    {
        if (selection is { } p)
        {
            var m = p.Messages.FirstOrDefault(x => x.Id == p.MessageEpingleId);
            if (m != null) _ = chat.EpinglerMessageAsync(p, m);
            else { p.MessageEpingleId = ""; p.MessageEpingleTexte = ""; }
        }
    }

    async void MenuTransferer_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { Supprime: false } m) return;
        var cible = ChoisirContact();
        if (cible == null) return;
        if (!ChatService.PeutEnvoyer(cible, out var err)) { MessageBox.Show(err); return; }
        if (m.Fichier != null && File.Exists(m.Fichier)) await chat.EnvoyerFichierAsync(cible, m.Fichier, true);
        else if (m.Texte != "") await chat.EnvoyerTexteAsync(cible, m.Texte, null, true);
    }

    Peer? ChoisirContact()
    {
        var liste = new ListBox
        {
            ItemsSource = reseau.Peers.ToList(), DisplayMemberPath = nameof(Peer.Nom),
            Height = 220, Margin = new Thickness(0, 6, 0, 12),
            Background = (Brush)Resources["FondEntete"], Foreground = (Brush)Resources["Texte"]
        };
        var ok = new Button { Content = "Transférer", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre("Transférer à…", 320, new StackPanel { Margin = new Thickness(16), Children = { new TextBlock { Text = "Choisissez un contact :" }, liste, ok } });
        Peer? res = null;
        ok.Click += (_, _) => { res = liste.SelectedItem as Peer; fen.Close(); };
        liste.MouseDoubleClick += (_, _) => { res = liste.SelectedItem as Peer; if (res != null) fen.Close(); };
        fen.ShowDialog();
        return res;
    }

    void MenuModifier_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { EstMoi: true, Supprime: false } m) return;
        AnnulerContexte();
        modification = m;
        txtMessage.Text = m.Texte;
        txtBandeau.Text = "Modification du message";
        bandeau.Visibility = Visibility.Visible;
        txtMessage.Focus();
        txtMessage.CaretIndex = txtMessage.Text.Length;
    }

    void MenuReagir_Click(object s, RoutedEventArgs e)
    {
        var item = (MenuItem)s;
        if (item.DataContext is ChatMessage m && selection is { } p)
            _ = chat.ReagirAsync(p, m, item.Header?.ToString() ?? "");
    }

    void MenuEtoile_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m) { m.Etoile = !m.Etoile; PlanifierSauvegarde(); }
    }

    void MenuInfos_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { } m) return;
        var etat = !m.EstMoi ? "Reçu" : m.Lu ? "Lu" : m.Statut == "✓✓" ? "Distribué" : "Envoyé";
        MessageBox.Show($"Date : {m.Date:dddd d MMMM yyyy HH:mm}\nÉtat : {etat}" +
                        (m.Modifie ? "\nModifié" : "") + (m.Transfere ? "\nTransféré" : "") +
                        (m.Epingle ? "\nÉpinglé" : ""), "Infos du message");
    }

    void MenuOuvrirDossier_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { Fichier: not null } m) FichierService.OuvrirDossier(m.Fichier!);
    }

    void MenuSupprMoi_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { } m) return;
        if (prm.ConfirmSuppr && MessageBox.Show("Supprimer ce message pour vous ?", "Supprimer", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        selection?.Messages.Remove(m);
        PlanifierSauvegarde();
    }

    void MenuSupprTous_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { EstMoi: true } m && selection is { } p)
        {
            if (prm.ConfirmSuppr && MessageBox.Show("Supprimer ce message pour tout le monde ?", "Supprimer", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _ = chat.SupprimerPourTousAsync(p, m);
        }
    }

    void ChatMenu_Click(object s, RoutedEventArgs e) { vue.Refresh(); PlanifierSauvegarde(); }

    void ChatSuppr_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Peer p) return;
        if (prm.ConfirmSuppr && MessageBox.Show($"Supprimer la discussion avec {p.Nom} ?", "Supprimer", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        if (p == selection) Selectionner(null);
        reseau.Peers.Remove(p);
        MajTitre();
        PlanifierSauvegarde();
    }

    void TxtRecherche_TextChanged(object s, TextChangedEventArgs e) => vue?.Refresh();

    // ---------- Emojis ----------
    void BtnEmoji_Click(object s, RoutedEventArgs e)
    {
        var grille = new WrapPanel { Width = 336 };
        var popup = new Popup
        {
            PlacementTarget = (UIElement)s, Placement = PlacementMode.Top, StaysOpen = false, AllowsTransparency = true,
            Child = new Border
            {
                Background = (Brush)Resources["FondPanneau"], BorderBrush = (Brush)Resources["Separateur"],
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = grille
            }
        };
        foreach (var em in Emojis)
        {
            var b = new Button { Content = em, FontSize = 21, Width = 40, Height = 40, Style = (Style)FindResource("BtnIcone") };
            b.Click += (_, _) =>
            {
                txtMessage.Text += em;
                txtMessage.CaretIndex = txtMessage.Text.Length;
                txtMessage.Focus();
            };
            grille.Children.Add(b);
        }
        popup.IsOpen = true;
    }

    // ---------- Réglages complets ----------
    void BtnReglages_Click(object s, RoutedEventArgs e)
    {
        var txtNom = ChampDialogue(prm.Pseudo);
        var txtBio = ChampDialogue(prm.Bio);
        var txtPortReg = ChampDialogue(prm.Port.ToString());
        var cbSombre = CaseDialogue("Thème sombre WhatsApp", prm.Sombre);
        var cbSon = CaseDialogue("Notifications sonores", prm.NotifSon);
        var cbBureau = CaseDialogue("Notifications dans le titre de la fenêtre", prm.NotifBureau);
        var cbLu = CaseDialogue("Envoyer les accusés de lecture (coches bleues)", prm.AccusesLecture);
        var cbSaisie = CaseDialogue("Indicateur « en train d'écrire »", prm.IndicateurSaisie);
        var cbConfirm = CaseDialogue("Confirmation avant suppression", prm.ConfirmSuppr);

        var avatar = prm.AvatarBase64;
        var fond = prm.Fond;

        var btnAvatar = new Button { Content = "Changer ma photo de profil…", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 4, 0, 6) };
        var btnFond = new Button { Content = "Choisir un fond d'écran de discussion…", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 6) };
        var btnSansFond = new Button { Content = "Retirer le fond d'écran", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 14) };
        var btnOk = new Button { Content = "Enregistrer", IsDefault = true, Style = (Style)FindResource("BtnAction"), HorizontalAlignment = HorizontalAlignment.Right };

        btnAvatar.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
            if (d.ShowDialog() == true) avatar = Convert.ToBase64String(File.ReadAllBytes(d.FileName));
        };
        btnFond.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
            if (d.ShowDialog() == true) fond = d.FileName;
        };
        btnSansFond.Click += (_, _) => fond = "";

        var fen = Fenetre("Réglages — ChatP2P Desktop", 400, new ScrollViewer
        {
            MaxHeight = 600, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    new TextBlock { Text = "Mon nom", FontWeight = FontWeights.SemiBold }, txtNom,
                    new TextBlock { Text = "Ma bio / actu", FontWeight = FontWeights.SemiBold }, txtBio,
                    btnAvatar,
                    cbSombre, cbSon, cbBureau, cbLu, cbSaisie, cbConfirm,
                    new TextBlock { Text = "Mon IP locale (à partager aux contacts) :", Margin = new Thickness(0, 4, 0, 2) },
                    new TextBox { Text = MesIp(), IsReadOnly = true, Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 8),
                                  Background = (Brush)Resources["FondEntete"], Foreground = (Brush)Resources["Texte"], BorderThickness = new Thickness(0) },
                    new TextBlock { Text = "Port TCP d'écoute (redémarrage requis si modifié) :" }, txtPortReg,
                    btnFond, btnSansFond, btnOk
                }
            }
        });

        btnOk.Click += async (_, _) =>
        {
            if (txtNom.Text.Trim() != "") prm.Pseudo = txtNom.Text.Trim();
            prm.Bio = txtBio.Text.Trim();
            prm.AvatarBase64 = avatar;
            prm.Sombre = cbSombre.IsChecked == true;
            prm.NotifSon = cbSon.IsChecked == true;
            prm.NotifBureau = cbBureau.IsChecked == true;
            prm.AccusesLecture = cbLu.IsChecked == true;
            prm.IndicateurSaisie = cbSaisie.IsChecked == true;
            prm.ConfirmSuppr = cbConfirm.IsChecked == true;
            if (int.TryParse(txtPortReg.Text, out var pt) && pt > 1024 && pt < 65535) prm.Port = pt;
            prm.Fond = fond;

            ThemeService.Appliquer(Resources, prm.Sombre);
            AppliquerFond(prm.Fond);
            MajMonProfil();
            StockageService.SauverParametres(prm);
            await reseau.DiffuserProfilAsync();
            fen.Close();
        };
        fen.ShowDialog();
    }
}
