using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Chatp2pWPF.Services;

public class WebcamService
{
    [StructLayout(LayoutKind.Sequential)]
    struct VIDEOHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength, dwBytesUsed, dwTimeCaptured;
        public IntPtr dwUser;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public IntPtr[] dwReserved;
    }

    delegate IntPtr FrameCallback(IntPtr hWnd, ref VIDEOHDR hdr);

    [DllImport("avicap32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr capCreateCaptureWindowW(string lpszWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hwndParent, int nID);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, FrameCallback fp);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();

    const uint WM_CAP_SET_CALLBACK_FRAME = 0x0405;
    const uint WM_CAP_DRIVER_CONNECT = 0x040A;
    const uint WM_CAP_DRIVER_DISCONNECT = 0x040B;
    const uint WM_CAP_GRAB_FRAME_NOSTOP = 0x043D;

    readonly Dispatcher _ui;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    IntPtr _hwnd;
    FrameCallback? _cb;
    byte[]? _dernierBrut;
    bool _enCours, _encodage;

    public event Action<BitmapImage, byte[]>? FrameCapturee;

    public WebcamService(Dispatcher ui)
    {
        _ui = ui;
        _timer.Tick += (_, _) => CapturerFrame();
    }

    public bool Demarrer()
    {
        if (_enCours) return true;
        try
        {
            _hwnd = capCreateCaptureWindowW("ChatP2PCam", 0, 0, 0, 320, 240, GetDesktopWindow(), 0);
            if (_hwnd == IntPtr.Zero) return false;

            if (SendMessage(_hwnd, WM_CAP_DRIVER_CONNECT, IntPtr.Zero, IntPtr.Zero) == IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                return false;
            }

            _cb = (IntPtr _, ref VIDEOHDR hdr) =>
            {
                if (hdr.dwBytesUsed > 0)
                {
                    var buf = new byte[hdr.dwBytesUsed];
                    Marshal.Copy(hdr.lpData, buf, 0, buf.Length);
                    _dernierBrut = buf;
                }
                return IntPtr.Zero;
            };
            SendMessage(_hwnd, WM_CAP_SET_CALLBACK_FRAME, IntPtr.Zero, _cb);
            _enCours = true;
            _timer.Start();
            CapturerFrame();
            return true;
        }
        catch
        {
            Arreter();
            return false;
        }
    }

    void CapturerFrame()
    {
        if (!_enCours || _hwnd == IntPtr.Zero || _encodage) return;
        _dernierBrut = null;
        SendMessage(_hwnd, WM_CAP_GRAB_FRAME_NOSTOP, IntPtr.Zero, IntPtr.Zero);
        var brut = _dernierBrut;
        if (brut == null || brut.Length < 100) return;

        _encodage = true;
        _ = Task.Run(() =>
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 320;
                bmp.StreamSource = new MemoryStream(brut);
                bmp.EndInit();
                bmp.Freeze();

                var enc = new JpegBitmapEncoder { QualityLevel = 55 };
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using var ms = new MemoryStream();
                enc.Save(ms);
                var jpeg = ms.ToArray();

                _ui.BeginInvoke(() =>
                {
                    if (_enCours) FrameCapturee?.Invoke(bmp, jpeg);
                });
            }
            catch { }
            finally { _encodage = false; }
        });
    }

    public void Arreter()
    {
        _enCours = false;
        _timer.Stop();
        if (_hwnd != IntPtr.Zero)
        {
            try { SendMessage(_hwnd, WM_CAP_SET_CALLBACK_FRAME, IntPtr.Zero, IntPtr.Zero); } catch { }
            try { SendMessage(_hwnd, WM_CAP_DRIVER_DISCONNECT, IntPtr.Zero, IntPtr.Zero); } catch { }
            try { DestroyWindow(_hwnd); } catch { }
            _hwnd = IntPtr.Zero;
        }
        _cb = null;
    }
}
