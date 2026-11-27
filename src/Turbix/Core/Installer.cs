using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>
    /// Instalar y desinstalar Turbix, sin herramientas externas.
    ///
    /// - El instalador (Turbix-Setup.exe) es el mismo programa compilado con -p:TurbixSetup=true: lleva dentro el
    ///   Turbix.exe normal y solo muestra la ventana de instalación (Views/SetupWindow).
    /// - Instalar = copiar Turbix.exe a «Archivos de programa\Turbix», crear los accesos directos y anotarlo en la
    ///   lista de aplicaciones de Windows.
    /// - Desinstalar = «Turbix.exe --uninstall» (lo llama Windows desde Configuración › Aplicaciones). Como un
    ///   programa no puede borrar su propio exe mientras corre, se copia a %ProgramData%\Turbix y termina desde ahí;
    ///   esa copia queda marcada para que Windows la borre en el siguiente reinicio.
    /// - Nunca se borran las copias de seguridad ni los registros de %ProgramData%\Turbix: hacen falta para
    ///   deshacer los cambios aplicados a Windows si se vuelve a instalar.
    /// </summary>
    public static class Installer
    {
        public const string UninstallArg = "--uninstall";
        public const string CleanupArg = "--uninstall-cleanup";
        public const string InstanceMutex = @"Global\Turbix.SingleInstance";

        private const string PayloadResource = "Turbix.Payload.exe";
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TURBiX";
        private const string ExeName = "TURBiX.exe";
        private const string ShortcutName = "TURBiX.lnk";

        public static string InstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TURBiX");

        public static string InstalledExe => Path.Combine(InstallDir, ExeName);

        private static string StartMenuShortcut =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), ShortcutName);

        private static string DesktopShortcut =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), ShortcutName);

        private static string CurrentExe => Process.GetCurrentProcess().MainModule.FileName;

        /// <summary>true si este exe trae dentro el Turbix.exe para instalar.</summary>
        public static bool HasPayload =>
            Assembly.GetExecutingAssembly().GetManifestResourceInfo(PayloadResource) != null;

        /// <summary>Versión instalada según la lista de aplicaciones de Windows, o null si no está instalado.</summary>
        public static string InstalledVersion()
        {
            try
            {
                using (var root = OpenMachine())
                using (var key = root.OpenSubKey(UninstallKey))
                    return key?.GetValue("DisplayVersion") as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>true si hay un Turbix abierto (aunque esté solo junto al reloj): su exe no se puede reemplazar ni borrar.</summary>
        public static bool IsTurbixRunning()
        {
            try
            {
                if (!Mutex.TryOpenExisting(InstanceMutex, out Mutex m)) return false;
                m.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ instalar

        /// <summary>Instala o actualiza. Devuelve null si salió bien, o el mensaje de error.</summary>
        public static string Install(bool desktopShortcut)
        {
            try
            {
                Directory.CreateDirectory(InstallDir);

                // Primero a un archivo aparte: si la copia falla a mitad, el Turbix instalado queda intacto.
                string temp = InstalledExe + ".new";
                using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                {
                    if (src == null) return "payload";
                    using (var dst = File.Create(temp)) src.CopyTo(dst);
                }
                if (File.Exists(InstalledExe)) File.Delete(InstalledExe);
                File.Move(temp, InstalledExe);

                string description = L.T("app.tagline");
                // Se borran antes para que tomen el nombre actual (Windows conserva las mayúsculas del archivo viejo).
                TryDelete(StartMenuShortcut);
                if (desktopShortcut) TryDelete(DesktopShortcut);
                CreateShortcut(StartMenuShortcut, InstalledExe, InstallDir, description);
                if (desktopShortcut) CreateShortcut(DesktopShortcut, InstalledExe, InstallDir, description);

                Register();

                // Si «Abrir Turbix al iniciar Windows» estaba activado con una copia portátil, que abra la instalada.
                if (SelfStartup.TaskExists()) SelfStartup.Enable(InstalledExe);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void Register()
        {
            long sizeKb = 0;
            try { sizeKb = new FileInfo(InstalledExe).Length / 1024; } catch { }

            using (var root = OpenMachine())
            using (var key = root.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "TURBiX");
                key.SetValue("DisplayVersion", AppInfo.Version);
                key.SetValue("Publisher", "TURBiX");
                key.SetValue("DisplayIcon", InstalledExe + ",0");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("UninstallString", "\"" + InstalledExe + "\" " + UninstallArg);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        // ------------------------------------------------------------------ desinstalar

        /// <summary>
        /// Quita la tarea de inicio, los accesos directos, la entrada de la lista de aplicaciones y las preferencias.
        /// El exe se borra después, desde una copia temporal (ver <see cref="RunCleanup"/>).
        /// Devuelve null si salió bien, o el mensaje de error.
        /// </summary>
        public static string Uninstall()
        {
            try
            {
                SelfStartup.Disable();
                TryDelete(StartMenuShortcut);
                TryDelete(DesktopShortcut);
                using (var root = OpenMachine()) root.DeleteSubKeyTree(UninstallKey, false);
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\TURBiX", false); } catch { }

                bool runningInstalledCopy = string.Equals(CurrentExe, InstalledExe, StringComparison.OrdinalIgnoreCase);
                if (!runningInstalledCopy)
                {
                    // Se llamó desde otra copia (por ejemplo, la portátil): se puede borrar directamente.
                    RemoveFiles(InstallDir);
                    return null;
                }

                // La copia va a la carpeta de datos de Turbix (solo los administradores escriben ahí), no a la
                // carpeta temporal: es un exe que se ejecuta con permisos de administrador.
                AppPaths.Secure();
                string copy = Path.Combine(AppPaths.Root, "uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(CurrentExe, copy, true);
                Process.Start(new ProcessStartInfo(copy,
                    CleanupArg + " \"" + InstallDir + "\" " + Process.GetCurrentProcess().Id)
                {
                    UseShellExecute = false,
                    WorkingDirectory = AppPaths.Root,
                });
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Último paso, desde la copia temporal: espera a que termine el Turbix instalado, borra su exe y su carpeta
        /// (solo si quedó vacía) y deja la propia copia marcada para que Windows la borre al reiniciar.
        /// Devuelve la ruta que no se pudo borrar, o null si quedó todo limpio.
        /// </summary>
        public static string RunCleanup(string dir, int parentPid)
        {
            try
            {
                using (var parent = Process.GetProcessById(parentPid)) parent.WaitForExit(15000);
            }
            catch
            {
                // ya terminó
            }

            string leftover = null;
            // Solo se toca la carpeta de instalación conocida, nunca una ruta cualquiera que llegue por argumento.
            if (string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), InstallDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < 20; i++)
                {
                    leftover = RemoveFiles(InstallDir);
                    if (leftover == null) break;
                    Thread.Sleep(500);
                }
            }

            try { MoveFileEx(CurrentExe, null, MoveFileDelayUntilReboot); } catch { }
            return leftover;
        }

        /// <summary>Borra Turbix.exe y, si no queda nada más, la carpeta. Nunca borra otros archivos.</summary>
        private static string RemoveFiles(string dir)
        {
            string exe = Path.Combine(dir, ExeName);
            try
            {
                if (File.Exists(exe)) File.Delete(exe);
                TryDelete(exe + ".new");
            }
            catch
            {
                return exe;
            }
            try
            {
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir, false);
            }
            catch
            {
                // carpeta en uso (un Explorador abierto ahí): queda vacía, sin importancia
            }
            return null;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        /// <summary>La lista de aplicaciones se lee de la vista de 64 bits del registro cuando Windows es de 64 bits.</summary>
        private static RegistryKey OpenMachine() =>
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);

        private const int MoveFileDelayUntilReboot = 4;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

        // ------------------------------------------------------------------ accesos directos (.lnk)

        /// <summary>
        /// El objeto de Windows que crea accesos directos solo funciona en un hilo «STA»; la instalación corre en un
        /// hilo de trabajo, así que se le da uno propio.
        /// </summary>
        private static void CreateShortcut(string lnkPath, string target, string workingDir, string description)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try { CreateShortcutCore(lnkPath, target, workingDir, description); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
            if (error != null) throw new IOException(error.Message, error);
        }

        private static void CreateShortcutCore(string lnkPath, string target, string workingDir, string description)
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(target);
                link.SetWorkingDirectory(workingDir);
                link.SetDescription(description);
                link.SetIconLocation(target, 0);
                Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
                ((IPersistFile)link).Save(lnkPath, false);
            }
            finally
            {
                Marshal.ReleaseComObject(link);
            }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
