using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Views
{
    /// <summary>
    /// Icono de Turbix junto al reloj (área de notificación) y notificaciones de Windows.
    ///
    /// - Doble clic: abre Turbix. Clic derecho: menú con Abrir, Liberar memoria, Limpiar temporales, Ajustes y Salir.
    /// - Las notificaciones usan el globo del icono (Shell_NotifyIcon), que Windows 10 y 11 muestran como una
    ///   notificación normal con el nombre y el icono de Turbix, y que respetan «No molestar».
    /// - Si el usuario ocultó el icono en Ajustes, aparece solo mientras se ve una notificación.
    /// </summary>
    public static class TrayIcon
    {
        private static System.Windows.Forms.NotifyIcon _icon;
        private static DispatcherTimer _hideTimer;
        private static ContextMenu _menu;
        private static bool _persistent;

        public static event Action OpenRequested;
        public static event Action SettingsRequested;
        public static event Action CleanMemoryRequested;
        public static event Action CleanTempRequested;
        public static event Action ExitRequested;

        /// <summary>Si el menú debe deshabilitar las acciones (hay una tarea en curso).</summary>
        public static Func<bool> IsBusy { get; set; } = () => false;

        /// <summary>Mostrar u ocultar el icono de forma permanente (preferencia «Mostrar el icono junto al reloj»).</summary>
        public static void SetPersistent(bool visible)
        {
            _persistent = visible;
            if (visible)
            {
                if (EnsureIcon()) _icon.Visible = true;
                _hideTimer?.Stop();
            }
            else if (_icon != null)
            {
                _icon.Visible = false;
            }
        }

        public static bool IsPersistent => _persistent && _icon != null && _icon.Visible;

        /// <summary>Notificación de Windows. Devuelve false si Windows no permitió mostrarla.</summary>
        public static bool Notify(ToastMessage m)
        {
            try
            {
                if (!EnsureIcon()) return false;
                _icon.Visible = true;
                _icon.ShowBalloonTip(6000, m.Title, string.IsNullOrEmpty(m.Text) ? " " : m.Text,
                    m.Kind == ToastKind.Warning ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.None);

                if (!_persistent)
                {
                    // Icono temporal: quitarlo al rato aunque Windows no avise que la notificación se cerró.
                    if (_hideTimer == null)
                    {
                        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
                        _hideTimer.Tick += (s, e) => HideTemporary();
                    }
                    _hideTimer.Stop();
                    _hideTimer.Start();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("shell.notify.error", ex.Message));
                return false;
            }
        }

        /// <summary>Al salir de Turbix: que no quede un icono fantasma junto al reloj.</summary>
        public static void Dispose()
        {
            _hideTimer?.Stop();
            if (_menu != null) _menu.IsOpen = false;
            if (_icon == null) return;
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }

        // ------------------------------------------------------------------ interno

        private static bool EnsureIcon()
        {
            if (_icon != null) return _icon.Icon != null;
            _icon = new System.Windows.Forms.NotifyIcon { Text = "TURBiX" };
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Turbix.ico"));
                if (res != null)
                    using (var s = res.Stream)
                        _icon.Icon = new System.Drawing.Icon(s, System.Windows.Forms.SystemInformation.SmallIconSize);
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("shell.notify.error", ex.Message));
            }
            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) OpenRequested?.Invoke();
            };
            _icon.MouseUp += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right) ShowMenu();
            };
            _icon.BalloonTipClicked += (s, e) => { OpenRequested?.Invoke(); HideTemporary(); };
            _icon.BalloonTipClosed += (s, e) => HideTemporary();
            return _icon.Icon != null;
        }

        private static void HideTemporary()
        {
            _hideTimer?.Stop();
            if (!_persistent && _icon != null) _icon.Visible = false;
        }

        // ------------------------------------------------------------------ menú

        /// <summary>Solo para las capturas de documentación (--render): dibuja el menú en un PNG.</summary>
        public static async System.Threading.Tasks.Task RenderMenuAsync(string path)
        {
            var menu = BuildMenu();
            menu.Placement = PlacementMode.Absolute;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 0;
            menu.IsOpen = true;
            await System.Threading.Tasks.Task.Delay(500);
            menu.UpdateLayout();
            int w = (int)Math.Ceiling(menu.ActualWidth), h = (int)Math.Ceiling(menu.ActualHeight);
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bmp.Render(menu);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var fs = System.IO.File.Create(path)) enc.Save(fs);
            menu.IsOpen = false;
        }

        private static void ShowMenu()
        {
            if (_menu == null) _menu = BuildMenu();
            // Con un diálogo esperando respuesta (confirmar un cambio, salir…) solo se puede volver a la ventana:
            // si no, se podrían cruzar dos tareas o cerrar Turbix a mitad de una.
            bool dialog = Dialogs.IsOpen;
            bool busy = IsBusy() || dialog;
            foreach (var item in _menu.Items)
            {
                if (!(item is MenuItem mi)) continue;
                if (mi.Tag as string == "action") mi.IsEnabled = !busy;
                else if (mi.Tag as string == "nodialog") mi.IsEnabled = !dialog;
            }
            _menu.Placement = PlacementMode.MousePoint;
            _menu.IsOpen = true;
        }

        private static ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            menu.SetResourceReference(FrameworkElement.StyleProperty, "TrayMenu");

            var open = Item(L.T("tray.open"), "", () => OpenRequested?.Invoke());
            open.FontWeight = FontWeights.SemiBold;   // acción predeterminada (igual que el doble clic)
            menu.Items.Add(open);
            menu.Items.Add(Separator());
            menu.Items.Add(Item(L.T("tray.cleanMemory"), "", () => CleanMemoryRequested?.Invoke(), "action"));
            menu.Items.Add(Item(L.T("tray.cleanTemp"), "", () => CleanTempRequested?.Invoke(), "action"));
            menu.Items.Add(Separator());
            menu.Items.Add(Item(L.T("tray.settings"), "", () => SettingsRequested?.Invoke(), "nodialog"));
            menu.Items.Add(Separator());
            menu.Items.Add(Item(L.T("tray.exit"), "", () => ExitRequested?.Invoke(), "nodialog"));

            // Para que el menú se cierre al hacer clic fuera, su ventana tiene que pasar al frente.
            menu.Opened += (s, e) =>
            {
                if (PresentationSource.FromVisual(menu) is HwndSource src) SetForegroundWindow(src.Handle);
            };
            return menu;
        }

        private static MenuItem Item(string text, string glyph, Action action, string tag = null)
        {
            var icon = new TextBlock { Text = glyph, FontSize = 14 };
            icon.SetResourceReference(TextBlock.StyleProperty, "Icon");
            var item = new MenuItem { Header = text, Icon = icon, Tag = tag };
            item.SetResourceReference(FrameworkElement.StyleProperty, "TrayMenuItem");
            // Se ejecuta después de que el menú termine de cerrarse (los diálogos no deben abrirse encima del menú).
            item.Click += (s, e) => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, action);
            return item;
        }

        private static Separator Separator()
        {
            var sep = new Separator();
            sep.SetResourceReference(FrameworkElement.StyleProperty, "TrayMenuSeparator");
            return sep;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
