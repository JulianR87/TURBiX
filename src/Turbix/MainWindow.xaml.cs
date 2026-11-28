using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Themes;
using Turbix.ViewModels;
using Turbix.Views;

namespace Turbix
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private readonly DispatcherTimer _liveTimer;
        private bool _renderMode;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            // En pantallas chicas (1024x768, portátiles con escala alta) la ventana no debe salirse por arriba.
            var work = SystemParameters.WorkArea;
            if (Height > work.Height) Height = Math.Max(MinHeight, work.Height);
            if (Width > work.Width) Width = Math.Max(MinWidth, work.Width);
            _vm.NavigateRequested += Navigate;
            // Mientras hay una tarea en curso no se puede tocar nada por debajo, tampoco con el teclado.
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(MainViewModel.IsBusy)) return;
                PageHost.IsEnabled = !_vm.IsBusy;
                Sidebar.IsEnabled = !_vm.IsBusy;
            };
            Navigate("home");

            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _liveTimer.Tick += (s, e) => _vm.RefreshLiveInfo();

            Loaded += (s, e) => StartWork();
            StateChanged += (s, e) => UpdateMaximizedLayout();
            SourceInitialized += (s, e) =>
            {
                ApplyWindowAttributes();
                (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);
            };

            // Avisos breves (memoria liberada, etc.).
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _toastTimer.Tick += (s, e) => HideToast();
            Notifications.Raised += ShowToast;
            Closed += (s, e) => Notifications.Raised -= ShowToast;

            ThemeManager.Changed += OnThemeChanged;
            Closed += (s, e) => ThemeManager.Changed -= OnThemeChanged;
        }

        // ------------------------------------------------------------------ tema

        private void OnThemeChanged(object sender, EventArgs e) => ApplyWindowAttributes();

        private const int WM_SETTINGCHANGE = 0x001A;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Windows envía «ImmersiveColorSet» cuando el usuario cambia entre modo claro y oscuro.
            if (msg == WM_SETTINGCHANGE && lParam != IntPtr.Zero &&
                System.Runtime.InteropServices.Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
            {
                ThemeManager.OnSystemThemeChanged();
            }
            return IntPtr.Zero;
        }

        // ------------------------------------------------------------------ navegación

        private void OnNav(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            if (ReferenceEquals(sender, NavHome)) Navigate("home");
            else if (ReferenceEquals(sender, NavPrivacy)) Navigate("privacy");
            else if (ReferenceEquals(sender, NavCleanup)) Navigate("cleanup");
            else if (ReferenceEquals(sender, NavPerformance)) Navigate("performance");
            else if (ReferenceEquals(sender, NavNetwork)) Navigate("network");
            else if (ReferenceEquals(sender, NavApps)) Navigate("apps");
            else if (ReferenceEquals(sender, NavHealth)) Navigate("health");
            else if (ReferenceEquals(sender, NavTools)) Navigate("tools");
            else if (ReferenceEquals(sender, NavRestore)) Navigate("restore");
            else if (ReferenceEquals(sender, NavLog)) Navigate("log");
            else if (ReferenceEquals(sender, NavChangelog)) Navigate("changelog");
            else if (ReferenceEquals(sender, NavAbout)) Navigate("about");
            else if (ReferenceEquals(sender, NavSettings)) Navigate("settings");
        }

        private void Navigate(string page)
        {
            if (PageHost == null || page == null) return;

            // «performance-memory» = página «performance», pestaña «memory».
            string tab = null;
            int dash = page.IndexOf('-');
            if (dash > 0)
            {
                tab = page.Substring(dash + 1);
                page = page.Substring(0, dash);
            }

            if (!_pages.TryGetValue(page, out var view))
            {
                switch (page)
                {
                    case "privacy": view = new PrivacyView(); break;
                    case "cleanup": view = new CleanupView(); break;
                    case "performance": view = new PerformanceView(); break;
                    case "network": view = new NetworkView(); break;
                    case "apps": view = new AppsView(); break;
                    case "health": view = new HealthView(); break;
                    case "tools": view = new ToolsView(); break;
                    case "restore": view = new RestoreView(); break;
                    case "log": view = new LogView(); break;
                    case "changelog": view = new ChangelogView(); break;
                    case "about": view = new AboutView(); break;
                    case "settings": view = new SettingsView(); break;
                    default: view = new DashboardView(); break;
                }
                _pages[page] = view;
            }
            PageHost.Content = view;
            _currentPage = page;
            _vm.IsPerformanceVisible = page == "performance" && (IsVisible || _renderMode);
            if (page == "health") _vm.EnsureHealthLoaded();
            if (page == "apps") _vm.EnsureAppsLoaded();

            // Mantener marcada la opción del menú cuando se navega desde un enlace.
            RadioButton nav;
            switch (page)
            {
                case "privacy": nav = NavPrivacy; break;
                case "cleanup": nav = NavCleanup; break;
                case "performance": nav = NavPerformance; break;
                case "network": nav = NavNetwork; break;
                case "apps": nav = NavApps; break;
                case "health": nav = NavHealth; break;
                case "tools": nav = NavTools; break;
                case "restore": nav = NavRestore; break;
                case "log": nav = NavLog; break;
                case "changelog": nav = NavChangelog; break;
                case "about": nav = NavAbout; break;
                case "settings": nav = NavSettings; break;
                default: nav = NavHome; break;
            }
            if (nav != null && nav.IsChecked != true) nav.IsChecked = true;

            if (tab != null)
            {
                if (view is PerformanceView perf) perf.ShowTab(tab);
                else if (view is ToolsView tools) tools.ShowTab(tab);
                else if (view is AppsView apps) apps.ShowTab(tab);
                else if (view is NetworkView network) network.ShowTab(tab);
            }
        }

        // ------------------------------------------------------------------ ventana

        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximize(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        // ------------------------------------------------------------------ segundo plano (icono junto al reloj)

        private bool _started;
        private string _currentPage = "home";

        public MainViewModel ViewModel => _vm;

        /// <summary>Carga los datos y arranca los temporizadores (una sola vez), se vea la ventana o no.</summary>
        private async void StartWork()
        {
            if (_started || _renderMode) return;
            _started = true;
            SetupTray();
            _liveTimer.Start();
            await _vm.InitializeAsync();
        }

        /// <summary>Abierto al iniciar sesión con el icono junto al reloj: funciona sin mostrar la ventana.</summary>
        public void StartInBackground()
        {
            new WindowInteropHelper(this).EnsureHandle();
            StartWork();
            EnterBackground();
        }

        private void SetupTray()
        {
            TrayIcon.IsBusy = () => _vm.IsBusy;
            TrayIcon.OpenRequested += ShowFromBackground;
            TrayIcon.SettingsRequested += OpenSettings;
            TrayIcon.CleanMemoryRequested += OnTrayCleanMemory;
            TrayIcon.CleanTempRequested += OnTrayCleanTemp;
            TrayIcon.ExitRequested += App.RequestExit;
            TrayIcon.SetPersistent(_vm.ShowTrayIcon);
        }

        private async void OnTrayCleanMemory() => await _vm.CleanMemoryFromTrayAsync();

        private async void OnTrayCleanTemp() => await _vm.CleanTempFromTrayAsync();

        private void OpenSettings()
        {
            ShowFromBackground();
            Navigate("settings");
        }

        /// <summary>Vuelve a mostrar la ventana (doble clic en el icono, otra copia de Turbix que se abre, etc.).</summary>
        public void ShowFromBackground()
        {
            if (_renderMode) return;
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            // Windows a veces no deja pasar al frente a un programa que no estaba activo: se fuerza un instante.
            Topmost = true;
            Topmost = false;
            Focus();
            _liveTimer.Interval = TimeSpan.FromSeconds(5);
            _vm.IsPerformanceVisible = _currentPage == "performance";
            _vm.RefreshLiveInfo();
        }

        /// <summary>Esconde la ventana: Turbix sigue junto al reloj (limpieza automática, menú del icono).</summary>
        private void HideToBackground()
        {
            Hide();
            EnterBackground();
            if (!Settings.GetBool(MainViewModel.TrayHintSetting, false))
            {
                Settings.SetBool(MainViewModel.TrayHintSetting, true);
                Notifications.Show(new ToastMessage
                {
                    Kind = ToastKind.Info,
                    Title = L.T("tray.hint.title"),
                    Text = L.T("tray.hint.text"),
                });
            }
        }

        private void EnterBackground()
        {
            // Sin ventana a la vista basta con mirar la memoria de vez en cuando (para la limpieza automática).
            _liveTimer.Interval = TimeSpan.FromSeconds(30);
            _vm.IsPerformanceVisible = false;
        }

        private void UpdateMaximizedLayout()
        {
            if (WindowState == WindowState.Maximized)
            {
                // Con WindowChrome la ventana maximizada se sale unos píxeles de la pantalla.
                var r = SystemParameters.WindowResizeBorderThickness;
                Root.Margin = new Thickness(r.Left + 4, r.Top + 4, r.Right + 4, r.Bottom + 4);
                MaxButton.Content = "";
                MaxButton.ToolTip = L.T("shell.restoreDown");
            }
            else
            {
                Root.Margin = new Thickness(0);
                MaxButton.Content = "";
                MaxButton.ToolTip = L.T("shell.maximize");
            }
        }

        private void ApplyWindowAttributes()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = ThemeManager.IsLight ? 0 : 1;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));
                int round = 2; // DWMWCP_ROUND (solo Windows 11)
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
                // Borde de la ventana: COLORREF (0x00BBGGRR) del color «Stroke» de cada paleta.
                int border = ThemeManager.IsLight ? 0x00DAE2E4 : 0x00342822;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch
            {
                // Atributos no disponibles en versiones antiguas: sin importancia.
            }
        }

        // ------------------------------------------------------------------ avisos

        private readonly DispatcherTimer _toastTimer;

        private void ShowToast(ToastMessage m)
        {
            if (_renderMode) return;
            // Los avisos van como notificación de Windows. Solo si Windows no la deja mostrar,
            // se ven dentro de la ventana para que el resultado no se pierda.
            if (TrayIcon.Notify(m)) return;
            if (!IsVisible) ShowFromBackground();
            ShowToastInWindow(m);
        }

        private void ShowToastInWindow(ToastMessage m)
        {
            ToastTitle.Text = m.Title;
            ToastText.Text = m.Text ?? string.Empty;
            ToastText.Visibility = string.IsNullOrEmpty(m.Text) ? Visibility.Collapsed : Visibility.Visible;
            ToastIcon.Text = string.IsNullOrEmpty(m.Glyph) ? "" : m.Glyph;
            switch (m.Kind)
            {
                case ToastKind.Warning:
                    ToastIconBox.SetResourceReference(Border.BackgroundProperty, "WarningSoft");
                    ToastIcon.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
                    break;
                case ToastKind.Info:
                    ToastIconBox.SetResourceReference(Border.BackgroundProperty, "ChipNeutral");
                    ToastIcon.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                    break;
                default:
                    ToastIconBox.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                    ToastIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentText");
                    break;
            }

            bool wasVisible = Toast.Visibility == Visibility.Visible && Toast.Opacity > 0.5;
            Toast.Visibility = Visibility.Visible;
            if (!wasVisible)
            {
                var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                Toast.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                ToastShift.BeginAnimation(TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
            }
            _toastTimer.Interval = TimeSpan.FromSeconds(6);
            _toastTimer.Stop();
            if (!Toast.IsMouseOver) _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            if (Toast.Visibility != Visibility.Visible) return;
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (s, e) =>
            {
                if (Toast.Opacity < 0.05) Toast.Visibility = Visibility.Collapsed;
            };
            Toast.BeginAnimation(OpacityProperty, fade);
        }

        private void OnToastClose(object sender, RoutedEventArgs e) => HideToast();

        // Mientras el ratón está encima, el aviso no se cierra solo.
        private void OnToastMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _toastTimer.Stop();

        private void OnToastMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (Toast.Visibility != Visibility.Visible) return;
            _toastTimer.Interval = TimeSpan.FromSeconds(3);
            _toastTimer.Start();
        }

        /// <summary>
        /// La X (o Alt+F4): si así está configurado, esconde la ventana y Turbix sigue junto al reloj.
        /// Si no, es salir de Turbix, con su confirmación. Al apagar o cerrar sesión, se cierra sin preguntar.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!App.IsExiting && !_renderMode)
            {
                e.Cancel = true;
                if (_vm.CloseKeepsRunning && TrayIcon.IsPersistent) HideToBackground();
                else App.RequestExit();
            }
            base.OnClosing(e);
        }

        // ------------------------------------------------------------------ capturas

        /// <summary>
        /// Genera capturas de todas las páginas sin mostrar la ventana en pantalla
        /// (para documentación). Solo lee el estado del sistema; no modifica nada.
        /// </summary>
        public async void RenderScreenshots(string folder)
        {
            _renderMode = true;
            Directory.CreateDirectory(folder);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -30000;
            Top = -30000;
            ShowInTaskbar = false;
            ShowActivated = false;
            ThemeManager.Preview(ThemeMode.Dark);
            Show();
            try
            {
                await _vm.InitializeAsync();
                await _vm.AnalyzeAsync();
                await _vm.RefreshHealthAsync();
                await _vm.ScanRegistryAsync();
                await _vm.RefreshAppsAsync();
                foreach (var page in new[] { "home", "privacy", "cleanup", "restore", "log", "changelog", "about", "settings" })
                {
                    Navigate(page);
                    await Task.Delay(1200);
                    SaveRender(Path.Combine(folder, page + ".png"));
                }
                await RenderPerformanceTabs(folder, "");
                await RenderNetworkTabs(folder, "");

                // Las mismas páginas con la paleta clara.
                ThemeManager.Preview(ThemeMode.Light);
                foreach (var page in new[] { "home", "privacy", "cleanup", "restore", "log", "changelog", "about", "settings" })
                {
                    Navigate(page);
                    await Task.Delay(900);
                    SaveRender(Path.Combine(folder, page + "-light.png"));
                }
                await RenderPerformanceTabs(folder, "-light");
                await RenderNetworkTabs(folder, "-light");
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(folder, "error.txt"), ex.ToString());
            }
            Application.Current.Shutdown();
        }

        private async Task RenderNetworkTabs(string folder, string suffix)
        {
            Navigate("network");
            var view = (NetworkView)_pages["network"];
            foreach (var tab in new[] { "traffic", "connections", "ports", "blocks" })
            {
                view.ShowTab(tab);
                // Tráfico necesita dos tomas para calcular las velocidades.
                await Task.Delay(tab == "traffic" ? 4000 : 1800);
                SaveRender(Path.Combine(folder, "network-" + tab + suffix + ".png"));
            }
        }

        private async Task RenderPerformanceTabs(string folder, string suffix)
        {
            Navigate("performance");
            var view = (PerformanceView)_pages["performance"];
            foreach (var tab in new[] { "startup", "processes", "memory", "gaming" })
            {
                view.ShowTab(tab);
                // Procesos necesita dos tomas para calcular el uso de procesador.
                await Task.Delay(tab == "processes" ? 4500 : 1500);
                SaveRender(Path.Combine(folder, "performance-" + tab + suffix + ".png"));
            }
            Navigate("settings");
            await Task.Delay(600);
            if (_pages["settings"] is UserControl settings && settings.Content is ScrollViewer sv)
            {
                sv.ScrollToEnd();
                await Task.Delay(600);
                SaveRender(Path.Combine(folder, "settings-bottom" + suffix + ".png"));
                sv.ScrollToHome();
            }
            await TrayIcon.RenderMenuAsync(Path.Combine(folder, "tray-menu" + suffix + ".png"));
            SetupWindow.RenderTo(folder, suffix);

            Navigate("health");
            await Task.Delay(1200);
            SaveRender(Path.Combine(folder, "health" + suffix + ".png"));
            foreach (var tab in new[] { "store", "programs" })
            {
                Navigate("apps-" + tab);
                await Task.Delay(1200);
                SaveRender(Path.Combine(folder, "apps-" + tab + suffix + ".png"));
            }
            foreach (var tab in new[] { "repair", "registry", "windows" })
            {
                Navigate("tools-" + tab);
                await Task.Delay(1200);
                SaveRender(Path.Combine(folder, "tools-" + tab + suffix + ".png"));
            }
        }

        private void SaveRender(string path)
        {
            Root.UpdateLayout();
            int w = (int)Math.Ceiling(Root.ActualWidth), h = (int)Math.Ceiling(Root.ActualHeight);
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(Root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) enc.Save(fs);
        }
    }
}
