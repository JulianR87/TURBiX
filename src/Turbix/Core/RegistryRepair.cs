using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    public sealed class RegIssue
    {
        public string Category { get; set; }
        public string Name { get; set; }

        /// <summary>Qué archivo falta.</summary>
        public string Detail { get; set; }

        public string Help { get; set; }
        public RegistryHive Hive { get; set; }
        public string SubKey { get; set; }

        /// <summary>Valor a borrar; null si se borra la clave entera.</summary>
        public string ValueName { get; set; }

        /// <summary>Varios valores de la misma clave agrupados en una sola fila (opcional).</summary>
        public List<string> ValueNames { get; set; }

        public bool DeleteKey => ValueName == null && ValueNames == null;

        /// <summary>Orden en la lista: primero lo que más se nota.</summary>
        public int Order { get; set; }
        public string FullKey => RegistryHelper.HiveName(Hive) + "\\" + SubKey;
    }

    /// <summary>
    /// Reparación del registro de Windows, en versión prudente.
    ///
    /// Solo se marcan entradas que se pueden comprobar como rotas: apuntan a un archivo que ya no
    /// existe en un disco fijo. No se «optimiza» ni se compacta nada, y no se tocan clases COM,
    /// asociaciones de archivos ni nada que no se pueda verificar: ahí es donde los limpiadores de
    /// registro rompen Windows. Cada entrada se guarda antes de borrarla y se puede restaurar.
    /// </summary>
    public static class RegistryRepair
    {
        // Nombres de categoría ya traducidos: se muestran en la lista y también sirven para ordenarla.
        public static string CatStartup => L.T("regfix.cat.startup");
        public static string CatUninstall => L.T("regfix.cat.uninstall");
        public static string CatAppPath => L.T("regfix.cat.appPath");
        public static string CatSharedDll => L.T("regfix.cat.sharedDll");
        public static string CatMuiCache => L.T("regfix.cat.muiCache");

        private static string[] Importance => new[] { CatStartup, CatUninstall, CatAppPath, CatSharedDll, CatMuiCache };

        private const string CurrentVersion = @"SOFTWARE\Microsoft\Windows\CurrentVersion";
        private const string CurrentVersion32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion";

        // ------------------------------------------------------------------ análisis

        public static List<RegIssue> Scan(IProgress<ProgressInfo> progress)
        {
            Logger.Step(L.T("regfix.log.scan"));
            var list = new List<RegIssue>();

            progress?.Report(new ProgressInfo(5, L.T("regfix.progress.startup")));
            Try(() => ScanRun(list, RegistryHive.CurrentUser, CurrentVersion + @"\Run"));
            Try(() => ScanRun(list, RegistryHive.LocalMachine, CurrentVersion + @"\Run"));
            Try(() => ScanRun(list, RegistryHive.LocalMachine, CurrentVersion32 + @"\Run"));

            progress?.Report(new ProgressInfo(25, L.T("regfix.progress.installed")));
            Try(() => ScanUninstall(list, RegistryHive.LocalMachine, CurrentVersion + @"\Uninstall"));
            Try(() => ScanUninstall(list, RegistryHive.LocalMachine, CurrentVersion32 + @"\Uninstall"));
            Try(() => ScanUninstall(list, RegistryHive.CurrentUser, CurrentVersion + @"\Uninstall"));

            progress?.Report(new ProgressInfo(50, L.T("regfix.progress.appPaths")));
            Try(() => ScanAppPaths(list, RegistryHive.LocalMachine, CurrentVersion + @"\App Paths"));
            Try(() => ScanAppPaths(list, RegistryHive.LocalMachine, CurrentVersion32 + @"\App Paths"));
            Try(() => ScanAppPaths(list, RegistryHive.CurrentUser, CurrentVersion + @"\App Paths"));

            progress?.Report(new ProgressInfo(70, L.T("regfix.progress.sharedDlls")));
            Try(() => ScanSharedDlls(list, CurrentVersion + @"\SharedDLLs"));
            Try(() => ScanSharedDlls(list, CurrentVersion32 + @"\SharedDLLs"));

            progress?.Report(new ProgressInfo(85, L.T("regfix.progress.muiCache")));
            Try(() => ScanMuiCache(list));

            var importance = Importance;
            foreach (var issue in list) issue.Order = Array.IndexOf(importance, issue.Category);
            Logger.Success(L.P("regfix.log.found", list.Count));
            progress?.Report(new ProgressInfo(100, L.T("regfix.progress.scanDone")));
            return list;
        }

        private static void ScanRun(List<RegIssue> list, RegistryHive hive, string subKey)
        {
            using (var root = RegistryHelper.OpenBase(hive))
            using (var key = root.OpenSubKey(subKey))
            {
                if (key == null) return;
                foreach (string name in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    string path = Target(Convert.ToString(key.GetValue(name)));
                    if (!IsMissingFile(path)) continue;
                    list.Add(new RegIssue
                    {
                        Category = CatStartup, Name = name, Detail = L.F("regfix.detail.missing", path),
                        Hive = hive, SubKey = subKey, ValueName = name,
                        Help = L.T("regfix.help.startup")
                    });
                }
            }
        }

        private static void ScanUninstall(List<RegIssue> list, RegistryHive hive, string subKey)
        {
            using (var root = RegistryHelper.OpenBase(hive))
            using (var key = root.OpenSubKey(subKey))
            {
                if (key == null) return;
                foreach (string app in key.GetSubKeyNames())
                {
                    using (var k = key.OpenSubKey(app))
                    {
                        if (k == null) continue;
                        string display = Convert.ToString(k.GetValue("DisplayName"));
                        if (string.IsNullOrWhiteSpace(display)) continue;
                        if (Convert.ToString(k.GetValue("SystemComponent")) == "1") continue;
                        if (Convert.ToString(k.GetValue("WindowsInstaller")) == "1") continue;   // MSI: lo gestiona Windows

                        string uninstaller = Target(Convert.ToString(k.GetValue("UninstallString")));
                        if (!IsMissingFile(uninstaller)) continue;

                        // Que falte el desinstalador no prueba que el programa no esté (pasa con controladores y con
                        // instaladores cuya caché se limpió). Solo se marca si además el registro dice dónde estaba
                        // instalado y esa carpeta ya no existe, y su icono tampoco.
                        string location = Convert.ToString(k.GetValue("InstallLocation"))?.Trim().Trim('"');
                        if (string.IsNullOrEmpty(location)) continue;
                        location = Environment.ExpandEnvironmentVariables(location);
                        if (!IsMissingFile(location.TrimEnd('\\'))) continue;

                        string icon = Convert.ToString(k.GetValue("DisplayIcon"))?.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(icon))
                        {
                            int comma = icon.LastIndexOf(',');
                            if (comma > 2) icon = icon.Substring(0, comma).Trim().Trim('"');
                            icon = Environment.ExpandEnvironmentVariables(icon);
                            if (Path.IsPathRooted(icon) && File.Exists(icon)) continue;
                        }

                        list.Add(new RegIssue
                        {
                            Category = CatUninstall, Name = display, Detail = L.F("regfix.detail.missingFolder", location),
                            Hive = hive, SubKey = subKey + "\\" + app, ValueName = null,
                            Help = L.T("regfix.help.uninstall")
                        });
                    }
                }
            }
        }

        private static void ScanAppPaths(List<RegIssue> list, RegistryHive hive, string subKey)
        {
            using (var root = RegistryHelper.OpenBase(hive))
            using (var key = root.OpenSubKey(subKey))
            {
                if (key == null) return;
                foreach (string app in key.GetSubKeyNames())
                {
                    using (var k = key.OpenSubKey(app))
                    {
                        string path = Convert.ToString(k?.GetValue(""))?.Trim().Trim('"');
                        if (string.IsNullOrEmpty(path)) continue;
                        path = Environment.ExpandEnvironmentVariables(path);
                        if (!IsMissingFile(path)) continue;
                        // En Windows de 64 bits esta clave es la misma desde las dos vistas: no repetir la entrada.
                        if (list.Any(x => x.Category == CatAppPath && x.Hive == hive &&
                                          string.Equals(x.Name, app, StringComparison.OrdinalIgnoreCase))) continue;
                        list.Add(new RegIssue
                        {
                            Category = CatAppPath, Name = app, Detail = L.F("regfix.detail.missing", path),
                            Hive = hive, SubKey = subKey + "\\" + app, ValueName = null,
                            Help = L.T("regfix.help.appPath")
                        });
                    }
                }
            }
        }

        private static void ScanSharedDlls(List<RegIssue> list, string subKey)
        {
            using (var root = RegistryHelper.OpenBase(RegistryHive.LocalMachine))
            using (var key = root.OpenSubKey(subKey))
            {
                if (key == null) return;
                foreach (string file in key.GetValueNames())
                {
                    if (!IsMissingFile(file)) continue;
                    // En Windows de 64 bits esta clave es la misma desde las dos vistas: no repetir la entrada.
                    if (list.Any(x => x.Category == CatSharedDll && string.Equals(x.ValueName, file, StringComparison.OrdinalIgnoreCase))) continue;
                    list.Add(new RegIssue
                    {
                        Category = CatSharedDll, Name = Path.GetFileName(file), Detail = L.F("regfix.detail.missing", file),
                        Hive = RegistryHive.LocalMachine, SubKey = subKey, ValueName = file,
                        Help = L.T("regfix.help.sharedDll")
                    });
                }
            }
        }

        private static void ScanMuiCache(List<RegIssue> list)
        {
            const string subKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
            using (var root = RegistryHelper.OpenBase(RegistryHive.CurrentUser))
            using (var key = root.OpenSubKey(subKey))
            {
                if (key == null) return;
                var values = new List<string>();
                var programs = new List<string>();
                foreach (string name in key.GetValueNames())
                {
                    // «C:\ruta\programa.exe.FriendlyAppName»
                    int dot = name.LastIndexOf('.');
                    if (dot <= 0) continue;
                    string file = name.Substring(0, dot);
                    if (!IsMissingFile(file)) continue;
                    values.Add(name);
                    string program = Path.GetFileName(file);
                    if (!programs.Contains(program, StringComparer.OrdinalIgnoreCase)) programs.Add(program);
                }
                if (values.Count == 0) return;

                // Pueden ser cientos y no tienen importancia: se muestran como una sola fila.
                string sample = string.Join(", ", programs.Take(3)) + (programs.Count > 3 ? "…" : "");
                list.Add(new RegIssue
                {
                    Category = CatMuiCache,
                    Name = L.P("regfix.muiCache.name", programs.Count),
                    Detail = sample,
                    Hive = RegistryHive.CurrentUser, SubKey = subKey, ValueNames = values,
                    Help = L.T("regfix.help.muiCache")
                });
            }
        }

        /// <summary>
        /// Ejecutable de una línea de comandos, o null si no se puede saber con certeza dónde termina la ruta
        /// (sin comillas, con espacios y sin «.exe»): en ese caso no se arriesga un falso positivo.
        /// </summary>
        private static string Target(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string c = command.Trim();
            string path = StartupManager.ExtractPath(c);
            if (path == null) return null;
            bool quoted = c.StartsWith("\"");
            if (!quoted && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && c.Contains(" ")) return null;
            return path;
        }

        /// <summary>
        /// true solo si la ruta es completa, está en un disco fijo conectado y el archivo no existe.
        /// Las rutas de red o de discos extraíbles nunca se dan por rotas: puede que simplemente no estén conectados.
        /// </summary>
        private static bool IsMissingFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\") || !Path.IsPathRooted(path)) return false;
                if (path.Length < 4 || path[1] != ':') return false;
                var drive = new DriveInfo(path.Substring(0, 1));
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) return false;
                if (File.Exists(path) || Directory.Exists(path)) return false;

                // Lo que está dentro de la carpeta de Windows es de Windows: un Windows recién instalado ya trae
                // entradas que apuntan a archivos opcionales que no están (por ejemplo, bibliotecas .tlb de .NET).
                // Eso no es un fallo, y si algo de Windows falta de verdad se repara con SFC, no tocando el registro.
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(win) && path.StartsWith(win.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return false;

                // Los programas de 32 bits pueden anotar «Archivos de programa» aunque su archivo real está en
                // «Archivos de programa (x86)». Se comprueban ambas.
                string pf = Environment.GetEnvironmentVariable("ProgramW6432"), pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                if (!string.IsNullOrEmpty(pf) && !string.IsNullOrEmpty(pf86) && path.StartsWith(pf + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    string alt = pf86 + path.Substring(pf.Length);
                    if (File.Exists(alt) || Directory.Exists(alt)) return false;
                }
                return true;
            }
            catch
            {
                return false;   // ruta rara: ante la duda, no se toca
            }
        }

        // ------------------------------------------------------------------ reparación

        /// <summary>Borra las entradas indicadas guardando antes una copia. Devuelve {reparadas, con error}.</summary>
        public static int[] Fix(IList<RegIssue> issues, IProgress<ProgressInfo> progress)
        {
            Logger.Step(L.P("regfix.log.fixing", issues.Count));
            var session = BackupStore.NewSession("regfix.session", issues.Count);
            int ok = 0, fail = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                progress?.Report(new ProgressInfo(100.0 * i / issues.Count, issue.Category + ": " + issue.Name));
                try
                {
                    if (issue.DeleteKey)
                    {
                        string file = ExportKey(issue.FullKey, session, i);
                        session.Entries.Add(new BackupEntry { Type = "RegFile", Name = issue.FullKey, OldString = file });
                        BackupStore.Save(session);
                        using (var root = RegistryHelper.OpenBase(issue.Hive))
                            root.DeleteSubKeyTree(issue.SubKey, false);
                    }
                    else
                    {
                        var names = issue.ValueNames ?? new List<string> { issue.ValueName };
                        foreach (string valueName in names)
                        {
                            object old = RegistryHelper.GetValue(issue.Hive, issue.SubKey, valueName, out var kind);
                            if (old == null) continue;   // ya no está
                            BackupStore.RecordRegistry(session, issue.Hive, issue.SubKey, valueName, old, kind, true);
                        }
                        BackupStore.Save(session);
                        foreach (string valueName in names)
                            RegistryHelper.DeleteValue(issue.Hive, issue.SubKey, valueName);
                    }
                    ok++;
                    Logger.Info($"   {issue.Category}: {issue.Name}");
                }
                catch (Exception ex)
                {
                    fail++;
                    Logger.Warn("   " + L.F("regfix.log.fixFailed", issue.Name, ex.Message));
                }
            }
            if (session.Entries.Count > 0) Logger.Info(L.F("regfix.log.backupSaved", session.FilePath));
            Logger.Success(L.P("regfix.log.fixed", ok, fail));
            progress?.Report(new ProgressInfo(100, L.T("regfix.progress.fixDone")));
            return new[] { ok, fail };
        }

        /// <summary>Exporta una clave completa a un archivo .reg con la herramienta de Windows.</summary>
        private static string ExportKey(string fullKey, BackupSession session, int index)
        {
            string folder = Path.Combine(AppPaths.Backups, "reg");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, Path.GetFileNameWithoutExtension(session.FilePath) + "-" + index + ".reg");
            int code = RunReg($"export \"{fullKey}\" \"{file}\" /y /reg:64");
            if (code != 0 || !File.Exists(file))
                throw new InvalidOperationException(L.T("regfix.error.exportFailed"));
            return file;
        }

        /// <summary>Restaura una clave exportada (lo usa la reversión).</summary>
        public static void ImportFile(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException(L.F("regfix.error.backupMissing", file));
            int code = RunReg($"import \"{file}\" /reg:64");
            if (code != 0) throw new InvalidOperationException(L.F("regfix.error.importFailed", Path.GetFileName(file)));
        }

        private static int RunReg(string args)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "reg.exe"), args)
            {
                UseShellExecute = false, CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warn(L.F("regfix.log.error", ex.Message)); }
        }
    }
}
