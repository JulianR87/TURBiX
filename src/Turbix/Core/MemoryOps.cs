using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Turbix.Core
{
    public sealed class MemoryCleanResult
    {
        /// <summary>Memoria disponible ganada (puede ser 0 si no había nada que liberar).</summary>
        public long FreedBytes { get; set; }
        public bool WorkingSetsTrimmed { get; set; }
        public bool StandbyPurged { get; set; }
        public bool Ok => WorkingSetsTrimmed || StandbyPurged;
    }

    public sealed class ProcessMemory
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public long Bytes { get; set; }
    }

    /// <summary>
    /// Liberación de memoria RAM con las mismas llamadas que usa Windows internamente:
    /// 1) recorta el «conjunto de trabajo» de los procesos (memoria que tienen reservada y no usan ahora), y
    /// 2) vacía la lista «en espera» (caché de archivos ya cerrados).
    /// No cierra ningún programa ni borra datos: lo que un programa necesite lo vuelve a cargar solo.
    /// </summary>
    public static class MemoryOps
    {
        public static MemoryCleanResult Clean()
        {
            var result = new MemoryCleanResult();
            SystemInfo.GetMemory(out _, out ulong before);

            EnablePrivilege("SeProfileSingleProcessPrivilege");
            EnablePrivilege("SeIncreaseQuotaPrivilege");
            EnablePrivilege("SeDebugPrivilege");

            result.WorkingSetsTrimmed = MemoryListCommand(Native.MemoryEmptyWorkingSets) || TrimEachProcess();
            result.StandbyPurged = MemoryListCommand(Native.MemoryPurgeStandbyList);

            Thread.Sleep(400);   // dar tiempo a que el sistema actualice los contadores
            SystemInfo.GetMemory(out _, out ulong after);
            result.FreedBytes = after > before ? (long)(after - before) : 0;
            return result;
        }

        private static bool MemoryListCommand(int command)
        {
            try
            {
                return Native.NtSetSystemInformation(Native.SystemMemoryListInformation, ref command, sizeof(int)) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Alternativa proceso a proceso por si la orden global no está permitida.</summary>
        private static bool TrimEachProcess()
        {
            int ok = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (Native.EmptyWorkingSet(p.Handle)) ok++;
                }
                catch
                {
                    // procesos protegidos del sistema: se omiten
                }
                finally
                {
                    p.Dispose();
                }
            }
            return ok > 0;
        }

        private static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!Native.OpenProcessToken(Native.GetCurrentProcess(),
                        Native.TOKEN_ADJUST_PRIVILEGES | Native.TOKEN_QUERY, out token)) return false;
                if (!Native.LookupPrivilegeValue(null, name, out var luid)) return false;
                var tp = new Native.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1, Luid = luid, Attributes = Native.SE_PRIVILEGE_ENABLED
                };
                if (!Native.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) return false;
                return Marshal.GetLastWin32Error() != Native.ERROR_NOT_ALL_ASSIGNED;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (token != IntPtr.Zero) Native.CloseHandle(token);
            }
        }

        /// <summary>Programas que más memoria ocupan, sumando todos los procesos del mismo nombre.</summary>
        public static List<ProcessMemory> TopProcesses(int count)
        {
            var map = new Dictionary<string, ProcessMemory>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0) continue;   // «Idle» no es un programa
                    string name = p.ProcessName;
                    long bytes = p.WorkingSet64;
                    if (!map.TryGetValue(name, out var entry))
                        map[name] = entry = new ProcessMemory { Name = name };
                    entry.Count++;
                    entry.Bytes += bytes;
                }
                catch
                {
                    // el proceso terminó mientras se leía
                }
                finally
                {
                    p.Dispose();
                }
            }
            return map.Values.OrderByDescending(x => x.Bytes).Take(count).ToList();
        }
    }
}
