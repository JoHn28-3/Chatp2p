using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using Chatp2pWPF.Models;

namespace Chatp2pWPF.Services;

public class AudioService
{
    [StructLayout(LayoutKind.Sequential)]
    struct WAVEFORMATEX
    {
        public ushort wFormatTag, nChannels;
        public uint nSamplesPerSec, nAvgBytesPerSec;
        public ushort nBlockAlign, wBitsPerSample, cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength, dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags, dwLoops;
        public IntPtr lpNext, reserved;
    }

    [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr hWaveIn, int uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);
    [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveInStart(IntPtr hWaveIn);
    [DllImport("winmm.dll")] static extern int waveInReset(IntPtr hWaveIn);
    [DllImport("winmm.dll")] static extern int waveInClose(IntPtr hWaveIn);

    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hWaveOut);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hWaveOut);

    sealed class WaveSlot
    {
        public byte[] Buffer;
        public GCHandle Pin;
        public IntPtr PtrHdr;
        public bool Prepared;

        public WaveSlot(int size)
        {
            Buffer = new byte[size];
            Pin = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            PtrHdr = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
            ResetHeader(size);
        }

        public void ResetHeader(int length)
        {
            var hdr = new WAVEHDR { lpData = Pin.AddrOfPinnedObject(), dwBufferLength = (uint)length };
            Marshal.StructureToPtr(hdr, PtrHdr, false);
        }

        public void Resize(int size)
        {
            if (Pin.IsAllocated) Pin.Free();
            Buffer = new byte[size];
            Pin = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        }

        public void Dispose()
        {
            if (Pin.IsAllocated) Pin.Free();
            if (PtrHdr != IntPtr.Zero) { Marshal.FreeHGlobal(PtrHdr); PtrHdr = IntPtr.Zero; }
        }
    }

    // ---- Lecture des messages vocaux ----
    readonly DispatcherTimer _timerLecture = new() { Interval = TimeSpan.FromMilliseconds(100) };
    MediaPlayer? _player;
    ChatMessage? _msgLecture;

    // ---- Enregistrement des messages vocaux (WaveIn 16kHz mono) ----
    readonly DispatcherTimer _timerRec = new() { Interval = TimeSpan.FromMilliseconds(45) };
    IntPtr _hWaveInRec;
    readonly List<WaveSlot> _recSlots = new();
    MemoryStream? _pcmRec;
    bool _enCoursRec;
    DateTime _debutRec;
    public event Action<double>? NiveauMicroChange;

    // ---- Audio temps réel pendant un appel (8kHz mono) ----
    readonly DispatcherTimer _timerAppel = new() { Interval = TimeSpan.FromMilliseconds(35) };
    IntPtr _hWaveInAppel, _hWaveOutAppel;
    readonly List<WaveSlot> _appelInSlots = new();
    readonly List<WaveSlot> _appelOutSlots = new();
    int _outIndex;
    public event Action<byte[]>? AudioAppelCapture;
    public event Action<double>? NiveauMicroAppelChange;
    public event Action<double>? NiveauAudioRecuChange;

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

        _timerRec.Tick += (_, _) => PollerEntree(_hWaveInRec, _recSlots, bytes =>
        {
            _pcmRec?.Write(bytes, 0, bytes.Length);
            NiveauMicroChange?.Invoke(CalculerNiveau(bytes));
        });

        _timerAppel.Tick += (_, _) => PollerEntree(_hWaveInAppel, _appelInSlots, bytes =>
        {
            NiveauMicroAppelChange?.Invoke(CalculerNiveau(bytes));
            AudioAppelCapture?.Invoke(bytes);
        });
    }

    // ══════════════════ ENREGISTREMENT VOCAL ══════════════════

    public bool DemarrerEnregistrement()
    {
        if (_enCoursRec) AnnulerEnregistrement();
        _enCoursRec = true;
        _debutRec = DateTime.Now;
        _pcmRec = new MemoryStream();

        if (!OuvrirWaveIn(16000, 3200, 4, out _hWaveInRec, _recSlots))
            return false;
        _timerRec.Start();
        return true;
    }

    public string? ArreterEnregistrement(out int dureeSecondes)
    {
        dureeSecondes = Math.Max(1, (int)Math.Round((DateTime.Now - _debutRec).TotalSeconds));
        if (!_enCoursRec) return null;
        _enCoursRec = false;
        _timerRec.Stop();

        if (_hWaveInRec != IntPtr.Zero)
        {
            try { waveInReset(_hWaveInRec); } catch { }
            PollerEntree(_hWaveInRec, _recSlots, bytes => _pcmRec?.Write(bytes, 0, bytes.Length), reQueue: false);
            FermerWaveIn(ref _hWaveInRec, _recSlots);
        }

        var dossier = Path.Combine(Path.GetTempPath(), "ChatP2P", "Vocaux");
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, $"vocal_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        var pcm = _pcmRec?.ToArray() ?? Array.Empty<byte>();
        _pcmRec?.Dispose();
        _pcmRec = null;
        NiveauMicroChange?.Invoke(0);

        if (pcm.Length > 800)
        {
            dureeSecondes = Math.Max(1, (int)Math.Round(pcm.Length / 32000.0));
            EcrireWav(chemin, 16000, pcm);
        }
        else
        {
            EcrireWavSilence(chemin, Math.Min(dureeSecondes, 30));
        }

        return chemin;
    }

    public void AnnulerEnregistrement()
    {
        _enCoursRec = false;
        _timerRec.Stop();
        FermerWaveIn(ref _hWaveInRec, _recSlots);
        _pcmRec?.Dispose();
        _pcmRec = null;
        NiveauMicroChange?.Invoke(0);
    }

    // ══════════════════ AUDIO APPEL TEMPS RÉEL ══════════════════

    public bool DemarrerAudioAppel(out bool microOk, out bool hpOk)
    {
        ArreterAudioAppel();
        microOk = OuvrirWaveIn(8000, 1600, 4, out _hWaveInAppel, _appelInSlots);
        if (microOk) _timerAppel.Start();

        var fmt = FormatPcm(8000);
        hpOk = waveOutOpen(out _hWaveOutAppel, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) == 0;
        if (hpOk)
        {
            for (int i = 0; i < 16; i++) _appelOutSlots.Add(new WaveSlot(3200));
        }
        else _hWaveOutAppel = IntPtr.Zero;

        return microOk;
    }

    public void LireAudioAppel(byte[] pcm)
    {
        if (pcm.Length == 0) return;
        NiveauAudioRecuChange?.Invoke(CalculerNiveau(pcm));
        if (_hWaveOutAppel == IntPtr.Zero || _appelOutSlots.Count == 0) return;

        var slot = _appelOutSlots[_outIndex];
        _outIndex = (_outIndex + 1) % _appelOutSlots.Count;

        if (slot.Prepared)
        {
            var cur = Marshal.PtrToStructure<WAVEHDR>(slot.PtrHdr);
            if ((cur.dwFlags & 1) == 0) return; // slot encore en lecture, on drop ce paquet
            waveOutUnprepareHeader(_hWaveOutAppel, slot.PtrHdr, Marshal.SizeOf<WAVEHDR>());
            slot.Prepared = false;
        }

        if (pcm.Length > slot.Buffer.Length) slot.Resize(pcm.Length);
        Buffer.BlockCopy(pcm, 0, slot.Buffer, 0, pcm.Length);
        slot.ResetHeader(pcm.Length);

        if (waveOutPrepareHeader(_hWaveOutAppel, slot.PtrHdr, Marshal.SizeOf<WAVEHDR>()) == 0)
        {
            slot.Prepared = true;
            waveOutWrite(_hWaveOutAppel, slot.PtrHdr, Marshal.SizeOf<WAVEHDR>());
        }
    }

    public void ArreterAudioAppel()
    {
        _timerAppel.Stop();
        FermerWaveIn(ref _hWaveInAppel, _appelInSlots);

        if (_hWaveOutAppel != IntPtr.Zero)
        {
            try { waveOutReset(_hWaveOutAppel); } catch { }
            int sz = Marshal.SizeOf<WAVEHDR>();
            foreach (var slot in _appelOutSlots)
            {
                if (slot.Prepared) try { waveOutUnprepareHeader(_hWaveOutAppel, slot.PtrHdr, sz); } catch { }
                slot.Dispose();
            }
            _appelOutSlots.Clear();
            try { waveOutClose(_hWaveOutAppel); } catch { }
            _hWaveOutAppel = IntPtr.Zero;
        }
        NiveauMicroAppelChange?.Invoke(0);
        NiveauAudioRecuChange?.Invoke(0);
    }

    // ══════════════════ LECTURE MESSAGES VOCAUX ══════════════════

    public void BasculerLecture(ChatMessage m)
    {
        if (string.IsNullOrEmpty(m.Fichier) || !File.Exists(m.Fichier)) return;

        if (_msgLecture == m && _player != null)
        {
            if (m.EnLecture) { _player.Pause(); m.EnLecture = false; _timerLecture.Stop(); }
            else { _player.Play(); m.EnLecture = true; _timerLecture.Start(); }
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
        m.VitesseLecture = m.VitesseLecture switch { "1x" => "1.5x", "1.5x" => "2x", _ => "1x" };
        if (_msgLecture == m && _player != null) _player.SpeedRatio = ParseVitesse(m.VitesseLecture);
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

    // ══════════════════ HELPERS WINMM ══════════════════

    static WAVEFORMATEX FormatPcm(int rate) => new()
    {
        wFormatTag = 1,
        nChannels = 1,
        nSamplesPerSec = (uint)rate,
        wBitsPerSample = 16,
        nBlockAlign = 2,
        nAvgBytesPerSec = (uint)(rate * 2),
        cbSize = 0
    };

    static bool OuvrirWaveIn(int rate, int bufSize, int nbBuf, out IntPtr handle, List<WaveSlot> slots)
    {
        handle = IntPtr.Zero;
        slots.Clear();
        var fmt = FormatPcm(rate);
        if (waveInOpen(out handle, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0)
        {
            handle = IntPtr.Zero;
            return false;
        }

        int sz = Marshal.SizeOf<WAVEHDR>();
        for (int i = 0; i < nbBuf; i++)
        {
            var slot = new WaveSlot(bufSize);
            waveInPrepareHeader(handle, slot.PtrHdr, sz);
            slot.Prepared = true;
            waveInAddBuffer(handle, slot.PtrHdr, sz);
            slots.Add(slot);
        }
        if (waveInStart(handle) != 0)
        {
            FermerWaveIn(ref handle, slots);
            return false;
        }
        return true;
    }

    static void PollerEntree(IntPtr handle, List<WaveSlot> slots, Action<byte[]> onBytes, bool reQueue = true)
    {
        if (handle == IntPtr.Zero) return;
        int sz = Marshal.SizeOf<WAVEHDR>();
        foreach (var slot in slots)
        {
            var hdr = Marshal.PtrToStructure<WAVEHDR>(slot.PtrHdr);
            if ((hdr.dwFlags & 1) == 0) continue;

            if (hdr.dwBytesRecorded > 0)
            {
                var data = new byte[hdr.dwBytesRecorded];
                Buffer.BlockCopy(slot.Buffer, 0, data, 0, data.Length);
                onBytes(data);
            }

            if (slot.Prepared)
            {
                waveInUnprepareHeader(handle, slot.PtrHdr, sz);
                slot.Prepared = false;
            }
            if (reQueue)
            {
                slot.ResetHeader(slot.Buffer.Length);
                if (waveInPrepareHeader(handle, slot.PtrHdr, sz) == 0)
                {
                    slot.Prepared = true;
                    waveInAddBuffer(handle, slot.PtrHdr, sz);
                }
            }
        }
    }

    static void FermerWaveIn(ref IntPtr handle, List<WaveSlot> slots)
    {
        if (handle == IntPtr.Zero) return;
        try { waveInReset(handle); } catch { }
        int sz = Marshal.SizeOf<WAVEHDR>();
        foreach (var slot in slots)
        {
            if (slot.Prepared) try { waveInUnprepareHeader(handle, slot.PtrHdr, sz); } catch { }
            slot.Dispose();
        }
        slots.Clear();
        try { waveInClose(handle); } catch { }
        handle = IntPtr.Zero;
    }

    static double CalculerNiveau(byte[] pcm)
    {
        if (pcm.Length < 2) return 0;
        long max = 0;
        for (int i = 0; i + 1 < pcm.Length; i += 2)
        {
            short s = (short)(pcm[i] | (pcm[i + 1] << 8));
            int abs = s == short.MinValue ? 32767 : Math.Abs(s);
            if (abs > max) max = abs;
        }
        return Math.Min(100.0, max * 100.0 / 16000.0);
    }

    static double ParseVitesse(string v) => v switch { "1.5x" => 1.5, "2x" => 2.0, _ => 1.0 };

    static void EcrireWav(string chemin, int sampleRate, byte[] pcm)
    {
        using var fs = File.Create(chemin);
        using var bw = new BinaryWriter(fs);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm.Length);
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
        bw.Write(pcm.Length);
        bw.Write(pcm);
    }

    static void EcrireWavSilence(string chemin, int secondes)
    {
        int sampleRate = 16000;
        int samples = sampleRate * Math.Max(1, secondes);
        var pcm = new byte[samples * 2];
        for (int i = 0; i < Math.Min(1600, samples); i++)
        {
            short val = (short)(Math.Sin(2 * Math.PI * 520 * i / sampleRate) * 1800);
            pcm[i * 2] = (byte)(val & 0xFF);
            pcm[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        EcrireWav(chemin, sampleRate, pcm);
    }
}
