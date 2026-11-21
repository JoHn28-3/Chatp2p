using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Chatp2pWPF.Models;
using Chatp2pWPF.Services;

namespace Chatp2pWPF.Views;

public class AppelWindow : Window
{
    readonly Peer _peer;
    readonly ChatService _chat;
    readonly string _callId;
    readonly string _typeAppel;
    readonly bool _entrant;
    readonly DispatcherTimer _chrono = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _sonnerie = new() { Interval = TimeSpan.FromSeconds(2) };

    readonly TextBlock _lblStatut;
    readonly Border _zoneVideo;
    readonly StackPanel _pnlEntrant, _pnlActif;
    readonly Button _btnMute, _btnCam, _btnHp;

    bool _actif, _termine, _muet, _camActive, _hpActif = true;
    int _secondes;

    public string CallId => _callId;

    public AppelWindow(Window owner, Peer peer, ChatService chat, string callId, string typeAppel, bool entrant)
    {
        Owner = owner;
        _peer = peer;
        _chat = chat;
        _callId = callId;
        _typeAppel = typeAppel;
        _entrant = entrant;
        _camActive = typeAppel == "Vidéo";

        Title = $"Appel {_typeAppel} — {peer.Nom}";
        Width = 380;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)new BrushConverter().ConvertFromString("#0B141A")!;
        Foreground = Brushes.White;

        var grille = new Grid { Margin = new Thickness(20) };
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // En-tête chiffrement
        var lblSec = new TextBlock
        {
            Text = $"🔒 Chiffré de bout en bout · Appel {_typeAppel}",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#8696A0")!,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        grille.Children.Add(lblSec);

        // Centre : Avatar / Caméra + Nom + Statut
        var centre = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(centre, 1);

        _zoneVideo = new Border
        {
            Width = 280, Height = 190, CornerRadius = new CornerRadius(14),
            Background = (Brush)new BrushConverter().ConvertFromString("#202C33")!,
            Margin = new Thickness(0, 0, 0, 16),
            Visibility = _camActive ? Visibility.Visible : Visibility.Collapsed,
            Child = new Grid
            {
                Children =
                {
                    new TextBlock { Text = "📹 Flux vidéo P2P", Foreground = Brushes.LightGray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 15 },
                    new Border
                    {
                        Width = 72, Height = 54, CornerRadius = new CornerRadius(8),
                        Background = (Brush)new BrushConverter().ConvertFromString("#005C4B")!,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(10),
                        Child = new TextBlock { Text = "Vous", Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            }
        };
        centre.Children.Add(_zoneVideo);

        var avatar = new Grid { Width = 110, Height = 110, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
        avatar.Children.Add(new Ellipse { Fill = peer.Couleur });
        if (peer.AvatarImage != null)
            avatar.Children.Add(new Ellipse { Fill = new ImageBrush(peer.AvatarImage) { Stretch = Stretch.UniformToFill } });
        else
            avatar.Children.Add(new TextBlock { Text = peer.Initiale, FontSize = 44, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        centre.Children.Add(avatar);

        centre.Children.Add(new TextBlock
        {
            Text = peer.Nom, FontSize = 24, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center
        });

        _lblStatut = new TextBlock
        {
            Text = entrant ? $"Appel {_typeAppel.ToLower()} entrant…" : "Appel en cours…",
            FontSize = 14, Margin = new Thickness(0, 6, 0, 0),
            Foreground = (Brush)new BrushConverter().ConvertFromString("#25D366")!,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        centre.Children.Add(_lblStatut);
        grille.Children.Add(centre);

        // Boutons bas
        var bas = new Grid();
        Grid.SetRow(bas, 2);

        // Entrant : Accepter / Refuser
        _pnlEntrant = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = entrant ? Visibility.Visible : Visibility.Collapsed
        };
        var btnRefuser = BoutonRond("❌", "#E53935", "Refuser");
        var btnAccepter = BoutonRond("✅", "#25D366", "Accepter");
        btnRefuser.Margin = new Thickness(0, 0, 36, 0);
        btnRefuser.Click += async (_, _) =>
        {
            await _chat.RepondreAppelAsync(_peer, _callId, false);
            Clore(false);
        };
        btnAccepter.Click += async (_, _) =>
        {
            await _chat.RepondreAppelAsync(_peer, _callId, true);
            DemarrerConversation();
        };
        _pnlEntrant.Children.Add(btnRefuser);
        _pnlEntrant.Children.Add(btnAccepter);

        // Actif / Sortant : Contrôles
        _pnlActif = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = entrant ? Visibility.Collapsed : Visibility.Visible
        };
        _btnMute = BoutonRond("🎤", "#2A3942", "Couper le micro");
        _btnCam = BoutonRond("📹", _camActive ? "#00A884" : "#2A3942", "Caméra");
        _btnHp = BoutonRond("🔊", "#00A884", "Haut-parleur");
        var btnRaccrocher = BoutonRond("📵", "#E53935", "Raccrocher");

        _btnMute.Margin = new Thickness(6);
        _btnCam.Margin = new Thickness(6);
        _btnHp.Margin = new Thickness(6);
        btnRaccrocher.Margin = new Thickness(14, 6, 6, 6);

        _btnMute.Click += (_, _) =>
        {
            _muet = !_muet;
            _btnMute.Background = (Brush)new BrushConverter().ConvertFromString(_muet ? "#E53935" : "#2A3942")!;
            _btnMute.Content = _muet ? "🔇" : "🎤";
        };
        _btnCam.Click += (_, _) =>
        {
            _camActive = !_camActive;
            _zoneVideo.Visibility = _camActive ? Visibility.Visible : Visibility.Collapsed;
            _btnCam.Background = (Brush)new BrushConverter().ConvertFromString(_camActive ? "#00A884" : "#2A3942")!;
        };
        _btnHp.Click += (_, _) =>
        {
            _hpActif = !_hpActif;
            _btnHp.Background = (Brush)new BrushConverter().ConvertFromString(_hpActif ? "#00A884" : "#2A3942")!;
        };
        btnRaccrocher.Click += async (_, _) =>
        {
            await _chat.TerminerAppelAsync(_peer, _callId);
            Clore(_actif);
        };

        _pnlActif.Children.Add(_btnMute);
        _pnlActif.Children.Add(_btnCam);
        _pnlActif.Children.Add(_btnHp);
        _pnlActif.Children.Add(btnRaccrocher);

        bas.Children.Add(_pnlEntrant);
        bas.Children.Add(_pnlActif);
        grille.Children.Add(bas);

        Content = grille;

        _chrono.Tick += (_, _) =>
        {
            _secondes++;
            _lblStatut.Text = TimeSpan.FromSeconds(_secondes).ToString(@"mm\:ss");
        };
        _sonnerie.Tick += (_, _) => { if (!_actif) try { SystemSounds.Beep.Play(); } catch { } };
        _sonnerie.Start();

        Closed += (_, _) =>
        {
            _chrono.Stop();
            _sonnerie.Stop();
            if (!_termine)
            {
                _ = _chat.TerminerAppelAsync(_peer, _callId);
                Enregistrer(false);
            }
        };
    }

    static Button BoutonRond(string icone, string couleur, string tip)
    {
        var btn = new Button
        {
            Content = icone, FontSize = 22, Width = 56, Height = 56, ToolTip = tip,
            Foreground = Brushes.White,
            Background = (Brush)new BrushConverter().ConvertFromString(couleur)!,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var tpl = new ControlTemplate(typeof(Button));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(28));
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        tpl.VisualTree = bd;
        btn.Template = tpl;
        return btn;
    }

    void DemarrerConversation()
    {
        if (_actif || _termine) return;
        _actif = true;
        _sonnerie.Stop();
        _pnlEntrant.Visibility = Visibility.Collapsed;
        _pnlActif.Visibility = Visibility.Visible;
        _lblStatut.Text = "00:00";
        _chrono.Start();
    }

    public void FermerSilencieusement()
    {
        if (_termine) return;
        _termine = true;
        _chrono.Stop();
        _sonnerie.Stop();
        Close();
    }

    public void SurReponseDistante(bool accepte)
    {
        if (_termine) return;
        if (accepte) DemarrerConversation();
        else
        {
            _lblStatut.Text = "Appel refusé";
            _sonnerie.Stop();
            Dispatcher.BeginInvoke(async () => { await Task.Delay(900); Clore(false); });
        }
    }

    public void SurFinDistante()
    {
        if (_termine) return;
        _lblStatut.Text = "Appel terminé";
        _chrono.Stop();
        _sonnerie.Stop();
        Dispatcher.BeginInvoke(async () => { await Task.Delay(700); Clore(_actif); });
    }

    void Clore(bool reussi)
    {
        if (_termine) return;
        _termine = true;
        _chrono.Stop();
        _sonnerie.Stop();
        Enregistrer(reussi);
        Close();
    }

    void Enregistrer(bool reussi)
    {
        _chat.EnregistrerAppel(new AppelInfo
        {
            Nom = _peer.Nom,
            Ip = _peer.Ip,
            Type = _typeAppel,
            Date = DateTime.Now,
            DureeSecondes = _secondes,
            Entrant = _entrant,
            Manque = _entrant && !reussi
        });
    }
}
