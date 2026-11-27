using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Themes;
using Turbix.ViewModels;
using Turbix.Views;

namespace Turbix
{
    public partial class App : Application
    {
        private Mutex _singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Idioma de Windows (o --lang para las capturas de prueba). Fechas y números en el mismo idioma que los textos.
            L.Init(GetArg(e.Args, "--lang"));
            var culture = L.Culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            string renderDir = GetArg(e.Args, "--render");
            if (renderDir != null)
            {
                Logger.FileEnabled = false;
                Settings.Disabled = true;
                ThemeManager.Initialize(persist: false);
                var w = new MainWindow();
                w.RenderScreenshots(renderDir);
                return;
            }

            // Instalador y desinstalador (ver Core/Installer.cs). No abren la ventana principal.
            // La primera ventana que se crea no debe cerrar el programa al cerrarse (son diálogos).
#if SETUP
            if (Installer.HasPayload)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Logger.FileEnabled = false;
                ThemeManager.Initialize(persist: false);
                var setup = new SetupWindow();
                MainWindow = setup;
                setup.Closed += (s, a) => Shutdown();
                setup.Show();
                return;
            }
#else
            string cleanupDir = GetArg(e.Args, Installer.CleanupArg);
            if (cleanupDir != null)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Logger.FileEnabled = false;
                ThemeManager.Initialize(persist: false);
                int pid = 0;
                int at = Array.FindIndex(e.Args, a => string.Equals(a, Installer.CleanupArg, StringComparison.OrdinalIgnoreCase));
                if (at >= 0 && at + 2 < e.Args.Length) int.TryParse(e.Args[at + 2], out pid);
                string leftover = Installer.RunCleanup(cleanupDir, pid);
                string done = L.F("uninstall.done.text", AppPaths.Root);
                if (leftover != null) done += "\n\n" + L.F("uninstall.done.leftover", leftover);
                Dialogs.Show(L.T("uninstall.done.title"), done, leftover == null ? DialogKind.Success : DialogKind.Warning);
                Shutdown();
                return;
            }
            if (e.Args.Any(a => string.Equals(a, Installer.UninstallArg, StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Logger.FileEnabled = false;
                ThemeManager.Initialize(persist: false);
                RunUninstall();
                Shutdown();
                return;
            }
#endif

#if SELFTEST
            // Pruebas sueltas en la VM: tarea de inicio y notificación de Windows.
            string taskCmd = GetArg(e.Args, "--selftest-startup");   // on | off | status
            if (taskCmd != null)
            {
                string err = taskCmd == "on" ? SelfStartup.Enable() : taskCmd == "off" ? SelfStartup.Disable() : null;
                System.IO.File.WriteAllText(GetArg(e.Args, "--out") ?? "startup-task.txt",
                    taskCmd + ": " + (err ?? "ok") + " | habilitada=" + SelfStartup.IsEnabled());
                Shutdown(err == null ? 0 : 1);
                return;
            }
            if (e.Args.Contains("--selftest-notify"))
            {
                ThemeManager.Initialize(persist: false);
                TrayIcon.Notify(new ToastMessage
                {
                    Kind = ToastKind.Success,
                    Title = L.F("memory.toast.freed.title", FileCleaner.FormatSize(1331691520)),
                    Text = L.F("memory.toast.freed.text", FileCleaner.FormatSize(2468000000)),
                });
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                t.Tick += (s2, a2) => { TrayIcon.Dispose(); Shutdown(0); };
                t.Start();
                return;
            }

            string testDir = GetArg(e.Args, "--selftest");
            if (testDir != null)
            {
                AppPaths.Secure();
                Logger.Initialize();
                Settings.Disabled = true;
                ThemeManager.Initialize(persist: false);
                Shutdown(Testing.SelfTest.Run(testDir));
                return;
            }
#endif

            // Abierto por la tarea «al iniciar sesión» (ver Core/SelfStartup.cs): arranca sin ventana.
            bool atLogon = e.Args.Any(a => string.Equals(a, SelfStartup.StartupArg, StringComparison.OrdinalIgnoreCase));

            _singleInstance = new Mutex(true, Installer.InstanceMutex, out bool created);
            if (!created)
            {
                // Ya hay un Turbix abierto (quizá solo junto al reloj): se le pide que muestre su ventana.
                if (!atLogon && !SignalRunningInstance())
                    Dialogs.Show(L.T("app.alreadyOpen.title"), L.T("app.alreadyOpen.text"), DialogKind.Info);
                Shutdown();
                return;
            }

            // Turbix puede seguir funcionando con la ventana oculta: solo termina cuando se pide salir.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SessionEnding += (s, a) => IsExiting = true;   // al cerrar sesión o apagar, no preguntar ni esconderse

            AppPaths.Secure();
            Logger.Initialize();
            ThemeManager.Initialize(persist: true);
            DispatcherUnhandledException += OnUnhandled;
            AppDomain.CurrentDomain.UnhandledException += (s, a) => Logger.Error(L.F("app.unhandled", a.ExceptionObject));

            var main = new MainWindow();
            MainWindow = main;
            ListenForShowRequests(main);

            if (!atLogon)
            {
                main.Show();
                return;
            }

            if (Settings.GetBool(MainViewModel.TrayIconSetting, true))
            {
                // Con el icono junto al reloj, al iniciar sesión no se muestra ninguna ventana.
                main.StartInBackground();
                Logger.Info(L.T("startup.self.log.startedTray"));
                return;
            }

            main.ShowActivated = false;
            main.WindowState = WindowState.Minimized;
            main.Show();
            // Cuando a Turbix lo abre otro programa (aquí, el Programador de tareas), Windows puede ignorar el
            // primer «mostrar minimizado» y usar el modo que indicó ese programa. Se comprueba y se corrige.
            main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(main).Handle;
                if (hwnd != IntPtr.Zero && !Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_SHOWMINNOACTIVE);
                Logger.Info(L.T("startup.self.log.started"));
            }));
        }

        // ------------------------------------------------------------------ una sola copia abierta

        // Uno por sesión de Windows: si otro usuario del equipo ya tiene Turbix abierto, no se le abre la ventana
        // a él; a quien lo intenta le sale el aviso «ya está abierto».
        private static readonly string ShowSignalName =
            @"Global\Turbix.Show." + System.Diagnostics.Process.GetCurrentProcess().SessionId;
        private EventWaitHandle _showSignal;
        private RegisteredWaitHandle _showWait;

        /// <summary>La copia que ya está abierta muestra su ventana cuando otra intenta abrirse.</summary>
        private void ListenForShowRequests(MainWindow main)
        {
            try
            {
                _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
                _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal,
                    (state, timedOut) => Dispatcher.BeginInvoke(new Action(main.ShowFromBackground)), null, Timeout.Infinite, false);
            }
            catch
            {
                // sin esto, abrir Turbix otra vez muestra el aviso de siempre
            }
        }

        private static bool SignalRunningInstance()
        {
            try
            {
                using (var signal = EventWaitHandle.OpenExisting(ShowSignalName))
                {
                    // Esta copia la abrió el usuario, así que puede ceder el primer plano a la que ya estaba abierta.
                    AllowSetForegroundWindow(-1);
                    return signal.Set();
                }
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        // ------------------------------------------------------------------ salir

        /// <summary>true mientras Turbix se cierra de verdad (la ventana no se esconde ni pregunta).</summary>
        public static bool IsExiting { get; private set; }

        /// <summary>
        /// Salir de Turbix (menú del icono, o la X si se eligió no seguir en segundo plano).
        /// Espera a que termine una tarea en curso y, si así está configurado, pide confirmación.
        /// </summary>
        public static void RequestExit()
        {
            if (IsExiting || !(Current is App app)) return;
            var vm = (app.MainWindow as MainWindow)?.ViewModel;
            if (vm != null && vm.IsBusy)
            {
                (app.MainWindow as MainWindow)?.ShowFromBackground();
                Dialogs.Show(L.T("shell.busyClose.title"), L.T("shell.busyClose.text"), DialogKind.Info);
                return;
            }
            if (Settings.GetBool(MainViewModel.ConfirmExitSetting, true))
            {
                string text = vm != null && vm.AutoRamEnabled ? L.T("exit.confirm.textAuto") : L.T("exit.confirm.text");
                if (!Dialogs.Confirm(L.T("exit.confirm.title"), text, L.T("exit.confirm.button"))) return;
                // Mientras el diálogo estaba abierto pudo empezar una tarea (o haberse pedido salir dos veces).
                if (IsExiting) return;
                if (vm != null && vm.IsBusy)
                {
                    Dialogs.Show(L.T("shell.busyClose.title"), L.T("shell.busyClose.text"), DialogKind.Info);
                    return;
                }
            }
            IsExiting = true;
            Logger.Info(L.T("exit.log"));
            app.Shutdown();
        }

        /// <summary>Volver a abrir Turbix (para aplicar el idioma). La copia nueva arranca cuando esta suelta el bloqueo.</summary>
        public static void Restart()
        {
            if (!(Current is App app)) return;
            IsExiting = true;
            try
            {
                app._showWait?.Unregister(null);
                app._showSignal?.Dispose();
                app._showSignal = null;
                app._singleInstance?.ReleaseMutex();
                app._singleInstance?.Dispose();
                app._singleInstance = null;
                System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            }
            catch (Exception ex)
            {
                // No se pudo abrir la copia nueva: esta sigue abierta, como estaba.
                Logger.Warn(L.F("settings.language.restartFailed", ex.Message));
                try { app._singleInstance = new Mutex(true, Installer.InstanceMutex, out _); } catch { }
                if (app.MainWindow is MainWindow main) app.ListenForShowRequests(main);
                IsExiting = false;
                Dialogs.Show(L.T("main.error.title"), L.F("settings.language.restartFailed", ex.Message), DialogKind.Warning);
                return;
            }
            app.Shutdown();
        }

        /// <summary>«Turbix.exe --uninstall»: lo llama Windows desde Configuración › Aplicaciones.</summary>
        private static void RunUninstall()
        {
            if (!Dialogs.Confirm(L.T("uninstall.confirm.title"), L.F("uninstall.confirm.text", AppPaths.Root),
                                 L.T("uninstall.confirm.button"))) return;

            // El Turbix abierto (quizá solo junto al reloj) tiene que cerrarse antes: su exe no se puede borrar.
            while (Installer.IsTurbixRunning())
                if (!Dialogs.Confirm(L.T("setup.running.title"), L.T("setup.running.text"), L.T("setup.retry"))) return;

            string error = Installer.Uninstall();
            if (error != null)
            {
                Dialogs.Show(L.T("uninstall.error.title"), error, DialogKind.Error);
                return;
            }
            // Si se desinstaló desde otra copia, no hay paso final en la carpeta temporal: se avisa aquí.
            if (!string.Equals(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, Installer.InstalledExe,
                               StringComparison.OrdinalIgnoreCase))
                Dialogs.Show(L.T("uninstall.done.title"), L.F("uninstall.done.text", AppPaths.Root), DialogKind.Success);
        }

        private static string GetArg(string[] args, string name)
        {
            int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error(L.F("app.unexpected.log", e.Exception));
            Dialogs.Show(L.T("app.unexpected.title"), L.F("app.unexpected.text", e.Exception.Message), DialogKind.Error);
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TrayIcon.Dispose();
            try { _showWait?.Unregister(null); _showSignal?.Dispose(); } catch { }
            try { _singleInstance?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }
    }
}
