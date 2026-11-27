using System;
using System.Globalization;
using System.Linq;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>Acceso al registro siempre en la vista nativa (64 bits en Windows de 64 bits).</summary>
    public static class RegistryHelper
    {
        public static RegistryKey OpenBase(RegistryHive hive)
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(hive, view);
        }

        public static void Parse(string fullPath, out RegistryHive hive, out string subKey)
        {
            int slash = fullPath.IndexOf('\\');
            string root = slash < 0 ? fullPath : fullPath.Substring(0, slash);
            subKey = slash < 0 ? string.Empty : fullPath.Substring(slash + 1);
            switch (root.ToUpperInvariant())
            {
                case "HKLM":
                case "HKEY_LOCAL_MACHINE": hive = RegistryHive.LocalMachine; break;
                case "HKCU":
                case "HKEY_CURRENT_USER": hive = RegistryHive.CurrentUser; break;
                case "HKU":
                case "HKEY_USERS": hive = RegistryHive.Users; break;
                default: throw new ArgumentException(L.F("core.registry.badRoot", root));
            }
        }

        public static string HiveName(RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.LocalMachine: return "HKLM";
                case RegistryHive.CurrentUser: return "HKCU";
                case RegistryHive.Users: return "HKU";
                default: return hive.ToString();
            }
        }

        public static bool KeyExists(RegistryHive hive, string subKey)
        {
            try
            {
                using (var b = OpenBase(hive))
                using (var k = b.OpenSubKey(subKey, false))
                    return k != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Devuelve el valor (o null si no existe) y su tipo.</summary>
        public static object GetValue(RegistryHive hive, string subKey, string name, out RegistryValueKind kind)
        {
            kind = RegistryValueKind.Unknown;
            try
            {
                using (var b = OpenBase(hive))
                using (var k = b.OpenSubKey(subKey, false))
                {
                    if (k == null) return null;
                    if (!k.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
                    kind = k.GetValueKind(name);
                    return k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                }
            }
            catch
            {
                return null;
            }
        }

        public static void SetValue(RegistryHive hive, string subKey, string name, object value, RegistryValueKind kind)
        {
            using (var b = OpenBase(hive))
            using (var k = b.CreateSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (k == null) throw new InvalidOperationException(L.F("core.registry.createKeyFailed", subKey));
                k.SetValue(name, value, kind);
            }
        }

        public static void DeleteValue(RegistryHive hive, string subKey, string name)
        {
            using (var b = OpenBase(hive))
            using (var k = b.OpenSubKey(subKey, true))
                k?.DeleteValue(name, false);
        }

        public static void DeleteKeyIfEmpty(RegistryHive hive, string subKey)
        {
            using (var b = OpenBase(hive))
            {
                using (var k = b.OpenSubKey(subKey, false))
                {
                    if (k == null || k.ValueCount > 0 || k.SubKeyCount > 0) return;
                }
                b.DeleteSubKey(subKey, false);
            }
        }

        public static bool ValueEquals(object current, object desired)
        {
            if (current == null || desired == null) return false;
            return string.Equals(
                Convert.ToString(current, CultureInfo.InvariantCulture),
                Convert.ToString(desired, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
