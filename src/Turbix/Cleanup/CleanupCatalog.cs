using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Threading;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Cleanup
{
    /// <summary>
    /// Catálogo de limpieza. Marcadores: {USER} = cada perfil de usuario, {DRIVE} = cada disco fijo.
    /// En navegadores solo se borra la caché: contraseñas, cookies, historial y sesiones no se tocan.
    /// </summary>
    public static class CleanupCatalog
    {
        private const string Local = @"{USER}\AppData\Local";
        private const string Roaming = @"{USER}\AppData\Roaming";

        private static List<CleanupItem> _all;
        private static readonly List<string> StoppedServices = new List<string>();
        private static bool _explorerStopped;

        public static IReadOnlyList<CleanupItem> All => _all ?? (_all = Build());

        private static List<string> Chromium(string b) => new List<string>
        {
            b + @"\*\Cache\Cache_Data\*", b + @"\*\Code Cache\*", b + @"\*\GPUCache\*",
            b + @"\*\DawnCache\*", b + @"\*\DawnGraphiteCache\*", b + @"\*\DawnWebGPUCache\*",
            b + @"\ShaderCache\*", b + @"\GrShaderCache\*", b + @"\GraphiteDawnCache\*",
        };

        private static List<string> Electron(string b) => new List<string>
        {
            b + @"\Cache\*", b + @"\Code Cache\*", b + @"\GPUCache\*", b + @"\DawnCache\*",
        };

        private static List<string> Join(params IEnumerable<string>[] lists) => lists.SelectMany(x => x).ToList();

        private static CleanupGroup G(string name, IEnumerable<string> paths, params string[] processes) =>
            new CleanupGroup { Name = name, Paths = paths.ToList(), Processes = processes };

        private static List<CleanupItem> Build() => new List<CleanupItem>
        {
            new CleanupItem
            {
                Id = "user-temp", Glyph = "\uE8B7", Recommended = true, MinAgeHours = 24,
                Name = L.T("cleanup.item.userTemp.name"),
                Summary = L.T("cleanup.item.userTemp.summary"),
                Help = L.T("cleanup.item.userTemp.help"),
                Groups = { G("Temp", new[] { Local + @"\Temp\*" }) }
            },
            new CleanupItem
            {
                Id = "windows-temp", Glyph = "\uE770", Recommended = true, MinAgeHours = 24,
                Name = L.T("cleanup.item.windowsTemp.name"),
                Summary = L.T("cleanup.item.windowsTemp.summary"),
                Help = L.T("cleanup.item.windowsTemp.help"),
                Groups = { G("Windows Temp", new[] { @"%WINDIR%\Temp\*" }) }
            },
            new CleanupItem
            {
                Id = "windows-update", Glyph = "\uE895", Recommended = true,
                Name = L.T("cleanup.item.windowsUpdate.name"),
                Summary = L.T("cleanup.item.windowsUpdate.summary"),
                Help = L.T("cleanup.item.windowsUpdate.help"),
                Warning = L.T("cleanup.item.windowsUpdate.warning"),
                Groups = { G("SoftwareDistribution", new[] { @"%WINDIR%\SoftwareDistribution\Download\*" }) },
                Before = () => StopServices("wuauserv", "bits"),
                After = RestartServices
            },
            new CleanupItem
            {
                Id = "delivery-optimization", Glyph = "\uE896", Recommended = true,
                Name = L.T("cleanup.item.deliveryOptimization.name"),
                Summary = L.T("cleanup.item.deliveryOptimization.summary"),
                Help = L.T("cleanup.item.deliveryOptimization.help"),
                Groups = { G(L.T("cleanup.item.deliveryOptimization.name"),
                    new[] { @"%WINDIR%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\*" }) },
                Before = () => StopServices("DoSvc"),
                After = RestartServices
            },
            new CleanupItem
            {
                Id = "error-reports", Glyph = "\uE7BA", Recommended = true,
                Name = L.T("cleanup.item.errorReports.name"),
                Summary = L.T("cleanup.item.errorReports.summary"),
                Help = L.T("cleanup.item.errorReports.help"),
                Groups =
                {
                    G("WER", new[]
                    {
                        @"%ProgramData%\Microsoft\Windows\WER\ReportArchive\*",
                        @"%ProgramData%\Microsoft\Windows\WER\ReportQueue\*",
                        @"%ProgramData%\Microsoft\Windows\WER\Temp\*",
                        Local + @"\Microsoft\Windows\WER\*",
                    })
                }
            },
            new CleanupItem
            {
                Id = "memory-dumps", Glyph = "\uE783", Recommended = true,
                Name = L.T("cleanup.item.memoryDumps.name"),
                Summary = L.T("cleanup.item.memoryDumps.summary"),
                Help = L.T("cleanup.item.memoryDumps.help"),
                Warning = L.T("cleanup.item.memoryDumps.warning"),
                Groups =
                {
                    G(L.T("cleanup.item.memoryDumps.name"), new[]
                    {
                        @"%WINDIR%\MEMORY.DMP", @"%WINDIR%\Minidump\*",
                        @"%WINDIR%\LiveKernelReports\*.dmp", @"%WINDIR%\LiveKernelReports\*\*.dmp",
                        Local + @"\CrashDumps\*",
                    })
                }
            },
            new CleanupItem
            {
                Id = "system-logs", Glyph = "\uE8A5", Recommended = true,
                Name = L.T("cleanup.item.systemLogs.name"),
                Summary = L.T("cleanup.item.systemLogs.summary"),
                Help = L.T("cleanup.item.systemLogs.help"),
                Groups =
                {
                    G("Logs", new[]
                    {
                        @"%WINDIR%\Logs\CBS\*", @"%WINDIR%\Logs\DISM\*", @"%WINDIR%\Logs\WindowsUpdate\*",
                        @"%WINDIR%\Logs\MoSetup\*", @"%WINDIR%\Logs\SIH\*", @"%WINDIR%\Logs\waasmedic\*",
                    })
                }
            },
            new CleanupItem
            {
                Id = "inet-cache", Glyph = "\uE968", Recommended = true,
                Name = L.T("cleanup.item.inetCache.name"),
                Summary = L.T("cleanup.item.inetCache.summary"),
                Help = L.T("cleanup.item.inetCache.help"),
                Groups =
                {
                    // Content.Outlook/Word/MSO guardan adjuntos y documentos abiertos desde Office: no se borran.
                    new CleanupGroup
                    {
                        Name = "INetCache", Paths = { Local + @"\Microsoft\Windows\INetCache\*" },
                        Exclude = new[] { "Content.Outlook", "Content.Word", "Content.MSO" }
                    }
                }
            },
            new CleanupItem
            {
                Id = "browsers", Glyph = "\uE774", Recommended = true,
                Name = L.T("cleanup.item.browsers.name"),
                Summary = L.T("cleanup.item.browsers.summary"),
                Help = L.T("cleanup.item.browsers.help"),
                Warning = L.T("cleanup.item.browsers.warning"),
                Groups =
                {
                    G("Google Chrome", Chromium(Local + @"\Google\Chrome\User Data"), "chrome"),
                    G("Microsoft Edge", Chromium(Local + @"\Microsoft\Edge\User Data"), "msedge"),
                    G("Brave", Chromium(Local + @"\BraveSoftware\Brave-Browser\User Data"), "brave"),
                    G("Vivaldi", Chromium(Local + @"\Vivaldi\User Data"), "vivaldi"),
                    G("Opera", Join(
                        new[] { Local + @"\Opera Software\Opera Stable\Cache\Cache_Data\*",
                                Local + @"\Opera Software\Opera GX Stable\Cache\Cache_Data\*" },
                        Chromium(Local + @"\Opera Software\Opera Stable"),
                        Chromium(Local + @"\Opera Software\Opera GX Stable"),
                        Electron(Roaming + @"\Opera Software\Opera Stable"),
                        Electron(Roaming + @"\Opera Software\Opera GX Stable")), "opera"),
                    G("Mozilla Firefox", new[]
                    {
                        Local + @"\Mozilla\Firefox\Profiles\*\cache2\*",
                        Local + @"\Mozilla\Firefox\Profiles\*\startupCache\*",
                        Local + @"\Mozilla\Firefox\Profiles\*\thumbnails\*",
                    }, "firefox"),
                }
            },
            new CleanupItem
            {
                Id = "apps-cache", Glyph = "\uE71D", Recommended = true,
                Name = L.T("cleanup.item.appsCache.name"),
                Summary = L.T("cleanup.item.appsCache.summary"),
                Help = L.T("cleanup.item.appsCache.help"),
                Warning = L.T("cleanup.item.appsCache.warning"),
                Groups =
                {
                    G("Discord", Electron(Roaming + @"\discord"), "Discord"),
                    G(L.T("cleanup.group.teamsClassic"), Join(Electron(Roaming + @"\Microsoft\Teams"), new[] { Roaming + @"\Microsoft\Teams\tmp\*" }), "Teams"),
                    G("Teams", Chromium(Local + @"\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView"), "ms-teams"),
                    G("Spotify", Join(new[] { Local + @"\Spotify\Data\*" }, Electron(Local + @"\Spotify\Browser")), "Spotify"),
                    G("Steam", Join(Electron(Local + @"\Steam\htmlcache"), Chromium(Local + @"\Steam\htmlcache")), "steam", "steamwebhelper"),
                    G("Slack", Electron(Roaming + @"\Slack"), "slack"),
                    G("Visual Studio Code", Join(Electron(Roaming + @"\Code"),
                        new[] { Roaming + @"\Code\CachedData\*", Roaming + @"\Code\CachedExtensionVSIXs\*" }), "Code"),
                }
            },
            new CleanupItem
            {
                Id = "store-cache", Glyph = "\uE719", Recommended = true,
                Name = L.T("cleanup.item.storeCache.name"),
                Summary = L.T("cleanup.item.storeCache.summary"),
                Help = L.T("cleanup.item.storeCache.help"),
                Groups = { G("Microsoft Store", new[] { Local + @"\Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache\*" }, "WinStore.App") }
            },
            new CleanupItem
            {
                Id = "dns", Glyph = "\uE701", Recommended = true,
                Name = L.T("cleanup.item.dns.name"),
                Summary = L.T("cleanup.item.dns.summary"),
                Help = L.T("cleanup.item.dns.help"),
                Custom = () =>
                {
                    if (Native.DnsFlushResolverCache()) Logger.Info("   " + L.T("cleanup.log.dnsFlushed"));
                    else Logger.Warn("   " + L.T("cleanup.log.dnsFailed"));
                    return null;
                }
            },
            new CleanupItem
            {
                Id = "shader-cache", Glyph = "\uE7FC",
                Name = L.T("cleanup.item.shaderCache.name"),
                Summary = L.T("cleanup.item.shaderCache.summary"),
                Help = L.T("cleanup.item.shaderCache.help"),
                Warning = L.T("cleanup.item.shaderCache.warning"),
                Groups =
                {
                    G("Shaders", new[]
                    {
                        Local + @"\D3DSCache\*", Local + @"\NVIDIA\DXCache\*", Local + @"\NVIDIA\GLCache\*",
                        @"{USER}\AppData\LocalLow\NVIDIA\PerDriverVersion\DXCache\*",
                        @"%ProgramData%\NVIDIA Corporation\NV_Cache\*",
                        Local + @"\AMD\DxCache\*", Local + @"\AMD\DxcCache\*", Local + @"\AMD\GLCache\*", Local + @"\AMD\VkCache\*",
                        @"{USER}\AppData\LocalLow\Intel\ShaderCache\*",
                    })
                }
            },
            new CleanupItem
            {
                Id = "thumbnails", Glyph = "\uE91B",
                Name = L.T("cleanup.item.thumbnails.name"),
                Summary = L.T("cleanup.item.thumbnails.summary"),
                Help = L.T("cleanup.item.thumbnails.help"),
                Warning = L.T("cleanup.item.thumbnails.warning"),
                Groups =
                {
                    G(L.T("cleanup.item.thumbnails.name"), new[]
                    {
                        Local + @"\Microsoft\Windows\Explorer\thumbcache_*.db",
                        Local + @"\Microsoft\Windows\Explorer\iconcache_*.db",
                        Local + @"\IconCache.db",
                    })
                },
                Before = StopExplorer,
                After = EnsureExplorer
            },
            new CleanupItem
            {
                Id = "driver-installers", Glyph = "\uE964",
                Name = L.T("cleanup.item.driverInstallers.name"),
                Summary = L.T("cleanup.item.driverInstallers.summary"),
                Help = L.T("cleanup.item.driverInstallers.help"),
                Groups = { G("Drivers", new[] { @"%SystemDrive%\NVIDIA\*", @"%SystemDrive%\AMD\*" }) }
            },
            new CleanupItem
            {
                Id = "recycle-bin", Glyph = "\uE74D",
                Name = L.T("cleanup.item.recycleBin.name"),
                Summary = L.T("cleanup.item.recycleBin.summary"),
                Help = L.T("cleanup.item.recycleBin.help"),
                Warning = L.T("cleanup.item.recycleBin.warning"),
                Groups = { new CleanupGroup { Name = L.T("cleanup.item.recycleBin.name"), Paths = { @"{DRIVE}\$Recycle.Bin\*\*" }, Exclude = new[] { "desktop.ini" } } },
                After = () => { try { Native.SHUpdateRecycleBinIcon(); } catch { } }
            },
            new CleanupItem
            {
                Id = "recent-items", Glyph = "\uE81C",
                Name = L.T("cleanup.item.recentItems.name"),
                Summary = L.T("cleanup.item.recentItems.summary"),
                Help = L.T("cleanup.item.recentItems.help"),
                Warning = L.T("cleanup.item.recentItems.warning"),
                Groups =
                {
                    new CleanupGroup
                    {
                        Name = L.T("cleanup.item.recentItems.name"),
                        Paths =
                        {
                            Roaming + @"\Microsoft\Windows\Recent\*.lnk",
                            Roaming + @"\Microsoft\Windows\Recent\AutomaticDestinations\*",
                            Roaming + @"\Microsoft\Windows\Recent\CustomDestinations\*",
                        },
                        // Este archivo guarda las carpetas ancladas en «Acceso rápido» del Explorador: se conserva.
                        Exclude = new[] { "f01b4d95cf55d32a.automaticDestinations-ms" }
                    }
                }
            },
            new CleanupItem
            {
                Id = "prefetch", Glyph = "\uE916",
                Name = L.T("cleanup.item.prefetch.name"),
                Summary = L.T("cleanup.item.prefetch.summary"),
                Help = L.T("cleanup.item.prefetch.help"),
                Warning = L.T("cleanup.item.prefetch.warning"),
                Groups = { G("Prefetch", new[] { @"%WINDIR%\Prefetch\*.pf" }) }
            },
            new CleanupItem
            {
                Id = "event-logs", Glyph = "\uE9D9",
                Name = L.T("cleanup.item.eventLogs.name"),
                Summary = L.T("cleanup.item.eventLogs.summary"),
                Help = L.T("cleanup.item.eventLogs.help"),
                Warning = L.T("cleanup.item.eventLogs.warning"),
                CustomMeasure = MeasureEventLogs,
                Custom = ClearEventLogs
            },
            new CleanupItem
            {
                Id = "component-store", Glyph = "\uE7B8",
                Name = L.T("cleanup.item.componentStore.name"),
                Summary = L.T("cleanup.item.componentStore.summary"),
                Help = L.T("cleanup.item.componentStore.help"),
                Warning = L.T("cleanup.item.componentStore.warning"),
                Custom = RunDism
            },
            new CleanupItem
            {
                Id = "windows-old", Glyph = "",
                Name = L.T("cleanup.item.windowsOld.name"),
                Summary = L.T("cleanup.item.windowsOld.summary"),
                Help = L.T("cleanup.item.windowsOld.help"),
                Warning = L.T("cleanup.item.windowsOld.warning"),
                CustomMeasure = MeasureOldWindows,
                Custom = CleanOldWindows
            },
        };

        // ------------------------------------------------------------------ Windows.old

        // Controladores del Liberador de espacio en disco (cleanmgr) que se activan. No se incluye
        // «Windows ESD installation files»: lo necesita «Restablecer este PC».
        private static readonly string[] OldWindowsHandlers =
        {
            "Previous Installations", "Temporary Setup Files", "Setup Log Files", "Windows Upgrade Log Files",
        };

        private const string VolumeCaches = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
        private const string StateFlag = "StateFlags0771";

        private static IEnumerable<string> OldWindowsFolders()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (string.IsNullOrEmpty(drive)) yield break;
            foreach (string name in new[] { "Windows.old", "$Windows.~BT", "$Windows.~WS" })
            {
                string path = Path.Combine(drive, name);
                if (Directory.Exists(path)) yield return path;
            }
        }

        private static long? MeasureOldWindows()
        {
            long total = 0;
            foreach (string folder in OldWindowsFolders()) total += FolderSize(new DirectoryInfo(folder));
            return total;
        }

        private static long FolderSize(DirectoryInfo dir)
        {
            long size = 0;
            try
            {
                foreach (var f in dir.EnumerateFiles()) size += f.Length;
                foreach (var d in dir.EnumerateDirectories())
                    if ((d.Attributes & FileAttributes.ReparsePoint) == 0) size += FolderSize(d);
            }
            catch
            {
                // Carpetas sin permiso de lectura: se cuenta lo que se pueda.
            }
            return size;
        }

        private static long? CleanOldWindows()
        {
            if (!OldWindowsFolders().Any())
            {
                Logger.Info("   " + L.T("cleanup.log.noOldWindows"));
                return 0;
            }
            string cleanmgr = Path.Combine(Environment.SystemDirectory, "cleanmgr.exe");
            if (!File.Exists(cleanmgr)) throw new FileNotFoundException(L.T("cleanup.log.noCleanmgr"));

            long before = SystemInfo.FreeSpace;
            var marked = new List<string>();
            try
            {
                foreach (string handler in OldWindowsHandlers)
                {
                    if (!RegistryHelper.KeyExists(Microsoft.Win32.RegistryHive.LocalMachine, VolumeCaches + "\\" + handler)) continue;
                    RegistryHelper.SetValue(Microsoft.Win32.RegistryHive.LocalMachine, VolumeCaches + "\\" + handler, StateFlag, 2, Microsoft.Win32.RegistryValueKind.DWord);
                    marked.Add(handler);
                }
                Logger.Info("   " + L.T("cleanup.log.cleanmgrRunning"));
                using (var p = Process.Start(new ProcessStartInfo(cleanmgr, "/sagerun:771") { UseShellExecute = false }))
                {
                    if (!p.WaitForExit((int)TimeSpan.FromMinutes(45).TotalMilliseconds))
                        Logger.Warn("   " + L.T("cleanup.log.cleanmgrStillRunning"));
                }
            }
            finally
            {
                // Las marcas se quitan siempre para no alterar el Liberador cuando lo uses a mano.
                foreach (string handler in marked)
                {
                    try { RegistryHelper.DeleteValue(Microsoft.Win32.RegistryHive.LocalMachine, VolumeCaches + "\\" + handler, StateFlag); }
                    catch { }
                }
            }
            return Math.Max(0, SystemInfo.FreeSpace - before);
        }

        // ------------------------------------------------------------------ servicios

        private static void StopServices(params string[] names)
        {
            StoppedServices.Clear();
            foreach (var n in names)
            {
                if (!ServiceOps.IsRunning(n)) continue;
                // Se anota antes de pedir la parada: aunque tarde en detenerse, al terminar se vuelve a iniciar.
                StoppedServices.Add(n);
                if (!ServiceOps.Stop(n)) Logger.Warn("   " + L.F("cleanup.log.serviceSlow", n));
            }
        }

        private static void RestartServices()
        {
            foreach (var n in StoppedServices) ServiceOps.Start(n);
            StoppedServices.Clear();
        }

        // ------------------------------------------------------------------ Explorador

        private static void StopExplorer()
        {
            _explorerStopped = false;
            int session = Process.GetCurrentProcess().SessionId;
            foreach (var p in Process.GetProcessesByName("explorer").Where(p => p.SessionId == session))
            {
                try { p.Kill(); _explorerStopped = true; } catch { }
            }
            Thread.Sleep(700);
        }

        private static void EnsureExplorer()
        {
            if (!_explorerStopped) return;
            Thread.Sleep(2000);
            int session = Process.GetCurrentProcess().SessionId;
            if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
                Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        }

        // ------------------------------------------------------------------ eventos

        private static string EventLogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"winevt\Logs");

        private static long? MeasureEventLogs()
        {
            try
            {
                return new DirectoryInfo(EventLogDir).EnumerateFiles("*.evtx").Sum(f => f.Length);
            }
            catch
            {
                return null;
            }
        }

        private static long? ClearEventLogs()
        {
            long before = MeasureEventLogs() ?? 0;
            int ok = 0, total = 0;
            using (var session = new EventLogSession())
            {
                foreach (var name in session.GetLogNames())
                {
                    total++;
                    try { session.ClearLog(name); ok++; }
                    catch { /* registros analíticos o en uso */ }
                }
            }
            Logger.Info("   " + L.P("cleanup.log.eventLogsCleared", total, ok.ToString("N0", L.Culture)));
            long after = MeasureEventLogs() ?? before;
            return Math.Max(0, before - after);
        }

        // ------------------------------------------------------------------ DISM

        private static long? RunDism()
        {
            long before = SystemInfo.FreeSpace;
            Logger.Info("   " + L.T("cleanup.log.dismRunning"));
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "dism.exe"),
                "/Online /Cleanup-Image /StartComponentCleanup /NoRestart")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            try { psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.InstalledUICulture.TextInfo.OEMCodePage); }
            catch { }
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string tail = string.Join(" ", output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Reverse().Take(2).Reverse());
                    Logger.Warn("   " + L.F("cleanup.log.dismExit", p.ExitCode, tail));
                }
            }
            return Math.Max(0, SystemInfo.FreeSpace - before);
        }
    }
}
