using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Chatp2pWPF.Models;
using Chatp2pWPF.Services;
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
    readonly ICollectionView vue;
    readonly DispatcherTimer minuterieSauvegarde = new() { Interval = TimeSpan.FromSeconds(2) };
    Peer? selection;
    ChatMessage? reponse, modification;
    bool voirArchives, soloNonLus;

    public MainWindow()
    {
        InitializeComponent();

        reseau = new ReseauService(Dispatcher, prm);
        chat = new ChatService(reseau, prm, Dispatcher) { FenetreActive = () => IsActive };

        foreach (var p in StockageService.ChargerDiscussions()) reseau.Peers.Add(p);

        lstChats.ItemsSource = reseau.Peers;
        vue = CollectionViewSource.GetDefaultView(reseau.Peers);
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Epingle), ListSortDirection.Descending));
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Derniere), ListSortDirection.Descending));
        vue.Filter = o => o is Peer p && p.Archive == voirArchives
            && (!soloNonLus || p.NonLus > 0)
            && (txtRecherche.Text == "" || p.Nom.Contains(txtRecherche.Text, StringComparison.OrdinalIgnoreCase));

        minuterieSauvegarde.Tick += (_, _) => { minuterieSauvegarde.Stop(); Sauver(); };
        chat.ListeChangee += () => { vue.Refresh(); MajTitre(); PlanifierSauvegarde(); };
        chat.MessageAjoute += (p, m) => { if (p == selection) DefilerEnBas(); };
        reseau.PeerConnecte += p => { if (selection == null) Selectionner(p); vue.Refresh(); };

        ThemeService.Appliquer(Resources, prm.Sombre);
        AppliquerFond(prm.Fond);
        MajTitre();

        Activated += (s, e) =>
        {
            if (selection != null) { selection.NonLus = 0; chat.EnvoyerLus(selection); MajTitre(); }
        };
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { BasculerRechercheChat(); e.Handled = true; }
        };
        Closing += (s, e) => Sauver();
        Loaded += async (s, e) =>
        {
            if (!reseau.Demarrer(out var err))
                MessageBox.Show("Impossible d'écouter sur le port 5000 : " + err);
            // Reconnexion automatique aux contacts connus
            foreach (var ip in reseau.Peers.Select(p => p.Ip).Where(i => i != "").Distinct().ToList())
            {
                try { await reseau.ConnecterAsync(ip); } catch { }
            }
        };
    }

    // ---------- Utilitaires ----------
    void Sauver()
    {
        StockageService.SauverDiscussions(reseau.Peers);
        StockageService.SauverParametres(prm);
    }

    void PlanifierSauvegarde() { minuterieSauvegarde.Stop(); minuterieSauvegarde.Start(); }

    void MajTitre()
    {
        var n = reseau.Peers.Sum(p => p.NonLus);
        Title = n > 0 ? $"ChatP2P ({n})" : "ChatP2P";
    }

    void DefilerEnBas() =>
        Dispatcher.BeginInvoke(() => scroll.ScrollToEnd(), DispatcherPriority.Background);

    void AppliquerFond(string chemin)
    {
        if (chemin != "" && File.Exists(chemin))
        {
            var bmp = new BitmapImage(new Uri(chemin));
            scroll.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
        }
        else scroll.SetResourceReference(BackgroundProperty, "FondApp");
    }

    Window Fenetre(string titre, double largeur, UIElement contenu) => new()
    {
        Title = titre,
        Width = largeur,
        SizeToContent = SizeToContent.Height,
        Owner = this,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ResizeMode = ResizeMode.NoResize,
        Background = (Brush)Resources["FondPanneau"],
        Foreground = (Brush)Resources["Texte"],
        Content = contenu
    };

    TextBox ChampDialogue(string texte) => new()
    {
        Text = texte,
        Margin = new Thickness(0, 6, 0, 12),
        Padding = new Thickness(6),
        Background = (Brush)Resources["FondEntete"],
        Foreground = (Brush)Resources["Texte"],
        CaretBrush = (Brush)Resources["Texte"]
    };

    CheckBox CaseDialogue(string texte, bool val) => new()
    {
        Content = texte,
        IsChecked = val,
        Margin = new Thickness(0, 0, 0, 8),
        Foreground = (Brush)Resources["Texte"]
    };

    string? Demander(string titre, string label, string defaut)
    {
        var txt = ChampDialogue(defaut);
        var ok = new Button { Content = "OK", IsDefault = true, Padding = new Thickness(18, 4, 18, 4), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre(titre, 340, new StackPanel
        {
            Margin = new Thickness(16),
            Children = { new TextBlock { Text = label }, txt, ok }
        });
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
            return string.Join(", ", Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()));
        }
        catch { return "inconnue"; }
    }

    // ---------- Connexion ----------
    async void BtnNouveau_Click(object sender, RoutedEventArgs e)
    {
        var ip = Demander("Nouvelle discussion", "Adresse IP du contact (son application doit être ouverte) :", "127.0.0.1");
        if (ip == null) return;
        try { await reseau.ConnecterAsync(ip); }
        catch (Exception ex) { MessageBox.Show("Connexion impossible : " + ex.Message); }
    }

    // ---------- Envoi ----------
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

    async Task EnvoyerFichiersAsync(IEnumerable<string> chemins)
    {
        if (!ChatService.PeutEnvoyer(selection, out var erreur)) { MessageBox.Show(erreur); return; }
        var p = selection!;
        foreach (var c in chemins)
        {
            if (!File.Exists(c)) continue;
            if (new FileInfo(c).Length > FichierService.TailleMax)
            {
                MessageBox.Show($"« {Path.GetFileName(c)} » est trop volumineux (max 20 Mo).");
                continue;
            }
            await chat.EnvoyerFichierAsync(p, c);
        }
    }

    async void BtnFichier_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Multiselect = true };
        if (dlg.ShowDialog() == true) await EnvoyerFichiersAsync(dlg.FileNames);
    }

    async void Fenetre_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] fichiers) await EnvoyerFichiersAsync(fichiers);
    }

    void Bulle_Down(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && (sender as FrameworkElement)?.DataContext is ChatMessage { Fichier: not null } m)
            FichierService.Ouvrir(m.Fichier!);
    }

    // ---------- Sélection et contexte ----------
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
        lstChats.SelectedItem = p;
        accueil.Visibility = p == null ? Visibility.Visible : Visibility.Collapsed;
        AnnulerContexte();
        if (p != null) { p.NonLus = 0; chat.EnvoyerLus(p); MajTitre(); }
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

    // ---------- Recherche dans la discussion ----------
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
                                   || m.NomFichier.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Menu de l'en-tête ----------
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
        Ajout("Rechercher (Ctrl+F)", BasculerRechercheChat);
        Ajout("Infos du contact", () => MessageBox.Show(
            $"Nom : {p.Nom}\nAdresse IP : {(p.Ip == "" ? "inconnue" : p.Ip)}\nStatut : {p.StatutTexte}\nMessages : {p.Messages.Count}",
            "Infos du contact"));
        Ajout("Messages importants", () => AfficherImportants(p));
        Ajout("Exporter la discussion…", () => Exporter(p));
        menu.Items.Add(new Separator());
        Ajout("Vider la discussion", () =>
        {
            if (MessageBox.Show("Supprimer tous les messages de cette discussion (sur cet ordinateur) ?",
                "Vider", MessageBoxButton.YesNo) == MessageBoxResult.Yes) chat.Vider(p);
        });
        menu.IsOpen = true;
    }

    void Exporter(Peer p)
    {
        var d = new SaveFileDialog { Filter = "Texte|*.txt", FileName = $"Discussion_{p.Nom}.txt" };
        if (d.ShowDialog() != true) return;
        File.WriteAllLines(d.FileName, p.Messages.Select(m =>
            $"[{m.Date:dd/MM/yyyy HH:mm}] {(m.EstMoi ? prm.Pseudo : p.Nom)} : {(m.HasFichier ? "📎 " + m.NomFichier + " " : "")}{m.Texte}"));
        MessageBox.Show("Discussion exportée.");
    }

    void BtnImportants_Click(object sender, RoutedEventArgs e) => AfficherImportants(null);

    void AfficherImportants(Peer? seulement)
    {
        var source = seulement != null ? new[] { seulement } : reseau.Peers.ToArray();
        var items = source.SelectMany(p => p.Messages.Where(x => x.Etoile && !x.Supprime).Select(x =>
            $"{p.Nom} · {x.Date:dd/MM/yyyy HH:mm}\n{(x.Texte != "" ? x.Texte : "📎 " + x.NomFichier)}")).ToList();
        UIElement contenu = items.Count == 0
            ? new TextBlock { Text = "Aucun message important.\nClic droit sur un message → « Message important ★ ».", Margin = new Thickness(16) }
            : new ListBox
            {
                ItemsSource = items, MaxHeight = 400, MinHeight = 120, Margin = new Thickness(8),
                Background = (Brush)Resources["FondPanneau"], Foreground = (Brush)Resources["Texte"]
            };
        Fenetre("Messages importants", 440, contenu).ShowDialog();
    }

    // ---------- Menu des messages ----------
    static ChatMessage? MsgDe(object s) => (s as FrameworkElement)?.DataContext as ChatMessage;

    void MenuRepondre_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { } m) return;
        AnnulerContexte();
        reponse = m;
        txtBandeau.Text = "Réponse à : " + (m.Texte != "" ? m.Texte : m.NomFichier);
        bandeau.Visibility = Visibility.Visible;
        txtMessage.Focus();
    }

    void MenuCopier_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m && m.Texte != "") Clipboard.SetText(m.Texte);
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
        var ok = new Button { Content = "Transférer", IsDefault = true, Padding = new Thickness(18, 4, 18, 4), HorizontalAlignment = HorizontalAlignment.Right };
        var fen = Fenetre("Transférer à…", 320, new StackPanel
        {
            Margin = new Thickness(16),
            Children = { new TextBlock { Text = "Choisissez un contact :" }, liste, ok }
        });
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
                        (m.Modifie ? "\nModifié" : "") + (m.Transfere ? "\nTransféré" : ""), "Infos du message");
    }

    void MenuSupprMoi_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m) { selection?.Messages.Remove(m); PlanifierSauvegarde(); }
    }

    void MenuSupprTous_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { EstMoi: true } m && selection is { } p)
            _ = chat.SupprimerPourTousAsync(p, m);
    }

    // ---------- Menu des discussions ----------
    void ChatMenu_Click(object s, RoutedEventArgs e) { vue.Refresh(); PlanifierSauvegarde(); }

    void ChatSuppr_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Peer p) return;
        if (p == selection)
        {
            FermerRechercheChat();
            selection = null;
            chat.Selection = null;
            icMessages.ItemsSource = null;
            entete.DataContext = null;
            accueil.Visibility = Visibility.Visible;
        }
        reseau.Peers.Remove(p);
        MajTitre();
        PlanifierSauvegarde();
    }

    void TxtRecherche_TextChanged(object s, TextChangedEventArgs e) => vue?.Refresh();

    void Chip_Changed(object s, RoutedEventArgs e)
    {
        soloNonLus = chipNonLus.IsChecked == true;
        vue?.Refresh();
    }

    void BtnArchives_Click(object s, RoutedEventArgs e)
    {
        voirArchives = !voirArchives;
        lblTitre.Text = voirArchives ? "Archivées" : "Discussions";
        vue.Refresh();
    }

    // ---------- Emojis ----------
    void BtnEmoji_Click(object s, RoutedEventArgs e)
    {
        var grille = new WrapPanel { Width = 336 };
        var popup = new Popup
        {
            PlacementTarget = (UIElement)s,
            Placement = PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = (Brush)Resources["FondPanneau"],
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Child = grille
            }
        };
        foreach (var em in Emojis)
        {
            var b = new Button { Content = em, FontSize = 22, Width = 40, Height = 40, Style = (Style)FindResource("BtnIcone") };
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

    // ---------- Réglages ----------
    void BtnReglages_Click(object s, RoutedEventArgs e)
    {
        var txtNom = ChampDialogue(prm.Pseudo);
        var cbSombre = CaseDialogue("Thème sombre", prm.Sombre);
        var cbSon = CaseDialogue("Notifications sonores", prm.NotifSon);
        var cbLu = CaseDialogue("Envoyer les accusés de lecture", prm.AccusesLecture);
        var cbSaisie = CaseDialogue("Indicateur « en train d'écrire »", prm.IndicateurSaisie);
        var btnFond = new Button { Content = "Choisir un fond d'écran…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 4) };
        var btnSansFond = new Button { Content = "Retirer le fond d'écran", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 16) };
        var btnOk = new Button { Content = "Enregistrer", IsDefault = true, Padding = new Thickness(8, 4, 8, 4) };
        var fond = prm.Fond;

        var fen = Fenetre("Réglages", 360, new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = "Mon nom (visible par mes contacts)" },
                txtNom, cbSombre, cbSon, cbLu, cbSaisie,
                new TextBlock { Text = "Mon adresse IP (à donner à vos contacts) :", Margin = new Thickness(0, 6, 0, 0) },
                new TextBox { Text = MesIp(), IsReadOnly = true, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(4),
                              Background = (Brush)Resources["FondEntete"], Foreground = (Brush)Resources["Texte"] },
                btnFond, btnSansFond, btnOk
            }
        });

        btnFond.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
            if (d.ShowDialog() == true) fond = d.FileName;
        };
        btnSansFond.Click += (_, _) => fond = "";
        btnOk.Click += (_, _) =>
        {
            if (txtNom.Text.Trim() != "") prm.Pseudo = txtNom.Text.Trim();
            prm.NotifSon = cbSon.IsChecked == true;
            prm.AccusesLecture = cbLu.IsChecked == true;
            prm.IndicateurSaisie = cbSaisie.IsChecked == true;
            prm.Sombre = cbSombre.IsChecked == true;
            prm.Fond = fond;
            ThemeService.Appliquer(Resources, prm.Sombre);
            AppliquerFond(prm.Fond);
            StockageService.SauverParametres(prm);
            fen.Close();
        };
        fen.ShowDialog();
    }
}
