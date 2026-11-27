using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    public enum FindingLevel { Ok, Info, Warning, Danger }

    public sealed class Finding
    {
        public FindingLevel Level { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }

        /// <summary>Texto del enlace y página a la que lleva («cleanup», «performance», «tools»...). Opcional.</summary>
        public string ActionText { get; set; }
        public string ActionPage { get; set; }
    }

    /// <summary>
    /// Revisión rápida y de solo lectura de las causas más habituales de un equipo lento.
    /// Son las que se repiten una y otra vez en los foros de soporte de Windows: disco mecánico,
    /// poca memoria, disco lleno, demasiados programas al inicio, semanas sin reiniciar...
    /// </summary>
    public static class Diagnostics
    {
        private const string PowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";

        public static List<Finding> Run()
        {
            var list = new List<Finding>();
            Try(() => CheckAccount(list));
            Try(() => CheckDisk(list));
            Try(() => CheckFreeSpace(list));
            Try(() => CheckMemory(list));
            Try(() => CheckStartup(list));
            Try(() => CheckUptime(list));
            Try(() => CheckPendingReboot(list));
            Try(() => CheckPowerPlan(list));
            Try(() => CheckWindowsSupport(list));

            if (list.Count == 0)
            {
                list.Add(new Finding
                {
                    Level = FindingLevel.Ok, Title = L.T("diag.allOk.title"),
                    Text = L.T("diag.allOk.text")
                });
            }
            return list.OrderByDescending(f => (int)f.Level).ToList();
        }

        private static void CheckAccount(List<Finding> list)
        {
            if (!SystemInfo.RunsAsOtherUser()) return;
            string session = SystemInfo.SessionUserName(), admin = Environment.UserName;
            list.Add(new Finding
            {
                Level = FindingLevel.Danger, Title = L.F("diag.otherUser.title", admin),
                Text = L.F("diag.otherUser.text", session, admin)
            });
        }

        private static void CheckDisk(List<Finding> list)
        {
            var disk = DiskInfo.GetSystemDisk();
            if (!disk.Healthy)
            {
                list.Add(new Finding
                {
                    Level = FindingLevel.Danger, Title = L.T("diag.diskFailing.title"),
                    Text = string.IsNullOrEmpty(disk.Model)
                        ? L.T("diag.diskFailing.text")
                        : L.F("diag.diskFailing.textModel", disk.Model)
                });
            }
            if (disk.Kind == DiskKind.Hdd)
            {
                list.Add(new Finding
                {
                    Level = FindingLevel.Warning, Title = L.T("diag.hdd.title"),
                    Text = L.T("diag.hdd.text"),
                    ActionText = L.T("diag.hdd.action"), ActionPage = "tools-windows"
                });
            }
        }

        private static void CheckFreeSpace(List<Finding> list)
        {
            var d = SystemInfo.SystemDrive;
            double freePercent = 100.0 * d.AvailableFreeSpace / d.TotalSize;
            long freeGb = d.AvailableFreeSpace / (1024L * 1024 * 1024);
            if (freePercent >= 15 && freeGb >= 20) return;
            bool critical = freePercent < 8 || freeGb < 8;
            list.Add(new Finding
            {
                Level = critical ? FindingLevel.Danger : FindingLevel.Warning,
                Title = critical ? L.T("diag.diskFull.title") : L.T("diag.diskLow.title"),
                Text = L.F("diag.diskLow.text", FileCleaner.FormatSize(d.AvailableFreeSpace), d.Name.TrimEnd('\\')),
                ActionText = L.T("diag.diskLow.action"), ActionPage = "cleanup"
            });
        }

        private static void CheckMemory(List<Finding> list)
        {
            SystemInfo.GetMemory(out ulong total, out ulong avail);
            if (total == 0) return;
            double totalGb = total / (1024.0 * 1024 * 1024);
            double used = 100.0 * (total - avail) / total;

            if (totalGb < 7.5)
            {
                list.Add(new Finding
                {
                    Level = totalGb < 3.8 ? FindingLevel.Warning : FindingLevel.Info,
                    Title = L.F("diag.lowRam.title", Math.Round(totalGb)),
                    Text = L.T("diag.lowRam.text"),
                    ActionText = L.T("diag.lowRam.action"), ActionPage = "performance-memory"
                });
            }
            else if (used >= 85)
            {
                list.Add(new Finding
                {
                    Level = FindingLevel.Warning, Title = L.F("diag.ramFull.title", L.F("unit.percent", Math.Round(used))),
                    Text = L.T("diag.ramFull.text"),
                    ActionText = L.T("diag.ramFull.action"), ActionPage = "performance-memory"
                });
            }
        }

        private static void CheckStartup(List<Finding> list)
        {
            int enabled = StartupManager.List().Count(e => e.Enabled && !e.IsProtected);
            if (enabled < 8) return;
            list.Add(new Finding
            {
                Level = FindingLevel.Warning, Title = L.P("diag.startup.title", enabled),
                Text = L.T("diag.startup.text"),
                ActionText = L.T("diag.startup.action"), ActionPage = "performance"
            });
        }

        private static void CheckUptime(List<Finding> list)
        {
            var up = SystemInfo.Uptime;
            if (up.TotalDays < 5) return;
            list.Add(new Finding
            {
                Level = FindingLevel.Info, Title = L.P("diag.uptime.title", (int)up.TotalDays),
                Text = L.T("diag.uptime.text"),
                ActionText = L.T("diag.uptime.action"), ActionPage = "tools-windows"
            });
        }

        private static void CheckPendingReboot(List<Finding> list)
        {
            bool pending =
                RegistryHelper.KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                RegistryHelper.KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (!pending) return;
            list.Add(new Finding
            {
                Level = FindingLevel.Info, Title = L.T("diag.reboot.title"),
                Text = L.T("diag.reboot.text")
            });
        }

        private static void CheckPowerPlan(List<Finding> list)
        {
            if (!PowerOps.IsActive(PowerSaver)) return;
            list.Add(new Finding
            {
                Level = FindingLevel.Warning, Title = L.T("diag.powerSaver.title"),
                Text = L.T("diag.powerSaver.text")
            });
        }

        private static void CheckWindowsSupport(List<Finding> list)
        {
            if (SystemInfo.GetOs().Build >= 22000) return;
            list.Add(new Finding
            {
                Level = FindingLevel.Info, Title = L.T("diag.win10.title"),
                Text = L.T("diag.win10.text")
            });
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch { /* una comprobación que falla no debe impedir las demás */ }
        }
    }
}
