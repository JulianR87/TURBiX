using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Turbix.Core
{
    public enum BlockKind { App, Address, Port }

    /// <summary>Un bloqueo creado por TURBiX en el Firewall de Windows (puede estar formado por una o dos reglas).</summary>
    public sealed class BlockEntry
    {
        public BlockKind Kind { get; set; }
        /// <summary>Ruta del programa, dirección IP o «TCP 445».</summary>
        public string Target { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Inbound { get; set; }
        public bool Outbound { get; set; }
        public List<string> RuleNames { get; } = new List<string>();
    }

    /// <summary>
    /// Bloqueos con el Firewall de Windows (la interfaz COM «HNetCfg.FwPolicy2», incluida en Windows). Todo lo que crea
    /// TURBiX queda en el grupo «TURBiX» y se puede ver y borrar también desde «Firewall de Windows Defender con
    /// seguridad avanzada». No toca ninguna regla que no sea suya.
    /// </summary>
    public static class FirewallOps
    {
        public const string Group = "TURBiX";
        private const int In = 1, Out = 2;
        private const int Tcp = 6, Udp = 17, AnyProtocol = 256;
        private const int AllProfiles = 0x7FFFFFFF;

        // ------------------------------------------------------------------ COM sin referencias extra

        private static object Create(string progId) => Activator.CreateInstance(Type.GetTypeFromProgID(progId, true));

        private static object Get(object o, string name, params object[] args) =>
            o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, args);

        private static void Set(object o, string name, object value) =>
            o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, new[] { value });

        private static object Call(object o, string name, params object[] args) =>
            o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args);

        private static string Describe(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex.Message.Trim();
        }

        // ------------------------------------------------------------------ estado

        /// <summary>
        /// true si el Firewall de Windows está activado en todos los perfiles de red en uso. Si está apagado (o lo
        /// reemplaza el de un antivirus), las reglas se guardan pero no bloquean nada.
        /// </summary>
        public static bool IsActive()
        {
            try
            {
                object policy = Create("HNetCfg.FwPolicy2");
                int current = Convert.ToInt32(Get(policy, "CurrentProfileTypes"));
                foreach (int profile in new[] { 1, 2, 4 })
                {
                    if ((current & profile) == 0) continue;
                    if (!Convert.ToBoolean(Get(policy, "FirewallEnabled", profile))) return false;
                }
                return true;
            }
            catch
            {
                return true;   // si no se puede leer, no se avisa de algo que quizá no pasa
            }
        }

        // ------------------------------------------------------------------ lista

        /// <summary>Los bloqueos de TURBiX, juntando las reglas de entrada y salida de cada uno.</summary>
        public static List<BlockEntry> List(out string error)
        {
            error = null;
            var result = new List<BlockEntry>();
            try
            {
                object policy = Create("HNetCfg.FwPolicy2");
                object rules = Get(policy, "Rules");
                var byKey = new Dictionary<string, BlockEntry>();
                foreach (object rule in (IEnumerable)rules)
                {
                    try
                    {
                        if (!string.Equals(Get(rule, "Grouping") as string, Group, StringComparison.Ordinal)) continue;
                        string name = (string)Get(rule, "Name");
                        string app = Get(rule, "ApplicationName") as string;
                        string remote = Get(rule, "RemoteAddresses") as string;
                        string ports = Get(rule, "LocalPorts") as string;
                        int direction = Convert.ToInt32(Get(rule, "Direction"));
                        int protocol = Convert.ToInt32(Get(rule, "Protocol"));
                        bool enabled = Convert.ToBoolean(Get(rule, "Enabled"));

                        BlockKind kind;
                        string target;
                        if (!string.IsNullOrEmpty(app)) { kind = BlockKind.App; target = app; }
                        else if (!string.IsNullOrEmpty(ports) && ports != "*")
                        {
                            kind = BlockKind.Port;
                            target = (protocol == Udp ? "UDP " : "TCP ") + ports;
                        }
                        else if (!string.IsNullOrEmpty(remote) && remote != "*") { kind = BlockKind.Address; target = CleanAddress(remote); }
                        else continue;

                        string key = kind + "|" + target;
                        if (!byKey.TryGetValue(key, out var entry))
                        {
                            entry = new BlockEntry { Kind = kind, Target = target, Enabled = enabled };
                            byKey[key] = entry;
                            result.Add(entry);
                        }
                        entry.RuleNames.Add(name);
                        entry.Enabled &= enabled;
                        if (direction == In) entry.Inbound = true; else entry.Outbound = true;
                    }
                    catch
                    {
                        // Una regla ilegible no impide mostrar las demás.
                    }
                }
            }
            catch (Exception ex)
            {
                error = Describe(ex);
            }
            return result.OrderBy(e => e.Kind).ThenBy(e => e.Target, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Windows devuelve «203.0.113.9/255.255.255.255» o «2001:db8::9-2001:db8::9»: se muestra solo la dirección.</summary>
        private static string CleanAddress(string value)
        {
            value = value.Trim();
            int slash = value.IndexOf('/');
            if (slash > 0)
            {
                string mask = value.Substring(slash + 1);
                if (mask == "255.255.255.255" || mask == "128") value = value.Substring(0, slash);
            }
            int dash = value.IndexOf('-');
            if (dash > 0 && string.Equals(value.Substring(0, dash), value.Substring(dash + 1), StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, dash);
            return value;
        }

        // ------------------------------------------------------------------ crear

        /// <summary>Corta el acceso a la red de un programa (entrada y salida). Devuelve el error, o null.</summary>
        public static string BlockApp(string path, string title)
        {
            if (string.IsNullOrEmpty(path)) return "path";
            string file = Path.GetFileName(path);
            string id = Short(path);
            return Both(Name("app", file + " " + id), title, path, null, AnyProtocol, null);
        }

        /// <summary>Corta todo el tráfico con una dirección IP (entrada y salida).</summary>
        public static string BlockAddress(string address, string title)
        {
            if (string.IsNullOrEmpty(address)) return "address";
            return Both(Name("ip", address), title, null, address, AnyProtocol, null);
        }

        /// <summary>Impide que otros equipos se conecten a un puerto de este (solo entrada).</summary>
        public static string BlockPort(int port, bool tcp, string title)
        {
            string name = Name("port", (tcp ? "TCP " : "UDP ") + port) + " (in)";
            return Add(name, In, title, null, null, tcp ? Tcp : Udp, port.ToString());
        }

        private static string Both(string baseName, string title, string app, string remote, int protocol, string ports)
        {
            string a = Add(baseName + " (in)", In, title, app, remote, protocol, ports);
            if (a != null) return a;
            string b = Add(baseName + " (out)", Out, title, app, remote, protocol, ports);
            if (b != null)
            {
                try { RemoveRules(new[] { baseName + " (in)" }); } catch { /* queda solo la de entrada, se ve en la lista */ }
            }
            return b;
        }

        private static string Name(string kind, string target) => "TURBiX - " + kind + " - " + target;

        /// <summary>Distingue dos programas con el mismo nombre de archivo en carpetas distintas.</summary>
        private static string Short(string path)
        {
            uint h = 2166136261;
            foreach (char c in path.ToLowerInvariant()) h = (h ^ c) * 16777619;
            return h.ToString("x8").Substring(0, 6);
        }

        private static string Add(string name, int direction, string description, string app, string remote, int protocol, string localPorts)
        {
            try
            {
                object policy = Create("HNetCfg.FwPolicy2");
                object rules = Get(policy, "Rules");
                if (Exists(rules, name)) return null;
                object rule = Create("HNetCfg.FWRule");
                Set(rule, "Name", name);
                Set(rule, "Description", description ?? name);
                Set(rule, "Grouping", Group);
                Set(rule, "Direction", direction);
                Set(rule, "Action", 0);   // bloquear
                Set(rule, "Enabled", true);
                Set(rule, "Profiles", AllProfiles);
                if (app != null) Set(rule, "ApplicationName", app);
                if (remote != null) Set(rule, "RemoteAddresses", remote);
                if (protocol != AnyProtocol) Set(rule, "Protocol", protocol);
                if (localPorts != null) Set(rule, "LocalPorts", localPorts);
                Call(rules, "Add", rule);
                return null;
            }
            catch (Exception ex)
            {
                return Describe(ex);
            }
        }

        private static bool Exists(object rules, string name)
        {
            try
            {
                return Call(rules, "Item", name) != null;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ borrar

        /// <summary>Quita un bloqueo (todas sus reglas). Devuelve el error, o null.</summary>
        public static string Remove(BlockEntry entry)
        {
            try
            {
                RemoveRules(entry.RuleNames);
                return null;
            }
            catch (Exception ex)
            {
                return Describe(ex);
            }
        }

        private static void RemoveRules(IEnumerable<string> names)
        {
            object policy = Create("HNetCfg.FwPolicy2");
            object rules = Get(policy, "Rules");
            foreach (string name in names.ToList())
            {
                // Se comprueba el grupo antes de borrar: nunca una regla que no sea de TURBiX.
                object rule = Call(rules, "Item", name);
                if (!string.Equals(Get(rule, "Grouping") as string, Group, StringComparison.Ordinal)) continue;
                Call(rules, "Remove", name);
            }
        }
    }
}
