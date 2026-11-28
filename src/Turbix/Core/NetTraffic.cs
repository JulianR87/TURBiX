using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace Turbix.Core
{
    /// <summary>Bytes que un programa recibió y envió desde que empezó la medición.</summary>
    public struct TrafficCount
    {
        public long Received, Sent;
    }

    /// <summary>
    /// Tráfico de red por programa, con el seguimiento de eventos de Windows (ETW): el proveedor
    /// «Microsoft-Windows-Kernel-Network», la misma fuente que usa el Monitor de recursos. Cuenta TCP y UDP, IPv4 e
    /// IPv6, y no instala nada ni mira el contenido: de cada paquete solo toma qué programa lo movió y cuántos bytes.
    /// Hace falta ser administrador. La sesión existe solo mientras se mide y se cierra al dejar de mirar.
    /// </summary>
    internal static class NetTraffic
    {
        private const string SessionName = "TURBiX-Network";
        private static readonly Guid Provider = new Guid("7DD42A49-5329-4832-8DFD-43D979153A88");

        private const uint WnodeFlagTracedGuid = 0x00020000;
        private const uint RealTimeMode = 0x00000100;
        private const uint ProcessTraceRealTime = 0x00000100, ProcessTraceEventRecord = 0x10000000;
        private const uint ControlStop = 1;
        private const uint ErrorAlreadyExists = 183;

        [StructLayout(LayoutKind.Sequential)]
        private struct WnodeHeader
        {
            public uint BufferSize, ProviderId;
            public ulong HistoricalContext;
            public long TimeStamp;
            public Guid Guid;
            public uint ClientContext, Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EventTraceProperties
        {
            public WnodeHeader Wnode;
            public uint BufferSize, MinimumBuffers, MaximumBuffers, MaximumFileSize, LogFileMode, FlushTimer, EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers, FreeBuffers, EventsLost, BuffersWritten, LogBuffersLost, RealTimeBuffersLost;
            public IntPtr LoggerThreadId;
            public uint LogFileNameOffset, LoggerNameOffset;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void EventRecordCallback(IntPtr record);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint StartTraceW(out ulong handle, string name, IntPtr properties);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ControlTraceW(ulong handle, string name, IntPtr properties, uint control);

        [DllImport("advapi32.dll")]
        private static extern uint EnableTraceEx2(ulong handle, ref Guid provider, uint control, byte level, ulong anyKeyword,
                                                  ulong allKeyword, uint timeout, IntPtr parameters);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern ulong OpenTraceW(IntPtr logfile);

        [DllImport("advapi32.dll")]
        private static extern uint ProcessTrace(ulong[] handles, uint count, IntPtr start, IntPtr end);

        [DllImport("advapi32.dll")]
        private static extern uint CloseTrace(ulong handle);

        private static readonly object Sync = new object();
        private static readonly Dictionary<int, TrafficCount> Counts = new Dictionary<int, TrafficCount>();
        private static readonly EventRecordCallback Callback = OnEvent;   // referencia fija: el recolector no puede llevársela

        private static ulong _session, _consumer;
        private static Thread _thread;
        private static IntPtr _loggerName = IntPtr.Zero, _logfile = IntPtr.Zero;

        public static bool IsRunning { get; private set; }

        /// <summary>Empieza a medir. Devuelve el motivo si no se pudo (sin permisos, Windows de 32 bits…), o null.</summary>
        public static string Start()
        {
            lock (Sync)
            {
                if (IsRunning) return null;
                // La estructura de lectura tiene otro tamaño en Windows de 32 bits y no se probó: sin datos de tráfico ahí.
                if (IntPtr.Size != 8) return "32-bit";
                try
                {
                    Counts.Clear();
                    IntPtr props = NewProperties(out int total);
                    try
                    {
                        uint rc = StartTraceW(out _session, SessionName, props);
                        if (rc == ErrorAlreadyExists)
                        {
                            // Quedó una sesión de una vez anterior (por ejemplo, si TURBiX se cerró a la fuerza).
                            IntPtr old = NewProperties(out _);
                            try { ControlTraceW(0, SessionName, old, ControlStop); }
                            finally { Marshal.FreeHGlobal(old); }
                            Marshal.FreeHGlobal(props);
                            props = NewProperties(out total);
                            rc = StartTraceW(out _session, SessionName, props);
                        }
                        if (rc != 0) return new Win32Exception((int)rc).Message;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(props);
                    }

                    Guid provider = Provider;
                    uint enable = EnableTraceEx2(_session, ref provider, 1, 5, 0, 0, 0, IntPtr.Zero);
                    if (enable != 0)
                    {
                        StopSession();
                        return new Win32Exception((int)enable).Message;
                    }

                    _logfile = NewLogfile(out _loggerName);
                    _consumer = OpenTraceW(_logfile);
                    if (_consumer == ulong.MaxValue || _consumer == 0xFFFFFFFF || _consumer == 0)
                    {
                        int err = Marshal.GetLastWin32Error();
                        StopSession();
                        FreeLogfile();
                        return new Win32Exception(err).Message;
                    }

                    ulong handle = _consumer;
                    _thread = new Thread(() => { try { ProcessTrace(new[] { handle }, 1, IntPtr.Zero, IntPtr.Zero); } catch { /* termina */ } })
                    {
                        IsBackground = true,
                        Name = "TURBiX network trace",
                    };
                    _thread.Start();
                    IsRunning = true;
                    return null;
                }
                catch (Exception ex)
                {
                    try { StopSession(); } catch { /* ya está cerrada */ }
                    return ex.Message;
                }
            }
        }

        /// <summary>Deja de medir y cierra la sesión (no queda nada funcionando en Windows).</summary>
        public static void Stop()
        {
            lock (Sync)
            {
                if (!IsRunning) return;
                IsRunning = false;
                try { StopSession(); } catch { /* ya está cerrada */ }
                try { if (_consumer != 0) CloseTrace(_consumer); } catch { /* ya está cerrada */ }
                _consumer = 0;
                _thread?.Join(1500);
                _thread = null;
                FreeLogfile();
            }
        }

        /// <summary>Copia de lo contado hasta ahora, por PID.</summary>
        public static Dictionary<int, TrafficCount> Snapshot()
        {
            lock (Sync) return new Dictionary<int, TrafficCount>(Counts);
        }

        // ------------------------------------------------------------------ interno

        private static void StopSession()
        {
            IntPtr props = NewProperties(out _);
            try { ControlTraceW(0, SessionName, props, ControlStop); }
            finally { Marshal.FreeHGlobal(props); }
            _session = 0;
        }

        private static IntPtr NewProperties(out int total)
        {
            int header = Marshal.SizeOf(typeof(EventTraceProperties));
            total = header + (SessionName.Length + 1) * 2;
            IntPtr mem = Marshal.AllocHGlobal(total);
            for (int i = 0; i < total; i++) Marshal.WriteByte(mem, i, 0);
            var p = new EventTraceProperties();
            p.Wnode.BufferSize = (uint)total;
            p.Wnode.Flags = WnodeFlagTracedGuid;
            p.Wnode.ClientContext = 1;                 // reloj de alta resolución
            p.BufferSize = 32;                         // KB: poco, solo se cuentan bytes
            p.MinimumBuffers = 2;
            p.MaximumBuffers = 8;
            p.LogFileMode = RealTimeMode;
            p.FlushTimer = 1;
            p.LoggerNameOffset = (uint)header;
            Marshal.StructureToPtr(p, mem, false);
            Marshal.Copy(SessionName.ToCharArray(), 0, mem + header, SessionName.Length);
            return mem;
        }

        private static int Align(int value, int boundary) => (value + boundary - 1) / boundary * boundary;

        /// <summary>
        /// EVENT_TRACE_LOGFILEW de 64 bits armado a mano: solo se rellenan el nombre de la sesión, el modo y la función
        /// que recibe los eventos. Las posiciones se calculan igual que lo hace el compilador de C.
        /// </summary>
        private static IntPtr NewLogfile(out IntPtr loggerName)
        {
            int p = IntPtr.Size;
            int offCurrentTime = Align(2 * p, 8);
            int offMode = offCurrentTime + 8 + 4;
            int offCurrentEvent = Align(offMode + 4, 8);
            const int eventTraceSize = 88, logfileHeaderSize64 = 280;
            int offHeader = offCurrentEvent + eventTraceSize;
            int offBufferCallback = Align(offHeader + logfileHeaderSize64, p);
            int offEventCallback = Align(offBufferCallback + p + 12, p);
            int offKernel = offEventCallback + p;
            int offContext = Align(offKernel + 4, p);
            int total = offContext + p;

            IntPtr mem = Marshal.AllocHGlobal(total);
            for (int i = 0; i < total; i++) Marshal.WriteByte(mem, i, 0);
            loggerName = Marshal.StringToHGlobalUni(SessionName);
            Marshal.WriteIntPtr(mem, p, loggerName);                                      // LoggerName
            Marshal.WriteInt32(mem, offMode, unchecked((int)(ProcessTraceRealTime | ProcessTraceEventRecord)));
            Marshal.WriteIntPtr(mem, offEventCallback, Marshal.GetFunctionPointerForDelegate(Callback));
            return mem;
        }

        private static void FreeLogfile()
        {
            if (_loggerName != IntPtr.Zero) { Marshal.FreeHGlobal(_loggerName); _loggerName = IntPtr.Zero; }
            if (_logfile != IntPtr.Zero) { Marshal.FreeHGlobal(_logfile); _logfile = IntPtr.Zero; }
        }

        /// <summary>
        /// ¿El paquete va de este equipo a este mismo equipo? Los eventos de datos traen, después del PID y los bytes,
        /// la dirección de destino y la de origen (4 bytes cada una en IPv4, 16 en IPv6).
        /// </summary>
        private static bool IsLoopback(IntPtr data, int length, int id)
        {
            bool v6 = id == 26 || id == 27 || id == 58 || id == 59;
            int size = v6 ? 16 : 4;
            if (length < 8 + 2 * size) return false;
            for (int which = 0; which < 2; which++)
            {
                int at = 8 + which * size;
                if (!v6)
                {
                    if (Marshal.ReadByte(data, at) == 127) return true;
                    continue;
                }
                bool zeros = true;
                for (int i = 0; i < 15 && zeros; i++) zeros = Marshal.ReadByte(data, at + i) == 0;
                if (zeros && Marshal.ReadByte(data, at + 15) == 1) return true;
            }
            return false;
        }

        /// <summary>
        /// Un evento de red. Los de datos enviados y recibidos (TCP y UDP, IPv4 e IPv6) empiezan igual: el PID del
        /// programa y los bytes. Lo demás (direcciones, puertos) ya lo da la tabla de conexiones.
        /// </summary>
        private static void OnEvent(IntPtr record)
        {
            try
            {
                int id = (ushort)Marshal.ReadInt16(record, 40);   // EVENT_RECORD.EventHeader.EventDescriptor.Id
                bool send;
                switch (id)
                {
                    case 10: case 26: case 42: case 58: send = true; break;
                    case 11: case 27: case 43: case 59: send = false; break;
                    default: return;
                }
                int length = (ushort)Marshal.ReadInt16(record, 86);   // UserDataLength
                if (length < 8) return;
                IntPtr data = Marshal.ReadIntPtr(record, 88 + IntPtr.Size);   // UserData
                if (data == IntPtr.Zero) return;
                // El tráfico de este mismo equipo (127.0.0.1, ::1) no pasa por la red: no se cuenta.
                if (IsLoopback(data, length, id)) return;
                int pid = Marshal.ReadInt32(data, 0);
                uint size = (uint)Marshal.ReadInt32(data, 4);
                lock (Sync)
                {
                    Counts.TryGetValue(pid, out var c);
                    if (send) c.Sent += size; else c.Received += size;
                    Counts[pid] = c;
                }
            }
            catch
            {
                // Un evento ilegible se ignora.
            }
        }
    }
}
