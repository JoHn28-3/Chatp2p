using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public class AudioService
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern int mciSendString(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

    bool _enCoursRec, _modeSimule;
    DateTime _debutRec;

    readonly DispatcherTimer _timerLecture = new() { Interval = TimeSpan.FromMilliseconds(100) };
    MediaPlayer? _player;
    ChatMessage? _msgLecture;

    public AudioService()
    {
        _timerLecture.Tick += (_, _) =>
        {
            if (_player == null || _msgLecture == null) return;
            if (_player.NaturalDuration.HasTimeSpan && _player.NaturalDuration.TimeSpan.TotalMilliseconds > 0)
            {
                var p = _player.Position.TotalMilliseconds * 100.0 / _player.NaturalDuration.TimeSpan.TotalMilliseconds;
                _msgLecture.ProgressionLecture = Math.Clamp(p, 0, 100);
            }
        };
    }

    public bool DemarrerEnregistrement()
    {
        if (_enCoursRec) AnnulerEnregistrement();
        _enCoursRec = true;
        _modeSimule = false;
        _debutRec = DateTime.Now;

        try
        {
            mciSendString("close chatrec", null, 0, IntPtr.Zero);
            var err1 = mciSendString("open new Type waveaudio Alias chatrec", null, 0, IntPtr.Zero);
            mciSendString("set chatrec time format ms bitspersample 16 channels 1 samplespersec 16000", null, 0, IntPtr.Zero);
            var err2 = mciSendString("record chatrec", null, 0, IntPtr.Zero);
            if (err1 != 0 || err2 != 0) _modeSimule = true;
        }
        catch { _modeSimule = true; }

        return !_modeSimule;
    }

    public string? ArreterEnregistrement(out int dureeSecondes)
    {
        dureeSecondes = Math.Max(1, (int)Math.Round((DateTime.Now - _debutRec).TotalSeconds));
        if (!_enCoursRec) return null;
        _enCoursRec = false;

        var dossier = Path.Combine(Path.GetTempPath(), "ChatP2P", "Vocaux");
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, $"vocal_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        if (!_modeSimule)
        {
            try
            {
                mciSendString("stop chatrec", null, 0, IntPtr.Zero);
                mciSendString($"save chatrec \"{chemin}\"", null, 0, IntPtr.Zero);
                mciSendString("close chatrec", null, 0, IntPtr.Zero);
            }
            catch { }
        }

        if (!File.Exists(chemin) || new FileInfo(chemin).Length <= 44)
            EcrireWavSilence(chemin, Math.Min(dureeSecondes, 30));

        return chemin;
    }

    public void AnnulerEnregistrement()
    {
        _enCoursRec = false;
        try { mciSendString("close chatrec", null, 0, IntPtr.Zero); } catch { }
    }

    public void BasculerLecture(ChatMessage m)
    {
        if (string.IsNullOrEmpty(m.Fichier) || !File.Exists(m.Fichier)) return;

        if (_msgLecture == m && _player != null)
        {
            if (m.EnLecture)
            {
                _player.Pause();
                m.EnLecture = false;
                _timerLecture.Stop();
            }
            else
            {
                _player.Play();
                m.EnLecture = true;
                _timerLecture.Start();
            }
            return;
        }

        ArreterLecture();
        _msgLecture = m;
        _player = new MediaPlayer();
        _player.MediaEnded += (_, _) => ArreterLecture();
        _player.Open(new Uri(m.Fichier));
        _player.SpeedRatio = ParseVitesse(m.VitesseLecture);
        _player.Play();
        m.EnLecture = true;
        _timerLecture.Start();
    }

    public void ChangerVitesse(ChatMessage m)
    {
        m.VitesseLecture = m.VitesseLecture switch
        {
            "1x" => "1.5x",
            "1.5x" => "2x",
            _ => "1x"
        };
        if (_msgLecture == m && _player != null)
            _player.SpeedRatio = ParseVitesse(m.VitesseLecture);
    }

    public void ArreterLecture()
    {
        _timerLecture.Stop();
        if (_msgLecture != null)
        {
            _msgLecture.EnLecture = false;
            _msgLecture.ProgressionLecture = 0;
            _msgLecture = null;
        }
        try { _player?.Stop(); _player?.Close(); } catch { }
        _player = null;
    }

    static double ParseVitesse(string v) => v switch { "1.5x" => 1.5, "2x" => 2.0, _ => 1.0 };

    static void EcrireWavSilence(string chemin, int secondes)
    {
        int sampleRate = 16000;
        int samples = sampleRate * Math.Max(1, secondes);
        using var fs = File.Create(chemin);
        using var bw = new BinaryWriter(fs);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + samples * 2);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            // Doux bip au début puis silence
            short val = i < 1600 ? (short)(Math.Sin(2 * Math.PI * 520 * i / sampleRate) * 1800) : (short)0;
            bw.Write(val);
        }
    }
}
