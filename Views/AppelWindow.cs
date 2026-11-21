using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Chatp2pWPF.Models;
using Chatp2pWPF.Services;

namespace Chatp2pWPF.Views;

public class AppelWindow : Window
{
    readonly Peer _peer;
    readonly ChatService _chat;
    readonly AudioService _audio = new();
    readonly WebcamService _webcam;
    readonly string _callId;
    readonly string _typeAppel;
    readonly bool _entrant;
    readonly DispatcherTimer _chrono = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _sonnerie = new() { Interval = TimeSpan.FromSeconds(2) };

    readonly TextBlock _lblStatut, _lblMateriel, _lblVideoInfo;
    readonly Border _zoneVideo;
    readonly Grid _avatarBloc;
    readonly Image _imgVideoDistante, _imgVideoLocale;
    readonly ProgressBar _pbMicro, _pbAudioRecu;
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
        _webcam = new WebcamService(Dispatcher);

        Title = $"Appel {_typeAppel} — {peer.Nom}";
        Width = 460;
        Height = 620;
        try { Icon = new BitmapImage(new Uri("pack://application:,,,/app.ico")); } catch { }
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)new BrushConverter().ConvertFromString("#0B141A")!;
        Foreground = Brushes.White;

        var grille = new Grid { Margin = new Thickness(18) };
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // En-tête sécurité + état matériel (micro / caméra)
        var haut = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        haut.Children.Add(new TextBlock
        {
            Text = $"🔒 Chiffré de bout en bout · Appel {_typeAppel}",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#8696A0")!,
            FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center
        });
        _lblMateriel = new TextBlock
        {
            Text = "Initialisation micro / caméra…",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#25D366")!,
            FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        haut.Children.Add(_lblMateriel);
        grille.Children.Add(haut);

        // Zone centrale : Vidéo réelle (distante + miniature locale) ou Avatar
        var centre = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(centre, 1);

        _imgVideoDistante = new Image { Stretch = Stretch.UniformToFill };
        _imgVideoLocale = new Image { Stretch = Stretch.UniformToFill };
        _lblVideoInfo = new TextBlock
        {
            Text = "📹 Caméra active (flux P2P)",
            Foreground = Brushes.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
        };

        _zoneVideo = new Border
        {
            Width = 390, Height = 260, CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            Background = (Brush)new BrushConverter().ConvertFromString("#182229")!,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = _camActive ? Visibility.Visible : Visibility.Collapsed,
            Child = new Grid
            {
                Children =
                {
                    _lblVideoInfo,
                    _imgVideoDistante,
                    new Border
                    {
                        Width = 110, Height = 82, CornerRadius = new CornerRadius(8),
                        ClipToBounds = true,
                        BorderBrush = (Brush)new BrushConverter().ConvertFromString("#25D366")!,
                        BorderThickness = new Thickness(1.5),
                        Background = (Brush)new BrushConverter().ConvertFromString("#111B21")!,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(8),
                        Child = new Grid
                        {
                            Children =
                            {
                                _imgVideoLocale,
                                new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                                    VerticalAlignment = VerticalAlignment.Bottom,
                                    Padding = new Thickness(4, 1, 4, 1),
                                    Child = new TextBlock { Text = "Vous", Foreground = Brushes.White, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }
                                }
                            }
                        }
                    }
                }
            }
        };
        centre.Children.Add(_zoneVideo);

        _avatarBloc = new Grid { Width = 104, Height = 104, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        _avatarBloc.Children.Add(new Ellipse { Fill = peer.Couleur });
        if (peer.AvatarImage != null)
            _avatarBloc.Children.Add(new Ellipse { Fill = new ImageBrush(peer.AvatarImage) { Stretch = Stretch.UniformToFill } });
        else
            _avatarBloc.Children.Add(new TextBlock { Text = peer.Initiale, FontSize = 42, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        if (_camActive) _avatarBloc.Visibility = Visibility.Collapsed;
        centre.Children.Add(_avatarBloc);

        centre.Children.Add(new TextBlock
        {
            Text = peer.Nom, FontSize = 22, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center
        });

        _lblStatut = new TextBlock
        {
            Text = entrant ? $"Appel {_typeAppel.ToLower()} entrant…" : "Appel en cours…",
            FontSize = 14, Margin = new Thickness(0, 4, 0, 0),
            Foreground = (Brush)new BrushConverter().ConvertFromString("#25D366")!,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        centre.Children.Add(_lblStatut);
        grille.Children.Add(centre);

        // VU-mètres audio en temps réel (preuve visuelle Micro + Réception)
        var vu = new Grid { Margin = new Thickness(12, 4, 12, 14) };
        Grid.SetRow(vu, 2);
        vu.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        vu.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        vu.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _pbMicro = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Foreground = (Brush)new BrushConverter().ConvertFromString("#25D366")!, Background = (Brush)new BrushConverter().ConvertFromString("#202C33")! };
        _pbAudioRecu = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Foreground = (Brush)new BrushConverter().ConvertFromString("#53BDEB")!, Background = (Brush)new BrushConverter().ConvertFromString("#202C33")! };

        var p1 = new StackPanel();
        p1.Children.Add(new TextBlock { Text = "🎤 Micro envoyé", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFromString("#8696A0")!, Margin = new Thickness(0, 0, 0, 3) });
        p1.Children.Add(_pbMicro);
        var p2 = new StackPanel();
        Grid.SetColumn(p2, 2);
        p2.Children.Add(new TextBlock { Text = "🔊 Audio reçu", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFromString("#8696A0")!, Margin = new Thickness(0, 0, 0, 3) });
        p2.Children.Add(_pbAudioRecu);
        vu.Children.Add(p1);
        vu.Children.Add(p2);
        grille.Children.Add(vu);

        // Boutons bas
        var bas = new Grid();
        Grid.SetRow(bas, 3);

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

        _pnlActif = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = entrant ? Visibility.Collapsed : Visibility.Visible
        };
        _btnMute = BoutonRond("🎤", "#2A3942", "Couper le micro");
        _btnCam = BoutonRond("📹", _camActive ? "#00A884" : "#2A3942", "Activer / Couper la caméra");
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
            if (_muet) _pbMicro.Value = 0;
        };
        _btnCam.Click += (_, _) =>
        {
            _camActive = !_camActive;
            _zoneVideo.Visibility = _camActive ? Visibility.Visible : Visibility.Collapsed;
            _avatarBloc.Visibility = _camActive ? Visibility.Collapsed : Visibility.Visible;
            _btnCam.Background = (Brush)new BrushConverter().ConvertFromString(_camActive ? "#00A884" : "#2A3942")!;
            if (_camActive) _camOk = _webcam.Demarrer();
            else { _webcam.Arreter(); _imgVideoLocale.Source = null; }
            MajLabelMateriel();
        };
        _btnHp.Click += (_, _) =>
        {
            _hpActif = !_hpActif;
            _btnHp.Background = (Brush)new BrushConverter().ConvertFromString(_hpActif ? "#00A884" : "#2A3942")!;
            _btnHp.Content = _hpActif ? "🔊" : "🔈";
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

        // Brancher les flux temps réel Micro + Webcam
        _audio.NiveauMicroAppelChange += n => { if (!_muet) _pbMicro.Value = n; };
        _audio.NiveauAudioRecuChange += n => _pbAudioRecu.Value = n;
        _audio.AudioAppelCapture += pcm =>
        {
            if (_actif && !_muet) _ = _chat.EnvoyerAudioAppelAsync(_peer, _callId, pcm);
        };
        _webcam.FrameCapturee += (bmp, jpeg) =>
        {
            _imgVideoLocale.Source = bmp;
            _lblVideoInfo.Visibility = Visibility.Collapsed;
            if (_actif && _camActive) _ = _chat.EnvoyerVideoAppelAsync(_peer, _callId, jpeg);
        };

        _chrono.Tick += (_, _) =>
        {
            _secondes++;
            _lblStatut.Text = TimeSpan.FromSeconds(_secondes).ToString(@"mm\:ss");
        };
        _sonnerie.Tick += (_, _) => { if (!_actif) try { SystemSounds.Beep.Play(); } catch { } };
        _sonnerie.Start();

        if (!entrant) ActiverMateriel();

        Closed += (_, _) =>
        {
            _chrono.Stop();
            _sonnerie.Stop();
            _webcam.Arreter();
            _audio.ArreterAudioAppel();
            if (!_termine)
            {
                _ = _chat.TerminerAppelAsync(_peer, _callId);
                Enregistrer(false);
            }
        };
    }

    bool _microOk, _camOk;
    void ActiverMateriel()
    {
        _audio.DemarrerAudioAppel(out _microOk, out _);
        _camOk = _camActive && _webcam.Demarrer();
        MajLabelMateriel();
    }

    void MajLabelMateriel()
    {
        var txtMic = _microOk ? "🎤 Micro actif" : "🎤 Micro indisponible";
        var txtCam = !_camActive ? "📹 Caméra coupée" : (_camOk ? "📹 Caméra active" : "📹 Caméra indisponible");
        _lblMateriel.Text = $"{txtMic}  ·  {txtCam}";
    }

    public void SurAudioRecu(byte[] pcm)
    {
        if (!_actif || _termine || !_hpActif) return;
        _audio.LireAudioAppel(pcm);
    }

    public void SurVideoRecu(byte[] jpeg)
    {
        if (!_actif || _termine) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(jpeg);
            bmp.EndInit();
            bmp.Freeze();
            _imgVideoDistante.Source = bmp;
            _lblVideoInfo.Visibility = Visibility.Collapsed;
            if (_zoneVideo.Visibility != Visibility.Visible)
            {
                _zoneVideo.Visibility = Visibility.Visible;
                _avatarBloc.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    static Button BoutonRond(string icone, string couleur, string tip)
    {
        var btn = new Button
        {
            Content = icone, FontSize = 22, Width = 54, Height = 54, ToolTip = tip,
            Foreground = Brushes.White,
            Background = (Brush)new BrushConverter().ConvertFromString(couleur)!,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var tpl = new ControlTemplate(typeof(Button));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(27));
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
        ActiverMateriel();
        _chrono.Start();
    }

    public void FermerSilencieusement()
    {
        if (_termine) return;
        _termine = true;
        _chrono.Stop();
        _sonnerie.Stop();
        _webcam.Arreter();
        _audio.ArreterAudioAppel();
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
        _webcam.Arreter();
        _audio.ArreterAudioAppel();
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
