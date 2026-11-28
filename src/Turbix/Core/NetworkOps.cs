using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Turbix.Core
{
    /// <summary>Estados de una conexión TCP (los mismos números que usa Windows).</summary>
    public enum TcpState
    {
        None = 0, Closed = 1, Listen = 2, SynSent = 3, SynReceived = 4, Established = 5, FinWait1 = 6, FinWait2 = 7,
        CloseWait = 8, Closing = 9, LastAck = 10, TimeWait = 11, DeleteTcb = 12,
    }

    /// <summary>Dónde está el otro extremo: en este mismo equipo, en la red local o en Internet.</summary>
    public enum NetScope { ThisPc, Lan, Internet, Any }

    /// <summary>Un extremo de red abierto: una conexión TCP o un puerto UDP, con el proceso que lo tiene.</summary>
    public sealed class NetEndpoint
    {
        public bool IsTcp { get; set; }
        public IPAddress Local { get; set; }
        public IPAddress Remote { get; set; }
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
        public TcpState State { get; set; }
        public int Pid { get; set; }

        // Valores tal como los da Windows (orden de red): hacen falta para cerrar una conexión.
        internal uint RawLocal, RawRemote, RawLocalPort, RawRemotePort;

        public bool IsV6 => Local != null && Local.AddressFamily == AddressFamily.InterNetworkV6;
        public bool IsListening => IsTcp && State == TcpState.Listen;

        /// <summary>Identidad estable para actualizar la lista sin que salte (un PID se reutiliza, la conexión no).</summary>
        public string Key => (IsTcp ? "T" : "U") + "|" + Pid + "|" + Local + ":" + LocalPort + "|" + Remote + ":" + RemotePort;

        /// <summary>Solo las conexiones TCP de IPv4 se pueden cerrar con la API de Windows.</summary>
        public bool CanClose => IsTcp && !IsV6 && State != TcpState.Listen && State != TcpState.DeleteTcb && State != TcpState.Closed;
    }

    /// <summary>
    /// Conexiones y puertos abiertos, tal como los ve Windows (iphlpapi, la misma fuente que «netstat» y el Monitor
    /// de recursos). Solo lee, salvo cerrar una conexión, que pide permisos de administrador.
    /// </summary>
    public static class NetworkOps
    {
        private const int AfInet = 2, AfInet6 = 23;
        private const int TcpTableOwnerPidAll = 5, UdpTableOwnerPid = 1;
        private const uint ErrorInsufficientBuffer = 122;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcpRow
        {
            public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort;
        }

        [DllImport("iphlpapi.dll")]
        private static extern uint SetTcpEntry(ref MibTcpRow row);

        // ------------------------------------------------------------------ lectura

        /// <summary>Todas las conexiones TCP y los puertos UDP abiertos, de IPv4 e IPv6.</summary>
        public static List<NetEndpoint> Read()
        {
            var list = new List<NetEndpoint>();
            ReadTable(list, AfInet, true);
            ReadTable(list, AfInet6, true);
            ReadTable(list, AfInet, false);
            ReadTable(list, AfInet6, false);
            return list;
        }

        private static void ReadTable(List<NetEndpoint> output, int family, bool tcp)
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int size = 0;
                uint rc = 0;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    rc = tcp
                        ? GetExtendedTcpTable(buffer, ref size, false, family, TcpTableOwnerPidAll, 0)
                        : GetExtendedUdpTable(buffer, ref size, false, family, UdpTableOwnerPid, 0);
                    if (rc != ErrorInsufficientBuffer) break;
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal(size + 4096);
                    size += 4096;
                }
                if (rc != 0 || buffer == IntPtr.Zero) return;

                int count = Marshal.ReadInt32(buffer);
                IntPtr row = buffer + 4;
                for (int i = 0; i < count; i++)
                {
                    NetEndpoint e;
                    if (family == AfInet)
                    {
                        if (tcp)
                        {
                            e = new NetEndpoint
                            {
                                IsTcp = true,
                                State = (TcpState)Marshal.ReadInt32(row, 0),
                                RawLocal = (uint)Marshal.ReadInt32(row, 4),
                                RawLocalPort = (uint)Marshal.ReadInt32(row, 8),
                                RawRemote = (uint)Marshal.ReadInt32(row, 12),
                                RawRemotePort = (uint)Marshal.ReadInt32(row, 16),
                                Pid = Marshal.ReadInt32(row, 20),
                            };
                            e.Local = new IPAddress(e.RawLocal);
                            e.Remote = new IPAddress(e.RawRemote);
                            e.LocalPort = Port(e.RawLocalPort);
                            e.RemotePort = Port(e.RawRemotePort);
                            row += 24;
                        }
                        else
                        {
                            e = new NetEndpoint
                            {
                                RawLocal = (uint)Marshal.ReadInt32(row, 0),
                                RawLocalPort = (uint)Marshal.ReadInt32(row, 4),
                                Pid = Marshal.ReadInt32(row, 8),
                            };
                            e.Local = new IPAddress(e.RawLocal);
                            e.Remote = IPAddress.Any;
                            e.LocalPort = Port(e.RawLocalPort);
                            row += 12;
                        }
                    }
                    else if (tcp)
                    {
                        e = new NetEndpoint
                        {
                            IsTcp = true,
                            Local = Address6(row, 0, Marshal.ReadInt32(row, 16)),
                            LocalPort = Port((uint)Marshal.ReadInt32(row, 20)),
                            Remote = Address6(row, 24, Marshal.ReadInt32(row, 40)),
                            RemotePort = Port((uint)Marshal.ReadInt32(row, 44)),
                            State = (TcpState)Marshal.ReadInt32(row, 48),
                            Pid = Marshal.ReadInt32(row, 52),
                        };
                        row += 56;
                    }
                    else
                    {
                        e = new NetEndpoint
                        {
                            Local = Address6(row, 0, Marshal.ReadInt32(row, 16)),
                            Remote = IPAddress.IPv6Any,
                            LocalPort = Port((uint)Marshal.ReadInt32(row, 20)),
                            Pid = Marshal.ReadInt32(row, 24),
                        };
                        row += 28;
                    }
                    output.Add(e);
                }
            }
            catch
            {
                // Sin datos de esta tabla: se muestra lo demás.
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }

        private static int Port(uint raw) => (int)(((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF));

        private static IPAddress Address6(IntPtr row, int offset, int scope)
        {
            var bytes = new byte[16];
            Marshal.Copy(row + offset, bytes, 0, 16);
            return scope != 0 ? new IPAddress(bytes, scope) : new IPAddress(bytes);
        }

        // ------------------------------------------------------------------ cerrar

        /// <summary>
        /// Cierra una conexión TCP de IPv4 (la misma llamada que usa TCPView). Devuelve el error de Windows, o null si
        /// se cerró. El programa verá que la conexión se cortó y, si quiere, abrirá otra.
        /// </summary>
        public static string Close(NetEndpoint e)
        {
            if (e == null || !e.CanClose) return new Win32Exception(50).Message;
            var row = new MibTcpRow
            {
                State = (uint)TcpState.DeleteTcb,
                LocalAddr = e.RawLocal,
                LocalPort = e.RawLocalPort,
                RemoteAddr = e.RawRemote,
                RemotePort = e.RawRemotePort,
            };
            uint rc = SetTcpEntry(ref row);
            return rc == 0 ? null : new Win32Exception((int)rc).Message;
        }

        // ------------------------------------------------------------------ clasificación

        /// <summary>¿El otro extremo es este equipo, la red local o Internet?</summary>
        public static NetScope ScopeOf(IPAddress a)
        {
            if (a == null) return NetScope.Any;
            if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
            if (IPAddress.IsLoopback(a)) return NetScope.ThisPc;
            if (a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any)) return NetScope.Any;
            var b = a.GetAddressBytes();
            if (a.AddressFamily == AddressFamily.InterNetwork)
            {
                if (b[0] == 10) return NetScope.Lan;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return NetScope.Lan;
                if (b[0] == 192 && b[1] == 168) return NetScope.Lan;
                if (b[0] == 169 && b[1] == 254) return NetScope.Lan;
                if (b[0] >= 224) return NetScope.Lan;   // multidifusión y difusión
                return NetScope.Internet;
            }
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return NetScope.Lan;   // enlace local
            if ((b[0] & 0xFE) == 0xFC) return NetScope.Lan;                   // dirección local única
            if (b[0] == 0xFF) return NetScope.Lan;                            // multidifusión
            return NetScope.Internet;
        }

        /// <summary>Puertos conocidos, para decir «HTTPS» en lugar de solo «443».</summary>
        private static readonly Dictionary<int, string> Services = new Dictionary<int, string>
        {
            { 20, "FTP data" }, { 21, "FTP" }, { 22, "SSH" }, { 23, "Telnet" }, { 25, "SMTP" }, { 53, "DNS" }, { 67, "DHCP" },
            { 68, "DHCP" }, { 80, "HTTP" }, { 110, "POP3" }, { 123, "NTP" }, { 135, "RPC" }, { 137, "NetBIOS" }, { 138, "NetBIOS" },
            { 139, "NetBIOS" }, { 143, "IMAP" }, { 389, "LDAP" }, { 443, "HTTPS" }, { 445, "SMB" }, { 465, "SMTPS" }, { 514, "Syslog" },
            { 587, "SMTP" }, { 636, "LDAPS" }, { 993, "IMAPS" }, { 995, "POP3S" }, { 1433, "SQL Server" }, { 1900, "SSDP" },
            { 3306, "MySQL" }, { 3389, "RDP" }, { 5040, "Windows" }, { 5353, "mDNS" }, { 5355, "LLMNR" }, { 5432, "PostgreSQL" },
            { 5900, "VNC" }, { 5985, "WinRM" }, { 5986, "WinRM" }, { 6379, "Redis" }, { 8080, "HTTP" }, { 8443, "HTTPS" },
            { 9200, "Elasticsearch" }, { 11211, "Memcached" }, { 27017, "MongoDB" },
        };

        public static string ServiceName(int port) => Services.TryGetValue(port, out var s) ? s : null;

        /// <summary>
        /// Puertos que, abiertos a toda la red, conviene revisar: acceso remoto, bases de datos y recursos compartidos.
        /// Que estén abiertos no significa que haya un problema (en Windows, 135 y 445 son normales), significa que
        /// otros equipos pueden intentar entrar.
        /// </summary>
        private static readonly HashSet<int> Sensitive = new HashSet<int>
        {
            21, 23, 135, 139, 445, 1433, 3306, 3389, 5432, 5900, 5985, 5986, 6379, 9200, 11211, 27017,
        };

        public static bool IsSensitive(int port) => Sensitive.Contains(port);

        // ------------------------------------------------------------------ tarjetas de red

        /// <summary>Bytes recibidos y enviados por las tarjetas activas (sin el bucle local ni los túneles).</summary>
        public static bool Totals(out long received, out long sent, out long linkBitsPerSecond)
        {
            received = sent = linkBitsPerSecond = 0;
            bool any = false;
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var st = nic.GetIPStatistics();
                    received += st.BytesReceived;
                    sent += st.BytesSent;
                    if (nic.Speed > linkBitsPerSecond) linkBitsPerSecond = nic.Speed;
                    any = true;
                }
            }
            catch
            {
                return false;
            }
            return any;
        }

        /// <summary>Puerta de enlace y servidores DNS: bloquearlos cortaría la conexión a Internet.</summary>
        public static HashSet<string> InfrastructureAddresses()
        {
            var set = new HashSet<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    var props = nic.GetIPProperties();
                    foreach (var g in props.GatewayAddresses) set.Add(Normalize(g.Address));
                    foreach (var d in props.DnsAddresses) set.Add(Normalize(d));
                    foreach (var d in props.DhcpServerAddresses) set.Add(Normalize(d));
                }
            }
            catch
            {
                // Sin la lista: igualmente se protegen las direcciones locales.
            }
            set.Remove(IPAddress.Any.ToString());
            set.Remove(IPAddress.IPv6Any.ToString());
            return set;
        }

        /// <summary>La dirección sin el identificador de zona («fe80::1%12» → «fe80::1»), para comparar y para el cortafuegos.</summary>
        public static string Normalize(IPAddress a)
        {
            if (a == null) return string.Empty;
            if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
            string s = a.ToString();
            int pct = s.IndexOf('%');
            return pct > 0 ? s.Substring(0, pct) : s;
        }
    }
}
