using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Turbix.Localization;

namespace Turbix.Core
{
    public sealed class RepairTool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Help { get; set; }
        public string Warning { get; set; }
        public string Glyph { get; set; }
        public string Duration { get; set; }
        public bool NeedsRestart { get; set; }

        /// <summary>Hace el trabajo y devuelve un resumen en una frase para el usuario.</summary>
        public Func<IProgress<ProgressInfo>, string> Run { get; set; }
    }

    /// <summary>
    /// Reparaciones estándar de Windows: las mismas que recomiendan el soporte de Microsoft y los foros
    /// para los problemas más comunes, pero con un botón. Todas usan herramientas incluidas en Windows.
    /// </summary>
    public static class RepairOps
    {
        private static readonly string Sys = Environment.GetFolderPath(Environment.SpecialFolder.System);

        private static List<RepairTool> _all;

        /// <summary>Se arma la primera vez que se pide, con los textos ya en el idioma elegido.</summary>
        public static IReadOnlyList<RepairTool> All => _all ?? (_all = Build());

        private static List<RepairTool> Build() => new List<RepairTool>
        {
            new RepairTool
            {
                Id = "sfc", Glyph = "", Duration = L.F("repair.duration.range", 5, 20),
                Name = L.T("repair.sfc.name"),
                Summary = L.T("repair.sfc.summary"),
                Help = L.T("repair.sfc.help"),
                Run = p => RunSfc(p)
            },
            new RepairTool
            {
                Id = "dism", Glyph = "", Duration = L.F("repair.duration.range", 10, 30),
                Name = L.T("repair.dism.name"),
                Summary = L.T("repair.dism.summary"),
                Help = L.T("repair.dism.help"),
                Warning = L.T("repair.dism.warning"),
                Run = p => RunDism(p)
            },
            new RepairTool
            {
                Id = "chkdsk", Glyph = "", Duration = L.F("repair.duration.range", 1, 10),
                Name = L.T("repair.chkdsk.name"),
                Summary = L.T("repair.chkdsk.summary"),
                Help = L.T("repair.chkdsk.help"),
                Run = p => RunChkdsk(p)
            },
            new RepairTool
            {
                Id = "network", Glyph = "", Duration = L.T("repair.duration.underMinute"), NeedsRestart = true,
                Name = L.T("repair.network.name"),
                Summary = L.T("repair.network.summary"),
                Help = L.T("repair.network.help"),
                Warning = L.T("repair.network.warning"),
                Run = p => RunNetwork(p)
            },
            new RepairTool
            {
                Id = "spooler", Glyph = "", Duration = L.T("repair.duration.underMinute"),
                Name = L.T("repair.spooler.name"),
                Summary = L.T("repair.spooler.summary"),
                Help = L.T("repair.spooler.help"),
                Warning = L.T("repair.spooler.warning"),
                Run = p => RunSpooler(p)
            },
            new RepairTool
            {
                Id = "explorer", Glyph = "", Duration = L.T("repair.duration.seconds"),
                Name = L.T("repair.explorer.name"),
                Summary = L.T("repair.explorer.summary"),
                Help = L.T("repair.explorer.help"),
                Warning = L.T("repair.explorer.warning"),
                Run = p => RunExplorer(p)
            },
        };

        // ------------------------------------------------------------------ herramientas

        private static string RunSfc(IProgress<ProgressInfo> p)
        {
            Logger.Step(L.T("repair.sfc.log"));
            // sfc escribe su salida en UTF-16.
            int code = RunTool(Path.Combine(Sys, "sfc.exe"), "/scannow", Encoding.Unicode, p, L.T("repair.sfc.status"));
            switch (code)
            {
                case 0: return L.T("repair.sfc.ok");
                default: return L.F("repair.sfc.warn", code);
            }
        }

        private static string RunDism(IProgress<ProgressInfo> p)
        {
            Logger.Step(L.T("repair.dism.log"));
            int code = RunTool(Path.Combine(Sys, "dism.exe"), "/Online /Cleanup-Image /RestoreHealth /English", null, p,
                L.T("repair.dism.status"));
            return code == 0
                ? L.T("repair.dism.ok")
                : L.F("repair.dism.fail", code);
        }

        private static string RunChkdsk(IProgress<ProgressInfo> p)
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).TrimEnd('\\');
            Logger.Step(L.F("repair.chkdsk.log", drive));
            int code = RunTool(Path.Combine(Sys, "chkdsk.exe"), drive + " /scan", null, p, L.F("repair.chkdsk.status", drive));
            return code == 0
                ? L.T("repair.chkdsk.ok")
                : L.F("repair.chkdsk.fail", code, drive);
        }

        private static string RunNetwork(IProgress<ProgressInfo> p)
        {
            Logger.Step(L.T("repair.network.log"));
            string netsh = Path.Combine(Sys, "netsh.exe");
            p?.Report(new ProgressInfo(15, L.T("repair.network.winsock")));
            int a = RunTool(netsh, "winsock reset", null, null, null);
            p?.Report(new ProgressInfo(50, L.T("repair.network.tcpip")));
            int b = RunTool(netsh, "int ip reset", null, null, null);
            p?.Report(new ProgressInfo(85, L.T("repair.network.dns")));
            try { Native.DnsFlushResolverCache(); } catch { }
            p?.Report(new ProgressInfo(100, L.T("repair.network.done")));
            // «int ip reset» devuelve 1 aunque funcione cuando alguna clave está protegida; es conocido e inofensivo.
            return a == 0
                ? L.T("repair.network.ok")
                : L.F("repair.network.fail", a, b);
        }

        private static string RunSpooler(IProgress<ProgressInfo> p)
        {
            Logger.Step(L.T("repair.spooler.log"));
            p?.Report(new ProgressInfo(10, L.T("repair.spooler.stopping")));
            bool wasRunning = ServiceOps.IsRunning("Spooler");
            int deleted = 0;
            try
            {
                if (wasRunning && !ServiceOps.Stop("Spooler"))
                    return L.T("repair.spooler.stopFailed");

                p?.Report(new ProgressInfo(50, L.T("repair.spooler.deleting")));
                string folder = Path.Combine(Sys, @"spool\PRINTERS");
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        try { File.Delete(file); deleted++; }
                        catch (Exception ex) { Logger.Warn("   " + Path.GetFileName(file) + ": " + ex.Message); }
                    }
                }
            }
            finally
            {
                // Pase lo que pase, la impresión debe quedar funcionando.
                p?.Report(new ProgressInfo(85, L.T("repair.spooler.starting")));
                try { ServiceOps.Start("Spooler"); }
                catch (Exception ex) { Logger.Warn(L.F("repair.spooler.startFailed", ex.Message)); }
            }
            Logger.Success(L.P("repair.spooler.logDone", deleted));
            return deleted == 0
                ? L.T("repair.spooler.empty")
                : L.P("repair.spooler.deleted", deleted);
        }

        private static string RunExplorer(IProgress<ProgressInfo> p)
        {
            Logger.Step(L.T("repair.explorer.log"));
            p?.Report(new ProgressInfo(30, L.T("repair.explorer.closing")));
            int session = Process.GetCurrentProcess().SessionId;
            foreach (var proc in Process.GetProcessesByName("explorer").Where(x => x.SessionId == session))
            {
                try { proc.Kill(); } catch { }
            }
            // Windows lo vuelve a abrir solo; solo se lanza a mano si no reaparece.
            p?.Report(new ProgressInfo(70, L.T("repair.explorer.waiting")));
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(500);
                if (Process.GetProcessesByName("explorer").Any(x => x.SessionId == session)) break;
            }
            if (!Process.GetProcessesByName("explorer").Any(x => x.SessionId == session))
                Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
            Logger.Success(L.T("repair.explorer.logDone"));
            return L.T("repair.explorer.done");
        }

        // ------------------------------------------------------------------ ejecución

        /// <summary>Las herramientas de consola escriben en la página de códigos OEM del sistema.</summary>
        private static Encoding OemEncoding()
        {
            try { return Encoding.GetEncoding(System.Globalization.CultureInfo.InstalledUICulture.TextInfo.OEMCodePage); }
            catch { return null; }
        }

        private static readonly Regex Percent = new Regex(@"(\d{1,3})[.,]?\d*\s*%", RegexOptions.Compiled);

        /// <summary>
        /// Ejecuta una herramienta de consola de Windows oculta, pasa su salida al registro
        /// y convierte las líneas de porcentaje en progreso. Devuelve el código de salida.
        /// </summary>
        private static int RunTool(string exe, string args, Encoding encoding, IProgress<ProgressInfo> progress, string status)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            encoding = encoding ?? OemEncoding();
            if (encoding != null)
            {
                psi.StandardOutputEncoding = encoding;
                psi.StandardErrorEncoding = encoding;
            }

            using (var proc = new Process { StartInfo = psi })
            {
                DataReceivedEventHandler onLine = (s, e) =>
                {
                    string line = e.Data?.Replace("\0", "").Trim();
                    if (string.IsNullOrEmpty(line)) return;
                    var m = Percent.Match(line);
                    if (m.Success && line.Length < 90)
                    {
                        // Las barras de progreso no se guardan en el registro: solo mueven el indicador.
                        if (progress != null && int.TryParse(m.Groups[1].Value, out int pct))
                            progress.Report(new ProgressInfo(Math.Min(99, pct), L.F("repair.progress.percent", status, pct)));
                        return;
                    }
                    Logger.Info("   " + line);
                };
                proc.OutputDataReceived += onLine;
                proc.ErrorDataReceived += onLine;
                progress?.Report(new ProgressInfo(1, status + "..."));
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                progress?.Report(new ProgressInfo(100, L.T("repair.progress.done")));
                return proc.ExitCode;
            }
        }
    }
}
