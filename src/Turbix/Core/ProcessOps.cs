using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Turbix.Core
{
    /// <summary>Datos de un proceso en una toma (los mismos que usa el Administrador de tareas).</summary>
    public sealed class ProcessSample
    {
        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public string ImageName { get; set; }
        public long CreateTime { get; set; }
        public long CpuTime { get; set; }            // usuario + núcleo, en unidades de 100 ns
        public long PrivateWorkingSet { get; set; }  // «Memoria» del Administrador de tareas
        public int Threads { get; set; }
        public int Handles { get; set; }
        public int SessionId { get; set; }

        /// <summary>Identidad estable: un PID se reutiliza, pero no con la misma hora de creación.</summary>
        public string Key => Pid + ":" + CreateTime;
    }

    public sealed class ProcessSnapshot
    {
        public DateTime Taken { get; set; }
        public List<ProcessSample> Processes { get; set; }
        public double CpuPercent { get; set; }
        public long MemoryUsed { get; set; }
        public long MemoryTotal { get; set; }
    }

    /// <summary>
    /// Lista de procesos con NtQuerySystemInformation (una sola llamada para todos, sin abrir cada proceso, así que
    /// también funciona con los procesos protegidos y sin permisos de administrador).
    /// </summary>
    public static class ProcessOps
    {
        private const int SystemProcessInformation = 5;
        private const uint StatusInfoLengthMismatch = 0xC0000004;
        private const int QueryLimitedInformation = 0x1000;
        private const int Terminate = 0x0001;

        private static IntPtr _buffer = IntPtr.Zero;
        private static int _bufferSize;
        private static readonly object Sync = new object();
        private static long _lastIdle, _lastKernel, _lastUser;

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        // Solo los campos que se leen; el resto de la estructura de Windows queda después y no hace falta declararlo.
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemProcessInfo
        {
            public uint NextEntryOffset;
            public uint NumberOfThreads;
            public long WorkingSetPrivateSize;
            public uint HardFaultCount;
            public uint NumberOfThreadsHighWatermark;
            public ulong CycleTime;
            public long CreateTime;
            public long UserTime;
            public long KernelTime;
            public UnicodeString ImageName;
            public int BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
            public uint HandleCount;
            public uint SessionId;
        }

        [DllImport("ntdll.dll")]
        private static extern uint NtQuerySystemInformation(int infoClass, IntPtr info, int size, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

        public static ProcessSnapshot Take()
        {
            lock (Sync)
            {
                var list = new List<ProcessSample>(256);
                if (_buffer == IntPtr.Zero) Grow(512 * 1024);
                for (int attempt = 0; ; attempt++)
                {
                    uint status = NtQuerySystemInformation(SystemProcessInformation, _buffer, _bufferSize, out int needed);
                    if (status == StatusInfoLengthMismatch && attempt < 5)
                    {
                        Grow(Math.Max(needed, _bufferSize) + 64 * 1024);
                        continue;
                    }
                    if (status != 0) throw new Win32Exception((int)status);
                    break;
                }

                long offset = 0;
                while (true)
                {
                    var p = new IntPtr(_buffer.ToInt64() + offset);
                    var info = (SystemProcessInfo)Marshal.PtrToStructure(p, typeof(SystemProcessInfo));
                    int pid = info.UniqueProcessId.ToInt32();
                    if (pid != 0)   // 0 = «Proceso inactivo del sistema», no es un proceso real
                    {
                        string name = info.ImageName.Buffer != IntPtr.Zero && info.ImageName.Length > 0
                            ? Marshal.PtrToStringUni(info.ImageName.Buffer, info.ImageName.Length / 2)
                            : (pid == 4 ? "System" : "?");
                        list.Add(new ProcessSample
                        {
                            Pid = pid,
                            ParentPid = info.InheritedFromUniqueProcessId.ToInt32(),
                            ImageName = name,
                            CreateTime = info.CreateTime,
                            CpuTime = info.UserTime + info.KernelTime,
                            PrivateWorkingSet = info.WorkingSetPrivateSize,
                            Threads = (int)info.NumberOfThreads,
                            Handles = (int)info.HandleCount,
                            SessionId = (int)info.SessionId,
                        });
                    }
                    if (info.NextEntryOffset == 0) break;
                    offset += info.NextEntryOffset;
                }

                var snap = new ProcessSnapshot { Taken = DateTime.UtcNow, Processes = list, CpuPercent = SystemCpu() };
                var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
                if (GlobalMemoryStatusEx(ref mem))
                {
                    snap.MemoryTotal = (long)mem.TotalPhys;
                    snap.MemoryUsed = (long)(mem.TotalPhys - mem.AvailPhys);
                }
                return snap;
            }
        }

        private static void Grow(int size)
        {
            if (_buffer != IntPtr.Zero) Marshal.FreeHGlobal(_buffer);
            _buffer = Marshal.AllocHGlobal(size);
            _bufferSize = size;
        }

        /// <summary>Uso total del procesador desde la toma anterior (el «Uso» del Administrador de tareas).</summary>
        private static double SystemCpu()
        {
            if (!GetSystemTimes(out long idle, out long kernel, out long user)) return 0;
            long dIdle = idle - _lastIdle, dKernel = kernel - _lastKernel, dUser = user - _lastUser;
            bool first = _lastKernel == 0;
            _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
            long total = dKernel + dUser;   // el tiempo de núcleo incluye el inactivo
            if (first || total <= 0) return 0;
            return Math.Max(0, Math.Min(100, 100.0 * (total - dIdle) / total));
        }

        /// <summary>Ruta completa del ejecutable, o null si Windows no la da (procesos protegidos sin permisos).</summary>
        public static string ImagePath(int pid)
        {
            IntPtr h = OpenProcess(QueryLimitedInformation, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        /// <summary>Descripción del archivo («Google Chrome» en lugar de chrome.exe).</summary>
        public static string Description(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                string d = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                return string.IsNullOrEmpty(d) ? null : d;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Servicios que corren dentro de cada proceso (para nombrar cada «svchost.exe»).</summary>
        public static Dictionary<int, List<string>> ServicesByPid()
        {
            var result = new Dictionary<int, List<string>>();
            IntPtr scm = OpenSCManager(null, null, 0x0004);   // SC_MANAGER_ENUMERATE_SERVICE
            if (scm == IntPtr.Zero) return result;
            IntPtr buf = IntPtr.Zero;
            try
            {
                int resume = 0;
                EnumServicesStatusEx(scm, 0, 0x30, 1, IntPtr.Zero, 0, out int needed, out int count, ref resume, null);
                if (needed <= 0) return result;
                buf = Marshal.AllocHGlobal(needed);
                resume = 0;
                if (!EnumServicesStatusEx(scm, 0, 0x30, 1, buf, needed, out needed, out count, ref resume, null)) return result;
                int size = Marshal.SizeOf(typeof(EnumServiceStatusProcess));
                for (int i = 0; i < count; i++)
                {
                    var e = (EnumServiceStatusProcess)Marshal.PtrToStructure(new IntPtr(buf.ToInt64() + (long)i * size), typeof(EnumServiceStatusProcess));
                    if (e.ProcessId == 0) continue;
                    string display = Marshal.PtrToStringUni(e.DisplayName) ?? Marshal.PtrToStringUni(e.ServiceName);
                    if (!result.TryGetValue((int)e.ProcessId, out var names)) result[(int)e.ProcessId] = names = new List<string>();
                    names.Add(display);
                }
            }
            catch
            {
                // Sin acceso al administrador de servicios: los «svchost.exe» se muestran sin nombre de servicio.
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                CloseServiceHandle(scm);
            }
            return result;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EnumServiceStatusProcess
        {
            public IntPtr ServiceName;
            public IntPtr DisplayName;
            public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode, CheckPoint, WaitHint;
            public uint ProcessId;
            public uint ServiceFlags;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machine, string database, int access);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumServicesStatusEx(IntPtr scm, int infoLevel, int serviceType, int serviceState, IntPtr services,
            int bufSize, out int bytesNeeded, out int servicesReturned, ref int resumeHandle, string groupName);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        // ------------------------------------------------------------------ finalizar

        /// <summary>
        /// Procesos que Turbix no deja finalizar: cerrarlos cuelga o reinicia Windows, o se vuelven a abrir solos.
        /// El Explorador se reinicia desde Soluciones › Reparar.
        /// </summary>
        private static readonly HashSet<string> Critical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Registry", "Memory Compression", "Secure System", "smss.exe", "csrss.exe", "wininit.exe",
            "winlogon.exe", "services.exe", "lsass.exe", "lsaiso.exe", "svchost.exe", "dwm.exe", "fontdrvhost.exe",
            "MsMpEng.exe", "NisSrv.exe", "SecurityHealthService.exe", "SgrmBroker.exe", "WUDFHost.exe", "LsaIso.exe",
            "explorer.exe", "sihost.exe", "ctfmon.exe", "audiodg.exe", "spoolsv.exe",
        };

        public static bool IsCritical(ProcessSample p) =>
            p.Pid <= 4 || Critical.Contains(p.ImageName) || p.Pid == Process.GetCurrentProcess().Id;

        /// <summary>
        /// Finaliza el proceso si sigue siendo el mismo (mismo PID y misma hora de creación). Devuelve el error de
        /// Windows si no se pudo, o null si terminó.
        /// </summary>
        public static string Kill(ProcessSample p)
        {
            IntPtr h = OpenProcess(Terminate | QueryLimitedInformation, false, p.Pid);
            if (h == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                return err == 87 ? null : new Win32Exception(err).Message;   // 87: ya no existe
            }
            try
            {
                if (GetProcessTimes(h, out long created, out _, out _, out _) && created != p.CreateTime) return null;   // otro proceso con el mismo PID
                return TerminateProcess(h, 1) ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            }
            finally
            {
                CloseHandle(h);
            }
        }
    }
}
