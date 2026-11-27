using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Turbix.Core
{
    /// <summary>Comodines estilo Windows (* y ?) sin las rarezas de las extensiones 8.3.</summary>
    public static class Glob
    {
        private static readonly ConcurrentDictionary<string, Regex> Cache = new ConcurrentDictionary<string, Regex>();

        public static bool HasWildcard(string s) => s.IndexOf('*') >= 0 || s.IndexOf('?') >= 0;

        public static bool IsMatch(string name, string pattern)
        {
            if (!HasWildcard(pattern)) return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
            var rx = Cache.GetOrAdd(pattern, p => new Regex(
                "^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            return rx.IsMatch(name);
        }
    }

    /// <summary>
    /// Expande rutas con variables de entorno y marcadores:
    ///   {USER}  -> cada perfil de usuario real del equipo
    ///   {DRIVE} -> cada disco fijo (C:, D:, ...)
    /// </summary>
    public static class PathExpander
    {
        private static List<string> _profiles;

        public static IReadOnlyList<string> UserProfiles
        {
            get
            {
                if (_profiles != null) return _profiles;
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using (var b = RegistryHelper.OpenBase(RegistryHive.LocalMachine))
                    using (var list = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"))
                    {
                        if (list != null)
                        {
                            foreach (string sid in list.GetSubKeyNames())
                            {
                                if (!sid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase)) continue;
                                using (var k = list.OpenSubKey(sid))
                                {
                                    var p = k?.GetValue("ProfileImagePath") as string;
                                    if (string.IsNullOrEmpty(p)) continue;
                                    p = Environment.ExpandEnvironmentVariables(p);
                                    if (Directory.Exists(p)) set.Add(p);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Se usará al menos el perfil actual.
                }
                string current = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(current)) set.Add(current);
                _profiles = set.ToList();
                return _profiles;
            }
        }

        public static IEnumerable<string> FixedDrives =>
            DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && SafeIsReady(d))
                .Select(d => d.Name.TrimEnd('\\'));

        private static bool SafeIsReady(DriveInfo d)
        {
            try { return d.IsReady; } catch { return false; }
        }

        public static IEnumerable<string> Expand(string pattern)
        {
            IEnumerable<string> results = new[] { Environment.ExpandEnvironmentVariables(pattern) };
            if (pattern.Contains("{USER}"))
                results = results.SelectMany(r => UserProfiles.Select(u => r.Replace("{USER}", u))).ToList();
            if (pattern.Contains("{DRIVE}"))
                results = results.SelectMany(r => FixedDrives.Select(d => r.Replace("{DRIVE}", d))).ToList();
            return results;
        }
    }

    public sealed class CleanStats
    {
        public long Bytes;
        public int Files;
        public int Failed;
        public int Skipped;

        public void Add(CleanStats other)
        {
            Bytes += other.Bytes;
            Files += other.Files;
            Failed += other.Failed;
            Skipped += other.Skipped;
        }
    }

    /// <summary>
    /// Borrado seguro de archivos: nunca sigue enlaces simbólicos ni uniones (junctions),
    /// omite archivos en uso y permite medir sin borrar.
    /// </summary>
    public static class FileCleaner
    {
        public static CleanStats Run(IEnumerable<string> patterns, bool measure, int minAgeHours = 0, string[] exclude = null)
        {
            var stats = new CleanStats();
            DateTime cutoff = minAgeHours > 0 ? DateTime.Now.AddHours(-minAgeHours) : DateTime.MaxValue;
            foreach (string raw in patterns)
                foreach (string pattern in PathExpander.Expand(raw))
                    foreach (FileSystemInfo root in Resolve(pattern))
                        Walk(root, measure, cutoff, exclude, stats);
            return stats;
        }

        /// <summary>Resuelve una ruta con comodines en cualquier segmento.</summary>
        public static IEnumerable<FileSystemInfo> Resolve(string pattern)
        {
            var parts = pattern.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].EndsWith(":")) yield break;

            var current = new List<DirectoryInfo> { new DirectoryInfo(parts[0] + "\\") };
            for (int i = 1; i < parts.Length; i++)
            {
                string seg = parts[i];
                bool last = i == parts.Length - 1;
                var next = new List<DirectoryInfo>();
                foreach (var dir in current)
                {
                    if (Glob.HasWildcard(seg))
                    {
                        IEnumerable<FileSystemInfo> children;
                        try { children = dir.EnumerateFileSystemInfos().ToList(); }
                        catch { continue; }
                        foreach (var c in children)
                        {
                            if (!Glob.IsMatch(c.Name, seg)) continue;
                            bool isDir = (c.Attributes & FileAttributes.Directory) != 0;
                            bool isLink = (c.Attributes & FileAttributes.ReparsePoint) != 0;
                            if (last)
                            {
                                if (!isLink) yield return c;
                            }
                            else if (isDir && !isLink)
                            {
                                next.Add((DirectoryInfo)c);
                            }
                        }
                    }
                    else
                    {
                        string path = Path.Combine(dir.FullName, seg);
                        if (last)
                        {
                            if (File.Exists(path)) yield return new FileInfo(path);
                            else if (Directory.Exists(path)) yield return new DirectoryInfo(path);
                        }
                        else if (Directory.Exists(path) && !IsLink(path))
                        {
                            // Nunca se atraviesa una unión o enlace: su destino puede ser cualquier carpeta.
                            next.Add(new DirectoryInfo(path));
                        }
                    }
                }
                if (last) yield break;
                current = next;
                if (current.Count == 0) yield break;
            }
        }

        private static bool IsLink(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }   // si no se puede comprobar, no se entra
        }

        private static void Walk(FileSystemInfo root, bool measure, DateTime cutoff, string[] exclude, CleanStats st)
        {
            var dirs = new List<DirectoryInfo>();
            var stack = new Stack<FileSystemInfo>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                FileSystemInfo item = stack.Pop();
                FileAttributes attr;
                try { attr = item.Attributes; }
                catch { st.Failed++; continue; }

                if ((attr & FileAttributes.ReparsePoint) != 0) continue;   // nunca seguir enlaces

                if ((attr & FileAttributes.Directory) != 0)
                {
                    var d = (DirectoryInfo)item;
                    // Una exclusión también puede ser el nombre de una carpeta entera.
                    if (exclude != null && exclude.Any(x => Glob.IsMatch(d.Name, x))) continue;
                    dirs.Add(d);
                    try
                    {
                        foreach (var child in d.EnumerateFileSystemInfos()) stack.Push(child);
                    }
                    catch
                    {
                        st.Failed++;
                    }
                    continue;
                }

                var f = (FileInfo)item;
                if (exclude != null && exclude.Any(x => Glob.IsMatch(f.Name, x))) continue;

                long len;
                DateTime written;
                try
                {
                    len = f.Length;
                    // Un archivo recién extraído conserva su fecha de modificación antigua: cuenta también la de creación.
                    written = f.LastWriteTime > f.CreationTime ? f.LastWriteTime : f.CreationTime;
                }
                catch
                {
                    continue;
                }

                if (written > cutoff) { st.Skipped++; continue; }

                if (measure)
                {
                    st.Bytes += len;
                    st.Files++;
                    continue;
                }

                try
                {
                    if ((attr & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                    f.Delete();
                    st.Bytes += len;
                    st.Files++;
                }
                catch
                {
                    st.Failed++;   // en uso o protegido
                }
            }

            if (measure) return;
            // Los hijos se descubren después que sus padres: recorrer al revés borra de dentro hacia fuera.
            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                try { dirs[i].Delete(false); } catch { /* no vacía */ }
            }
        }

        public static string FormatSize(long bytes)
        {
            // Unidades del idioma (en francés «Mo», en ruso «МБ») y espacio duro para que no se separen del número.
            const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
            var c = Localization.L.Culture;
            const string nbsp = " ";
            if (bytes >= GB) return (bytes / GB).ToString("0.00", c) + nbsp + Localization.L.T("unit.gb");
            if (bytes >= MB) return (bytes / MB).ToString("0.0", c) + nbsp + Localization.L.T("unit.mb");
            if (bytes >= KB) return (bytes / KB).ToString("0", c) + nbsp + Localization.L.T("unit.kb");
            return bytes.ToString(c) + nbsp + Localization.L.T("unit.b");
        }
    }
}
