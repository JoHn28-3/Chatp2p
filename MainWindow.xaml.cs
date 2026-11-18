using System.ComponentModel;
using System.IO;
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
    readonly Parametres prm = new();
    readonly ReseauService reseau;
    readonly ChatService chat;
    readonly ICollectionView vue;
    Peer? selection;
    ChatMessage? reponse, modification;
    bool voirArchives;

    public MainWindow()
    {
        InitializeComponent();

        reseau = new ReseauService(Dispatcher, prm);
        chat = new ChatService(reseau, prm, Dispatcher) { FenetreActive = () => IsActive };

        lstChats.ItemsSource = reseau.Peers;
        vue = CollectionViewSource.GetDefaultView(reseau.Peers);
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Epingle), ListSortDirection.Descending));
        vue.SortDescriptions.Add(new SortDescription(nameof(Peer.Derniere), ListSortDirection.Descending));
        vue.Filter = o => o is Peer p && p.Archive == voirArchives
            && (txtRecherche.Text == "" || p.Nom.Contains(txtRecherche.Text, StringComparison.OrdinalIgnoreCase));

        chat.ListeChangee += () => vue.Refresh();
        chat.MessageAjoute += (p, m) => { if (p == selection) DefilerEnBas(); };
        reseau.PeerConnecte += p => { if (selection == null) Selectionner(p); vue.Refresh(); };

        ThemeService.Appliquer(Resources, prm.Sombre);
        Activated += (s, e) =>
        {
            if (selection != null) { selection.NonLus = 0; chat.EnvoyerLus(selection); }
        };
        Loaded += (s, e) =>
        {
            if (!reseau.Demarrer(out var err))
                MessageBox.Show("Impossible d'écouter sur le port 5000 : " + err);
        };
    }

    void DefilerEnBas() =>
        Dispatcher.BeginInvoke(() => scroll.ScrollToEnd(), DispatcherPriority.Background);

    // ---------- Connexion ----------
    async void BtnConnecter_Click(object sender, RoutedEventArgs e)
    {
        try { await reseau.ConnecterAsync(txtIP.Text.Trim()); }
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

    void TxtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { BtnEnvoyer_Click(sender, e); e.Handled = true; }
    }

    void TxtMessage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (selection == null || modification != null || txtMessage.Text == "") return;
        chat.Frappe(selection);
    }

    async void BtnFichier_Click(object sender, RoutedEventArgs e)
    {
        if (!ChatService.PeutEnvoyer(selection, out var erreur)) { MessageBox.Show(erreur); return; }
        var p = selection!;
        var dlg = new OpenFileDialog();
        if (dlg.ShowDialog() != true) return;
        if (new FileInfo(dlg.FileName).Length > FichierService.TailleMax)
        {
            MessageBox.Show("Fichier trop volumineux (max 20 Mo).");
            return;
        }
        await chat.EnvoyerFichierAsync(p, dlg.FileName);
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
        selection = p;
        chat.Selection = p;
        icMessages.ItemsSource = p?.Messages;
        entete.DataContext = p;
        lstChats.SelectedItem = p;
        AnnulerContexte();
        if (p != null) { p.NonLus = 0; chat.EnvoyerLus(p); }
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

    // ---------- Menu des messages ----------
    static ChatMessage? MsgDe(object s) => (s as FrameworkElement)?.DataContext as ChatMessage;

    void MenuRepondre_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is not { } m) return;
        AnnulerContexte();
        reponse = m;
        txtBandeau.Text = "Réponse à : " + m.Texte;
        bandeau.Visibility = Visibility.Visible;
        txtMessage.Focus();
    }

    void MenuCopier_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m && m.Texte != "") Clipboard.SetText(m.Texte);
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
        if (MsgDe(s) is { } m) m.Etoile = !m.Etoile;
    }

    void MenuSupprMoi_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { } m) selection?.Messages.Remove(m);
    }

    void MenuSupprTous_Click(object s, RoutedEventArgs e)
    {
        if (MsgDe(s) is { EstMoi: true } m && selection is { } p)
            _ = chat.SupprimerPourTousAsync(p, m);
    }

    // ---------- Menu des discussions ----------
    void ChatMenu_Click(object s, RoutedEventArgs e) => vue.Refresh();

    void ChatSuppr_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Peer p) return;
        if (p == selection)
        {
            selection = null;
            chat.Selection = null;
            icMessages.ItemsSource = null;
            entete.DataContext = null;
        }
        reseau.Peers.Remove(p);
    }

    void TxtRecherche_TextChanged(object s, TextChangedEventArgs e) => vue?.Refresh();

    void BtnArchives_Click(object s, RoutedEventArgs e)
    {
        voirArchives = !voirArchives;
        lblTitre.Text = voirArchives ? "Archivées" : "Discussions";
        vue.Refresh();
    }

    // ---------- Emojis ----------
    void BtnEmoji_Click(object s, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)s, Placement = PlacementMode.Top };
        foreach (var em in new[] { "😀", "😂", "😍", "👍", "🙏", "😢", "😮", "🔥", "🎉", "❤️", "😎", "🤔" })
        {
            var it = new MenuItem { Header = em, FontSize = 20 };
            it.Click += (_, _) =>
            {
                txtMessage.Text += em;
                txtMessage.CaretIndex = txtMessage.Text.Length;
                txtMessage.Focus();
            };
            menu.Items.Add(it);
        }
        menu.IsOpen = true;
    }

    // ---------- Réglages ----------
    void BtnReglages_Click(object s, RoutedEventArgs e)
    {
        var txtNom = new TextBox { Text = prm.Pseudo, Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(4) };
        var cbSombre = new CheckBox { Content = "Thème sombre", IsChecked = prm.Sombre, Margin = new Thickness(0, 0, 0, 8) };
        var cbSon = new CheckBox { Content = "Notifications sonores", IsChecked = prm.NotifSon, Margin = new Thickness(0, 0, 0, 8) };
        var cbLu = new CheckBox { Content = "Envoyer les accusés de lecture", IsChecked = prm.AccusesLecture, Margin = new Thickness(0, 0, 0, 8) };
        var cbSaisie = new CheckBox { Content = "Indicateur « en train d'écrire »", IsChecked = prm.IndicateurSaisie, Margin = new Thickness(0, 0, 0, 12) };
        var btnFond = new Button { Content = "Choisir un fond d'écran…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 16) };
        var btnOk = new Button { Content = "Enregistrer", IsDefault = true, Padding = new Thickness(8, 4, 8, 4) };

        var fen = new Window
        {
            Title = "Réglages",
            Width = 340,
            SizeToContent = SizeToContent.Height,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock { Text = "Mon nom (visible par mes contacts)" },
                    txtNom, cbSombre, cbSon, cbLu, cbSaisie, btnFond, btnOk
                }
            }
        };

        btnFond.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" };
            if (d.ShowDialog() == true)
                scroll.Background = new ImageBrush(new BitmapImage(new Uri(d.FileName))) { Stretch = Stretch.UniformToFill };
        };
        btnOk.Click += (_, _) =>
        {
            if (txtNom.Text.Trim() != "") prm.Pseudo = txtNom.Text.Trim();
            prm.NotifSon = cbSon.IsChecked == true;
            prm.AccusesLecture = cbLu.IsChecked == true;
            prm.IndicateurSaisie = cbSaisie.IsChecked == true;
            prm.Sombre = cbSombre.IsChecked == true;
            ThemeService.Appliquer(Resources, prm.Sombre);
            fen.Close();
        };
        fen.ShowDialog();
    }
}