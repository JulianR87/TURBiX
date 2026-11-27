using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Views
{
    /// <summary>Ventana de Turbix-Setup.exe. La lógica está en Core/Installer.cs.</summary>
    public partial class SetupWindow : Window
    {
        private bool _working;

        public SetupWindow()
        {
            InitializeComponent();
            VersionRun.Text = AppInfo.Version;
            FolderText.Text = Installer.InstallDir;
            FolderText.ToolTip = Installer.InstallDir;

            string installed = Installer.InstalledVersion();
            if (!string.IsNullOrEmpty(installed))
            {
                ReplaceText.Text = installed == AppInfo.Version
                    ? L.F("setup.reinstall", installed)
                    : L.F("setup.replace", installed, AppInfo.Version);
                ReplaceText.Visibility = Visibility.Visible;
            }
        }

        private async void OnInstall(object sender, RoutedEventArgs e)
        {
            if (_working) return;

            // El Turbix abierto (quizá solo junto al reloj) tiene que cerrarse antes: su exe no se puede reemplazar.
            while (Installer.IsTurbixRunning())
                if (!Dialogs.Confirm(L.T("setup.running.title"), L.T("setup.running.text"), L.T("setup.retry"))) return;

            _working = true;
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            bool desktop = DesktopCheck.IsChecked == true;
            string error = await Task.Run(() => Installer.Install(desktop));
            _working = false;

            if (error != null)
            {
                InstallButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                Dialogs.Show(L.T("setup.error.title"), L.F("setup.error.text", error), DialogKind.Error);
                return;
            }
            ReadyPanel.Visibility = Visibility.Collapsed;
            DonePanel.Visibility = Visibility.Visible;
        }

        private void OnOpen(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Installer.InstalledExe) { WorkingDirectory = Installer.InstallDir });
            }
            catch (Exception ex)
            {
                Dialogs.Show(L.T("setup.error.title"), L.F("setup.error.text", ex.Message), DialogKind.Error);
                return;
            }
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_working) e.Cancel = true;   // no cortar la copia a mitad
            base.OnClosing(e);
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        /// <summary>Solo para las capturas de documentación (--render): dibuja los dos estados en PNG.</summary>
        public static void RenderTo(string folder, string suffix)
        {
            foreach (bool done in new[] { false, true })
            {
                var w = new SetupWindow
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -30000,
                    Top = -30000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                };
                if (done)
                {
                    w.ReadyPanel.Visibility = Visibility.Collapsed;
                    w.DonePanel.Visibility = Visibility.Visible;
                }
                w.Show();
                w.UpdateLayout();
                var bmp = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth), (int)Math.Ceiling(w.ActualHeight),
                                                 96, 96, PixelFormats.Pbgra32);
                bmp.Render(w);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(Path.Combine(folder, (done ? "setup-done" : "setup") + suffix + ".png")))
                    enc.Save(fs);
                w.Close();
            }
        }
    }
}
