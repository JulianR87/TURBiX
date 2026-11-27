using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    public enum StartupKind { RegistryRun, StartupFolder, StoreApp }

    public sealed class StartupEntry
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Publisher { get; set; }
        public string Command { get; set; }

        /// <summary>Ejecutable real (para el icono y el fabricante). Puede ser null.</summary>
        public string FilePath { get; set; }

        public StartupKind Kind { get; set; }
        public string SourceText { get; set; }
        public bool Enabled { get; set; }

        /// <summary>Componentes de seguridad de Windows: se muestran pero no se pueden desactivar.</summary>
        public bool IsProtected { get; set; }

        // Dónde guarda Windows el estado activado/desactivado.
        public RegistryHive Hive { get; set; }
        public string StateSubKey { get; set; }
        public string StateName { get; set; }
    }

    /// <summary>
    /// Programas que se inician solos con Windows.
    ///
    /// Activar o desactivar usa el mismo mecanismo que el Administrador de tareas
    /// (claves «StartupApproved» y el estado de las apps de Microsoft Store): no se borra
    /// nada, el programa sigue instalado y se puede volver a activar cuando se quiera.
    /// </summary>
    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32Key = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
        private const string StoreKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

        public static List<StartupEntry> List()
        {
            var list = new List<StartupEntry>();
            Try(() => ReadRun(list, RegistryHive.CurrentUser, RunKey, "Run", L.T("startup.source.runUser")));
            Try(() => ReadRun(list, RegistryHive.LocalMachine, RunKey, "Run", L.T("startup.source.runAll")));
            Try(() => ReadRun(list, RegistryHive.LocalMachine, Run32Key, "Run32", L.T("startup.source.run32")));
            Try(() => ReadFolder(list, RegistryHive.CurrentUser, Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                L.T("startup.source.folderUser")));
            Try(() => ReadFolder(list, RegistryHive.LocalMachine, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                L.T("startup.source.folderAll")));
            Try(() => ReadStoreApps(list));

            return list.OrderByDescending(e => e.Enabled).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void SetEnabled(StartupEntry entry, bool enabled)
        {
            if (entry.IsProtected && !enabled)
                throw new InvalidOperationException(L.T("startup.error.protected"));

            using (var root = RegistryHelper.OpenBase(entry.Hive))
            using (var key = root.CreateSubKey(entry.StateSubKey))
            {
                if (key == null) throw new InvalidOperationException(L.T("startup.error.openKey"));
                if (entry.Kind == StartupKind.StoreApp)
                {
                    // 2 = activada, 1 = desactivada por el usuario
                    key.SetValue(entry.StateName, enabled ? 2 : 1, RegistryValueKind.DWord);
                }
                else
                {
                    // Formato del Administrador de tareas: 02 = activado, 03 = desactivado + fecha (FILETIME).
                    var data = new byte[12];
                    data[0] = (byte)(enabled ? 2 : 3);
                    if (!enabled) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
                    key.SetValue(entry.StateName, data, RegistryValueKind.Binary);
                }
            }
            entry.Enabled = enabled;
        }

        // ------------------------------------------------------------------ lectura

        private static void ReadRun(List<StartupEntry> list, RegistryHive hive, string runKey, string approvedName, string source)
        {
            using (var root = RegistryHelper.OpenBase(hive))
            using (var key = root.OpenSubKey(runKey))
            {
                if (key == null) return;
                string stateKey = ApprovedKey + "\\" + approvedName;
                foreach (string name in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    string command = Convert.ToString(key.GetValue(name));
                    if (string.IsNullOrWhiteSpace(command)) continue;

                    var entry = new StartupEntry
                    {
                        Name = name, Command = command, Kind = StartupKind.RegistryRun, SourceText = source,
                        Hive = hive, StateSubKey = stateKey, StateName = name,
                        FilePath = ExtractPath(command),
                        Enabled = ReadApproved(root, stateKey, name),
                    };
                    Describe(entry);
                    list.Add(entry);
                }
            }
        }

        private static void ReadFolder(List<StartupEntry> list, RegistryHive hive, string folder, string source)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            string stateKey = ApprovedKey + @"\StartupFolder";
            using (var root = RegistryHelper.OpenBase(hive))
            {
                foreach (string file in Directory.GetFiles(folder))
                {
                    string fileName = Path.GetFileName(file);
                    if (string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                    string target = file;
                    if (string.Equals(Path.GetExtension(file), ".lnk", StringComparison.OrdinalIgnoreCase))
                        target = ResolveShortcut(file) ?? file;

                    var entry = new StartupEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file), Command = target, Kind = StartupKind.StartupFolder,
                        SourceText = source, Hive = hive, StateSubKey = stateKey, StateName = fileName,
                        FilePath = target, Enabled = ReadApproved(root, stateKey, fileName),
                    };
                    Describe(entry);
                    list.Add(entry);
                }
            }
        }

        private static void ReadStoreApps(List<StartupEntry> list)
        {
            using (var root = RegistryHelper.OpenBase(RegistryHive.CurrentUser))
            using (var apps = root.OpenSubKey(StoreKey))
            {
                if (apps == null) return;
                foreach (string package in apps.GetSubKeyNames())
                {
                    using (var pkg = apps.OpenSubKey(package))
                    {
                        if (pkg == null) continue;
                        foreach (string task in pkg.GetSubKeyNames())
                        {
                            using (var t = pkg.OpenSubKey(task))
                            {
                                object state = t?.GetValue("State");
                                if (state == null) continue;
                                int s = Convert.ToInt32(state);
                                if (s < 0 || s > 2) continue;   // 3 y 4 = fijado por directiva: no se toca
                                list.Add(new StartupEntry
                                {
                                    Name = StoreAppName(package), Description = L.T("startup.storeApp.description"),
                                    Command = package, Kind = StartupKind.StoreApp, SourceText = L.T("startup.source.store"),
                                    Hive = RegistryHive.CurrentUser,
                                    StateSubKey = StoreKey + "\\" + package + "\\" + task, StateName = "State",
                                    Enabled = s == 2,
                                });
                            }
                        }
                    }
                }
            }
        }

        private static bool ReadApproved(RegistryKey root, string stateKey, string name)
        {
            using (var key = root.OpenSubKey(stateKey))
            {
                // Sin registro = nunca se desactivó. El primer byte par significa «activado».
                return !(key?.GetValue(name) is byte[] data) || data.Length == 0 || (data[0] & 1) == 0;
            }
        }

        // ------------------------------------------------------------------ detalles

        private static void Describe(StartupEntry entry)
        {
            try
            {
                if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath)) return;
                var info = FileVersionInfo.GetVersionInfo(entry.FilePath);
                entry.Description = Clean(info.FileDescription);
                entry.Publisher = Clean(info.CompanyName);
            }
            catch
            {
                // sin información de versión: se muestra solo el nombre
            }

            string all = (entry.Name + " " + entry.Command).ToLowerInvariant();
            if (all.Contains("securityhealth") || all.Contains("windows defender") || all.Contains("msascuil"))
                entry.IsProtected = true;
        }

        private static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Saca la ruta del ejecutable de una línea de comandos con argumentos.</summary>
        public static string ExtractPath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string c = Environment.ExpandEnvironmentVariables(command.Trim());
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                if (end > 1) return c.Substring(1, end - 1);
            }
            int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return c.Substring(0, exe + 4);
            int space = c.IndexOf(' ');
            return space > 0 ? c.Substring(0, space) : c;
        }

        private static string ResolveShortcut(string lnk)
        {
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return null;
                dynamic shell = Activator.CreateInstance(type);
                string target = shell.CreateShortcut(lnk).TargetPath;
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Nombre conocido de la lista de apps de Turbix (p. ej. «Microsoft.549981C3F5F10» = Cortana) o uno legible.</summary>
        private static string StoreAppName(string package)
        {
            int underscore = package.IndexOf('_');
            string name = underscore > 0 ? package.Substring(0, underscore) : package;
            var known = AppxOps.Catalog.FirstOrDefault(a => a.Matches(name));
            return known?.Name ?? FriendlyPackageName(package);
        }

        /// <summary>«SpotifyAB.SpotifyMusic_zpdnekdrzrea0» → «Spotify Music».</summary>
        private static string FriendlyPackageName(string package)
        {
            string name = package;
            int underscore = name.IndexOf('_');
            if (underscore > 0) name = name.Substring(0, underscore);
            int dot = name.LastIndexOf('.');
            if (dot >= 0 && dot < name.Length - 1) name = name.Substring(dot + 1);
            return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warn(L.F("startup.log.readPartError", ex.Message)); }
        }
    }
}
