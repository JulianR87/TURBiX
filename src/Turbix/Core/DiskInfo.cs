using System;
using System.IO;
using System.Management;

namespace Turbix.Core
{
    public enum DiskKind { Unknown, Hdd, Ssd }

    public sealed class SystemDiskInfo
    {
        public DiskKind Kind { get; set; }
        public string Model { get; set; }

        /// <summary>false solo si Windows informa de que el disco tiene problemas.</summary>
        public bool Healthy { get; set; } = true;
    }

    /// <summary>Tipo (HDD/SSD) y salud del disco donde está instalado Windows.</summary>
    public static class DiskInfo
    {
        private static SystemDiskInfo _cached;

        public static SystemDiskInfo GetSystemDisk()
        {
            if (_cached != null) return _cached;
            var info = new SystemDiskInfo();
            try
            {
                string letter = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Substring(0, 1);
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();

                int diskNumber = -1;
                using (var s = new ManagementObjectSearcher(scope,
                           new ObjectQuery($"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{letter}'")))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        diskNumber = Convert.ToInt32(mo["DiskNumber"]);
                        break;
                    }
                }

                if (diskNumber >= 0)
                {
                    using (var s = new ManagementObjectSearcher(scope,
                               new ObjectQuery("SELECT DeviceId, FriendlyName, MediaType, HealthStatus FROM MSFT_PhysicalDisk")))
                    {
                        foreach (ManagementObject mo in s.Get())
                        {
                            if (Convert.ToString(mo["DeviceId"]) != diskNumber.ToString()) continue;
                            int media = Convert.ToInt32(mo["MediaType"] ?? 0);        // 3 = HDD, 4 = SSD
                            int health = Convert.ToInt32(mo["HealthStatus"] ?? 0);    // 0 = correcto
                            info.Kind = media == 3 ? DiskKind.Hdd : media == 4 ? DiskKind.Ssd : DiskKind.Unknown;
                            info.Model = Convert.ToString(mo["FriendlyName"])?.Trim();
                            info.Healthy = health == 0;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Sin acceso a la información de almacenamiento: se queda como «desconocido».
            }
            _cached = info;
            return info;
        }

        public static bool SystemDiskIsHdd() => GetSystemDisk().Kind == DiskKind.Hdd;
    }
}
