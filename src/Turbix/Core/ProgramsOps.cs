using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    public sealed class InstalledProgram
    {
        public string Name { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public long SizeBytes { get; set; }
        public DateTime? InstallDate { get; set; }
        public string UninstallCommand { get; set; }
        public string IconPath { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// Programas instalados (la misma lista que «Aplicaciones instaladas» de Windows) y su desinstalador.
    /// Turbix no desinstala por su cuenta: abre el desinstalador oficial de cada programa.
    /// </summary>
    public static class ProgramsOps
    {
        private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string Uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        public static List<InstalledProgram> List()
        {
            var list = new List<InstalledProgram>();
            Read(list, RegistryHive.LocalMachine, Uninstall, L.T("apps.programs.source.allUsers"));
            Read(list, RegistryHive.LocalMachine, Uninstall32, L.T("apps.programs.source.allUsers32"));
            Read(list, RegistryHive.CurrentUser, Uninstall, L.T("apps.programs.source.currentUser"));
            // El mismo programa puede estar registrado dos veces (por ejemplo, 32 y 64 bits del mismo instalador).
            return list.GroupBy(p => (p.Name + "|" + p.Version).ToLowerInvariant())
                       .Select(g => g.First())
                       .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                       .ToList();
        }

        private static void Read(List<InstalledProgram> list, RegistryHive hive, string subKey, string source)
        {
            try
            {
                using (var root = RegistryHelper.OpenBase(hive))
                using (var key = root.OpenSubKey(subKey))
                {
                    if (key == null) return;
                    foreach (string name in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = key.OpenSubKey(name))
                            {
                                if (k == null) continue;
                                string display = Convert.ToString(k.GetValue("DisplayName"))?.Trim();
                                if (string.IsNullOrEmpty(display)) continue;
                                // Igual que Windows: se ocultan componentes, actualizaciones y partes de otros programas.
                                if (Convert.ToString(k.GetValue("SystemComponent")) == "1") continue;
                                if (k.GetValue("ParentKeyName") != null || k.GetValue("ReleaseType") != null) continue;
                                string command = Convert.ToString(k.GetValue("UninstallString"))?.Trim();
                                if (string.IsNullOrEmpty(command)) continue;

                                list.Add(new InstalledProgram
                                {
                                    Name = display,
                                    Publisher = Convert.ToString(k.GetValue("Publisher"))?.Trim(),
                                    Version = Convert.ToString(k.GetValue("DisplayVersion"))?.Trim(),
                                    SizeBytes = ToLong(k.GetValue("EstimatedSize")) * 1024,
                                    InstallDate = ParseDate(Convert.ToString(k.GetValue("InstallDate"))),
                                    UninstallCommand = command,
                                    IconPath = IconFile(Convert.ToString(k.GetValue("DisplayIcon"))),
                                    Source = source,
                                });
                            }
                        }
                        catch
                        {
                            // Una entrada ilegible no impide mostrar las demás.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("apps.programs.log.readError", ex.Message));
            }
        }

        /// <summary>Abre el desinstalador oficial del programa y espera a que termine.</summary>
        public static void RunUninstaller(InstalledProgram program)
        {
            Logger.Step(L.F("apps.programs.uninstalling", program.Name));
            // «cmd /c» interpreta la línea tal como la guardó el instalador (con o sin comillas, MsiExec, etc.).
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c \"" + program.UninstallCommand + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                Logger.Info("   " + L.F("apps.programs.log.exitCode", p.ExitCode));
            }
        }

        public static bool StillInstalled(InstalledProgram program) =>
            List().Any(p => string.Equals(p.Name, program.Name, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.Version, program.Version, StringComparison.OrdinalIgnoreCase));

        private static long ToLong(object v)
        {
            try { return v == null ? 0 : Convert.ToInt64(v); }
            catch { return 0; }
        }

        private static DateTime? ParseDate(string s) =>
            DateTime.TryParseExact(s?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;

        private static string IconFile(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon)) return null;
            string s = icon.Trim().Trim('"');
            int comma = s.LastIndexOf(',');
            if (comma > 2) s = s.Substring(0, comma).Trim().Trim('"');
            s = Environment.ExpandEnvironmentVariables(s);
            return Path.IsPathRooted(s) && File.Exists(s) ? s : null;
        }
    }
}
