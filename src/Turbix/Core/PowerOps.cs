using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>Planes de energía de Windows.</summary>
    public static class PowerOps
    {
        public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        private const string SchemesKey = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";

        /// <summary>GUID del plan activo, o null si no se pudo leer.</summary>
        public static string GetActive()
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                if (Native.PowerGetActiveScheme(IntPtr.Zero, out ptr) != 0 || ptr == IntPtr.Zero) return null;
                var guid = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
                return guid.ToString("D");
            }
            catch
            {
                return null;
            }
            finally
            {
                if (ptr != IntPtr.Zero) Native.LocalFree(ptr);
            }
        }

        /// <summary>
        /// Indica si el plan existe en este equipo. Muchos portátiles modernos
        /// (Modern Standby) solo traen el plan «Equilibrado».
        /// </summary>
        public static bool Exists(string guid) =>
            RegistryHelper.KeyExists(RegistryHive.LocalMachine, SchemesKey + "\\" + guid);

        public static bool IsActive(string guid) =>
            string.Equals(GetActive(), guid, StringComparison.OrdinalIgnoreCase);

        public static void SetActive(string guid)
        {
            var g = new Guid(guid);
            uint err = Native.PowerSetActiveScheme(IntPtr.Zero, ref g);
            if (err != 0) throw new InvalidOperationException(L.F("gaming.power.setError", err));
        }
    }
}
