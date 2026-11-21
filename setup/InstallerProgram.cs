using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class InstallerProgram
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
    }
}

sealed class SetupForm : Form
{
    readonly TextBox _txtDir;
    readonly CheckBox _cbDesktop, _cbStartMenu, _cbLaunch;
    readonly ProgressBar _progress;
    readonly Label _lblStatus;
    readonly Button _btnInstall, _btnBrowse, _btnCancel;
    bool _done;

    public SetupForm()
    {
        Text = "Installation — ChatP2P Desktop";
        ClientSize = new Size(520, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 251);

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = Color.FromArgb(30, 27, 75)
        };

        var pic = new PictureBox
        {
            Size = new Size(64, 64),
            Location = new Point(22, 16),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                if (s != null) pic.Image = Image.FromStream(s);
        }
        catch { }

        var lblTitle = new Label
        {
            Text = "ChatP2P Desktop",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            Location = new Point(100, 18),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Assistant d'installation — Messagerie P2P locale",
            ForeColor = Color.FromArgb(165, 243, 252),
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(102, 50),
            AutoSize = true
        };
        header.Controls.Add(pic);
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        var lblPath = new Label
        {
            Text = "Dossier d'installation :",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(24, 114),
            AutoSize = true
        };
        _txtDir = new TextBox
        {
            Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ChatP2P Desktop"),
            Location = new Point(24, 138),
            Width = 376,
            Font = new Font("Segoe UI", 9.5f)
        };
        _btnBrowse = new Button
        {
            Text = "Parcourir…",
            Location = new Point(408, 136),
            Width = 88,
            Height = 27
        };
        _btnBrowse.Click += (_, __) =>
        {
            using (var dlg = new FolderBrowserDialog { SelectedPath = _txtDir.Text })
                if (dlg.ShowDialog() == DialogResult.OK) _txtDir.Text = Path.Combine(dlg.SelectedPath, "ChatP2P Desktop");
        };

        _cbDesktop = new CheckBox
        {
            Text = "Créer un raccourci sur le Bureau",
            Checked = true,
            Location = new Point(24, 180),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cbStartMenu = new CheckBox
        {
            Text = "Ajouter au Menu Démarrer",
            Checked = true,
            Location = new Point(24, 206),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cbLaunch = new CheckBox
        {
            Text = "Lancer ChatP2P Desktop après l'installation",
            Checked = true,
            Location = new Point(24, 232),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f)
        };

        _lblStatus = new Label
        {
            Text = "Prêt à installer ChatP2P Desktop sur votre ordinateur.",
            Location = new Point(24, 272),
            Width = 472,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _progress = new ProgressBar
        {
            Location = new Point(24, 294),
            Width = 472,
            Height = 16
        };

        _btnInstall = new Button
        {
            Text = "Installer",
            Location = new Point(304, 328),
            Width = 96,
            Height = 32,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += async (_, __) => await InstallerAsync();

        _btnCancel = new Button
        {
            Text = "Fermer",
            Location = new Point(408, 328),
            Width = 88,
            Height = 32
        };
        _btnCancel.Click += (_, __) => Close();

        Controls.AddRange(new Control[]
        {
            lblPath, _txtDir, _btnBrowse, _cbDesktop, _cbStartMenu, _cbLaunch,
            _lblStatus, _progress, _btnInstall, _btnCancel
        });
    }

    async Task InstallerAsync()
    {
        if (_done) { Close(); return; }
        var cible = _txtDir.Text.Trim();
        if (string.IsNullOrEmpty(cible)) return;

        _btnInstall.Enabled = false;
        _btnBrowse.Enabled = false;
        _txtDir.Enabled = false;
        _lblStatus.Text = "Extraction des fichiers…";

        try
        {
            bool mkDesktop = _cbDesktop.Checked;
            bool mkStart = _cbStartMenu.Checked;

            await Task.Run(() =>
            {
                Directory.CreateDirectory(cible);
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                {
                    if (stream == null) throw new InvalidOperationException("Archive d'installation introuvable.");
                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        int total = Math.Max(1, zip.Entries.Count);
                        int idx = 0;
                        foreach (var entry in zip.Entries)
                        {
                            idx++;
                            var destPath = Path.GetFullPath(Path.Combine(cible, entry.FullName));
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                entry.ExtractToFile(destPath, true);
                            }
                            int pct = Math.Min(90, idx * 90 / total);
                            Invoke((Action)(() => _progress.Value = pct));
                        }
                    }
                }

                var exePath = Path.Combine(cible, "Chatp2pWPF.exe");
                var icoPath = Path.Combine(cible, "app.ico");
                if (!File.Exists(icoPath)) icoPath = exePath;

                var desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ChatP2P Desktop.lnk");
                var startDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "ChatP2P Desktop");
                var startLink = Path.Combine(startDir, "ChatP2P Desktop.lnk");
                var uninstBat = Path.Combine(cible, "Desinstaller.bat");

                File.WriteAllText(uninstBat,
                    "@echo off\r\n" +
                    "taskkill /f /im Chatp2pWPF.exe >nul 2>&1\r\n" +
                    "del /q \"" + desktopLink + "\" >nul 2>&1\r\n" +
                    "rmdir /s /q \"" + startDir + "\" >nul 2>&1\r\n" +
                    "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ChatP2PDesktop\" /f >nul 2>&1\r\n" +
                    "start /b \"\" cmd /c \"timeout /t 1 >nul & rmdir /s /q \\\"" + cible + "\\\"\"\r\n");

                if (mkDesktop)
                    CreerRaccourci(desktopLink, exePath, cible, icoPath, "ChatP2P Desktop — Messagerie P2P");
                if (mkStart)
                {
                    Directory.CreateDirectory(startDir);
                    CreerRaccourci(startLink, exePath, cible, icoPath, "ChatP2P Desktop — Messagerie P2P");
                    CreerRaccourci(Path.Combine(startDir, "Désinstaller ChatP2P Desktop.lnk"), uninstBat, cible, icoPath, "Désinstaller ChatP2P Desktop");
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChatP2PDesktop"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "ChatP2P Desktop");
                        key.SetValue("DisplayVersion", "2.0.0");
                        key.SetValue("Publisher", "JoHn28-3");
                        key.SetValue("DisplayIcon", exePath);
                        key.SetValue("InstallLocation", cible);
                        key.SetValue("UninstallString", "cmd /c \"" + uninstBat + "\"");
                    }
                }
            });

            _progress.Value = 100;
            _lblStatus.Text = "Installation terminée avec succès !";
            _lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            _done = true;

            if (_cbLaunch.Checked)
            {
                var exe = Path.Combine(cible, "Chatp2pWPF.exe");
                if (File.Exists(exe))
                    Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = cible, UseShellExecute = true });
                Close();
            }
            else
            {
                _btnInstall.Text = "Terminer";
                _btnInstall.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Erreur : " + ex.Message;
            _lblStatus.ForeColor = Color.Firebrick;
            _btnInstall.Enabled = true;
            _btnBrowse.Enabled = true;
            _txtDir.Enabled = true;
        }
    }

    static void CreerRaccourci(string lnkPath, string target, string workDir, string iconPath, string desc)
    {
        var t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        dynamic shell = Activator.CreateInstance(t);
        dynamic sc = shell.CreateShortcut(lnkPath);
        sc.TargetPath = target;
        sc.WorkingDirectory = workDir;
        sc.IconLocation = iconPath + ",0";
        sc.Description = desc;
        sc.Save();
    }
}
