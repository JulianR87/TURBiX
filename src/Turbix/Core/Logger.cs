using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Turbix.Localization;

namespace Turbix.Core
{
    public static class AppPaths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TURBiX");
        public static readonly string Logs = Path.Combine(Root, "Logs");
        public static readonly string Backups = Path.Combine(Root, "Backups");

        /// <summary>
        /// Crea las carpetas de Turbix de modo que solo los administradores puedan escribir en ellas
        /// (por defecto, cualquier usuario puede crear archivos dentro de ProgramData).
        /// </summary>
        public static void Secure()
        {
            try
            {
                Directory.CreateDirectory(Root);
                var acl = new System.Security.AccessControl.DirectorySecurity();
                acl.SetAccessRuleProtection(true, false);   // sin permisos heredados
                var inherit = System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                              System.Security.AccessControl.InheritanceFlags.ObjectInherit;
                var none = System.Security.AccessControl.PropagationFlags.None;
                var allow = System.Security.AccessControl.AccessControlType.Allow;
                Func<System.Security.Principal.WellKnownSidType, System.Security.Principal.SecurityIdentifier> sid =
                    t => new System.Security.Principal.SecurityIdentifier(t, null);

                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    sid(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid),
                    System.Security.AccessControl.FileSystemRights.FullControl, inherit, none, allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    sid(System.Security.Principal.WellKnownSidType.LocalSystemSid),
                    System.Security.AccessControl.FileSystemRights.FullControl, inherit, none, allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    sid(System.Security.Principal.WellKnownSidType.BuiltinUsersSid),
                    System.Security.AccessControl.FileSystemRights.ReadAndExecute, inherit, none, allow));
                Directory.SetAccessControl(Root, acl);
                Directory.CreateDirectory(Logs);
                Directory.CreateDirectory(Backups);
            }
            catch
            {
                // Sin permisos de administrador no se pueden cambiar: se usan las carpetas tal cual.
            }
        }
    }

    public enum LogLevel { Info, Success, Warning, Error, Step }

    public sealed class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        /// <summary>Separador de día en la vista (no es una línea del registro).</summary>
        public bool IsDayHeader { get; set; }

        public string TimeText => Time.ToString("HH:mm:ss");

        public string DayText
        {
            get
            {
                var today = DateTime.Today;
                if (Time.Date == today) return L.T("log.today");
                if (Time.Date == today.AddDays(-1)) return L.T("log.yesterday");
                string text = Time.ToString("D", L.Culture);
                return text.Length > 0 ? char.ToUpper(text[0], L.Culture) + text.Substring(1) : text;
            }
        }

        public string LevelKey => Level.ToString().ToLowerInvariant();

        public string LevelText => L.T("log.level." + LevelKey);

        /// <summary>Código fijo que se escribe en el archivo, igual en todos los idiomas.</summary>
        public string FileLevel
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Success: return "OK";
                    case LogLevel.Warning: return "WARN";
                    case LogLevel.Error: return "ERROR";
                    case LogLevel.Step: return "STEP";
                    default: return "INFO";
                }
            }
        }

        public static LogEntry DayHeader(DateTime day) => new LogEntry { Time = day.Date, IsDayHeader = true, Message = string.Empty };
    }

    /// <summary>Registro de actividad en memoria y en %ProgramData%\Turbix\Logs.</summary>
    public static class Logger
    {
        private static readonly object FileLock = new object();
        private static readonly List<LogEntry> Session = new List<LogEntry>();
        private const int SessionMax = 4000;
        private static long _todayCutoff = -1;

        public static string LogFile { get; private set; }
        public static bool FileEnabled { get; set; } = true;

        public static event Action<LogEntry> Logged;

        public static void Initialize()
        {
            if (!FileEnabled) return;
            try
            {
                Directory.CreateDirectory(AppPaths.Logs);
                LogFile = Path.Combine(AppPaths.Logs, $"turbix-{DateTime.Now:yyyyMMdd}.log");
                // Lo que ya tenía el archivo de hoy es historial; lo que se escriba desde ahora es de esta sesión.
                _todayCutoff = File.Exists(LogFile) ? new FileInfo(LogFile).Length : 0;
            }
            catch
            {
                LogFile = null;
            }
        }

        public static void Info(string message) => Write(LogLevel.Info, message);
        public static void Success(string message) => Write(LogLevel.Success, message);
        public static void Warn(string message) => Write(LogLevel.Warning, message);
        public static void Error(string message) => Write(LogLevel.Error, message);
        public static void Step(string message) => Write(LogLevel.Step, message);

        public static void Write(LogLevel level, string message)
        {
            var entry = new LogEntry { Time = DateTime.Now, Level = level, Message = message ?? string.Empty };
            lock (FileLock)
            {
                Session.Add(entry);
                if (Session.Count > SessionMax) Session.RemoveRange(0, Session.Count - SessionMax);
                if (FileEnabled && LogFile != null)
                {
                    try
                    {
                        string today = Path.Combine(AppPaths.Logs, $"turbix-{entry.Time:yyyyMMdd}.log");
                        if (!string.Equals(today, LogFile, StringComparison.OrdinalIgnoreCase))
                        {
                            LogFile = today;
                            _todayCutoff = 0;   // archivo nuevo: todo lo que tenga es de esta sesión
                        }
                        File.AppendAllText(LogFile,
                            $"{entry.Time:yyyy-MM-dd HH:mm:ss} {entry.FileLevel,-5} {entry.Message}{Environment.NewLine}",
                            Encoding.UTF8);
                    }
                    catch
                    {
                        // El registro nunca debe interrumpir una operación.
                    }
                }
            }
            Logged?.Invoke(entry);
        }

        /// <summary>
        /// Historial de sesiones anteriores (de más antiguo a más reciente) más lo registrado en esta sesión hasta ahora.
        /// Se leen los archivos diarios desde el más reciente hasta juntar <paramref name="max"/> líneas.
        /// </summary>
        public static List<LogEntry> LoadHistory(int max)
        {
            var history = new List<LogEntry>();
            try
            {
                if (FileEnabled && Directory.Exists(AppPaths.Logs))
                {
                    var files = Directory.GetFiles(AppPaths.Logs, "turbix-*.log").OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase);
                    foreach (string file in files)
                    {
                        long limit = string.Equals(file, LogFile, StringComparison.OrdinalIgnoreCase) ? _todayCutoff : long.MaxValue;
                        try { history.InsertRange(0, ReadFile(file, limit)); }
                        catch { /* archivo en uso o sin permiso: se salta */ }
                        if (history.Count >= max) break;
                    }
                }
            }
            catch
            {
                // Sin acceso a la carpeta: se muestra solo esta sesión.
            }
            if (history.Count > max) history.RemoveRange(0, history.Count - max);
            lock (FileLock) history.AddRange(Session);
            return history;
        }

        private static List<LogEntry> ReadFile(string path, long limit)
        {
            var list = new List<LogEntry>();
            if (limit == 0) return list;
            string text;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long length = Math.Min(fs.Length, limit);
                var buffer = new byte[length];
                int read = 0;
                while (read < length)
                {
                    int n = fs.Read(buffer, read, (int)(length - read));
                    if (n <= 0) break;
                    read += n;
                }
                text = Encoding.UTF8.GetString(buffer, 0, read);
            }
            LogEntry last = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.Length >= 20 && DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    string rest = line.Substring(20);
                    int space = rest.IndexOf(' ');
                    string level = space > 0 ? rest.Substring(0, space) : rest;
                    string message = space > 0 ? rest.Substring(space).TrimStart() : string.Empty;
                    last = new LogEntry { Time = time, Level = ParseLevel(level), Message = message };
                    list.Add(last);
                }
                else if (last != null)
                {
                    last.Message += "\n" + line;   // continuación (p. ej., el detalle de un error)
                }
            }
            return list;
        }

        private static LogLevel ParseLevel(string token)
        {
            switch (token.ToUpperInvariant())
            {
                case "OK": return LogLevel.Success;
                case "WARN":
                case "AVISO": return LogLevel.Warning;   // registros de versiones anteriores
                case "ERROR": return LogLevel.Error;
                case "STEP":
                case "PASO": return LogLevel.Step;
                default: return LogLevel.Info;
            }
        }
    }
}
