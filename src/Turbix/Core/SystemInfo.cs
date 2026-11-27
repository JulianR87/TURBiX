using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;
using System.Globalization;
using Turbix.Localization;

namespace Turbix.Core
{
    public static class AppInfo
    {
        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }
    }

    public sealed class OsInfo
    {
        public string Product { get; set; }
        public string Edition { get; set; }
        public string EditionName { get; set; }
        public string DisplayVersion { get; set; }
        public int Build { get; set; }
        public int Ubr { get; set; }

        public string Caption => $"{Product} {EditionName}";
        public string VersionText => L.F("sys.osVersion", DisplayVersion, Build.ToString(CultureInfo.InvariantCulture), Ubr.ToString(CultureInfo.InvariantCulture));

        /// <summary>Solo Enterprise/Education/Server/IoT permiten apagar del todo los datos de diagnóstico.</summary>
        public bool SupportsTelemetryOff =>
            Edition != null && (Edition.IndexOf("Enterprise", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                Edition.IndexOf("Education", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                Edition.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                Edition.IndexOf("IoT", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static class SystemInfo
    {
        private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public static bool IsAdministrator()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static OsInfo GetOs()
        {
            string Get(string name) =>
                Convert.ToString(RegistryHelper.GetValue(RegistryHive.LocalMachine, CurrentVersionKey, name, out _));

            int.TryParse(Get("CurrentBuildNumber"), out int build);
            int.TryParse(Get("UBR"), out int ubr);
            string edition = Get("EditionID");
            string display = Get("DisplayVersion");
            if (string.IsNullOrEmpty(display)) display = Get("ReleaseId");

            return new OsInfo
            {
                // «ProductName» dice Windows 10 incluso en Windows 11: se usa la compilación.
                Product = build >= 22000 ? "Windows 11" : "Windows 10",
                Edition = edition,
                EditionName = FriendlyEdition(edition),
                DisplayVersion = display,
                Build = build,
                Ubr = ubr
            };
        }

        private static string FriendlyEdition(string id)
        {
            switch ((id ?? string.Empty).ToLowerInvariant())
            {
                case "core": return "Home";
                case "coresinglelanguage": return "Home Single Language";
                case "corecountryspecific": return "Home China";
                case "professional": return "Pro";
                case "professionalworkstation": return "Pro for Workstations";
                case "professionaleducation": return "Pro Education";
                case "enterprise": return "Enterprise";
                case "enterprises": return "Enterprise LTSC";
                case "education": return "Education";
                case "iotenterprise": return "IoT Enterprise";
                case "iotenterprises": return "IoT Enterprise LTSC";
                default: return id;
            }
        }

        public static string GetCpuName()
        {
            var v = RegistryHelper.GetValue(RegistryHive.LocalMachine,
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", out _);
            string name = Convert.ToString(v)?.Trim();
            if (string.IsNullOrEmpty(name)) name = L.T("sys.unknown");
            return L.P("sys.cpuThreads", Environment.ProcessorCount, System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " "));
        }

        public static string GetGpuName()
        {
            try
            {
                var names = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string n = Convert.ToString(mo["Name"]);
                        if (!string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
                    }
                }
                var real = names.Where(n => n.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) < 0 &&
                                            n.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0).ToList();
                var list = real.Count > 0 ? real : names;
                return list.Count > 0 ? string.Join(" + ", list.Distinct()) : L.T("sys.unknown");
            }
            catch
            {
                return L.T("sys.unknown");
            }
        }

        /// <summary>
        /// Usuario que inició la sesión de Windows. Puede no ser el mismo que ejecuta Turbix: si una cuenta
        /// estándar acepta el aviso de permisos con la contraseña de otro administrador, Turbix corre como ese otro.
        /// </summary>
        public static string SessionUserName()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
                if (!Native.WTSQuerySessionInformation(IntPtr.Zero, session, Native.WTSUserName, out buffer, out _)) return null;
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(buffer);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Native.WTSFreeMemory(buffer);
            }
        }

        /// <summary>true si Turbix se ejecuta con una cuenta distinta a la que inició sesión.</summary>
        public static bool RunsAsOtherUser()
        {
            string session = SessionUserName();
            return !string.IsNullOrEmpty(session) && !string.Equals(session, Environment.UserName, StringComparison.OrdinalIgnoreCase);
        }

        public static void GetMemory(out ulong total, out ulong available)
        {
            var m = new Native.MEMORYSTATUSEX();
            if (Native.GlobalMemoryStatusEx(m))
            {
                total = m.ullTotalPhys;
                available = m.ullAvailPhys;
            }
            else
            {
                total = 0;
                available = 0;
            }
        }

        public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Native.GetTickCount64());

        public static DriveInfo SystemDrive
        {
            get
            {
                string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                return new DriveInfo(root);
            }
        }

        public static long FreeSpace
        {
            get
            {
                try { return SystemDrive.AvailableFreeSpace; } catch { return 0; }
            }
        }

        public static string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return L.F("sys.uptime.days", (int)t.TotalDays, t.Hours);
            if (t.TotalHours >= 1) return L.F("sys.uptime.hours", t.Hours, t.Minutes);
            return L.F("sys.uptime.minutes", t.Minutes);
        }
    }
}
