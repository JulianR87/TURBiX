using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    [DataContract]
    public sealed class BackupEntry
    {
        [DataMember(Order = 0)] public string Type { get; set; }          // Registry | Service | Task

        // Registro
        [DataMember(Order = 1, EmitDefaultValue = false)] public string Hive { get; set; }
        [DataMember(Order = 2, EmitDefaultValue = false)] public string SubKey { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string Name { get; set; }
        [DataMember(Order = 4)] public bool Existed { get; set; }
        [DataMember(Order = 5)] public bool KeyCreated { get; set; }
        [DataMember(Order = 6, EmitDefaultValue = false)] public string OldKind { get; set; }
        [DataMember(Order = 7, EmitDefaultValue = false)] public string OldString { get; set; }
        [DataMember(Order = 8, EmitDefaultValue = false)] public string[] OldStrings { get; set; }
        [DataMember(Order = 9)] public long OldNumber { get; set; }

        // Servicio
        [DataMember(Order = 10)] public int OldStart { get; set; }
        [DataMember(Order = 11)] public bool OldDelayed { get; set; }

        // Tarea programada
        [DataMember(Order = 12, EmitDefaultValue = false)] public string TaskFolder { get; set; }

        public string Describe()
        {
            switch (Type)
            {
                case "Registry": return $"{Hive}\\{SubKey}\\{Name}";
                case "Service": return L.F("backup.describe.service", Name);
                case "PowerPlan": return L.T("backup.describe.powerPlan");
                case "RegFile": return L.F("backup.describe.regKey", Name);
                case "Task": return L.F("backup.describe.task", $"{TaskFolder}\\{Name}");
                default: return Type;
            }
        }
    }

    [DataContract]
    public sealed class BackupSession
    {
        [DataMember(Order = 0)] public string Tool { get; set; } = "Turbix";
        [DataMember(Order = 1)] public string Version { get; set; }
        [DataMember(Order = 2)] public string Created { get; set; }
        [DataMember(Order = 3)] public string Label { get; set; }
        [DataMember(Order = 4)] public string Computer { get; set; }
        [DataMember(Order = 5)] public string User { get; set; }
        [DataMember(Order = 6)] public string Reverted { get; set; }
        [DataMember(Order = 7)] public List<BackupEntry> Entries { get; set; } = new List<BackupEntry>();

        // El nombre se guarda también como clave de texto, para mostrarlo en el idioma de quien lo mire
        // (Label queda como texto legible en el JSON y para copias de versiones anteriores).
        [DataMember(Order = 8, EmitDefaultValue = false)] public string LabelKey { get; set; }
        [DataMember(Order = 9, EmitDefaultValue = false)] public int LabelCount { get; set; }
        [DataMember(Order = 10, EmitDefaultValue = false)] public string LabelArgKey { get; set; }

        public string FilePath { get; set; }

        /// <summary>Nombre de la sesión en el idioma actual.</summary>
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(LabelKey))
                    return LabelArgKey != null ? L.P(LabelKey, LabelCount, L.T(LabelArgKey)) : L.P(LabelKey, LabelCount);
                return LegacyLabel(Label);
            }
        }

        /// <summary>Copias de versiones anteriores a 1.1.1, guardadas con el nombre en español («Privacidad: 23 ajustes»).</summary>
        private static string LegacyLabel(string label)
        {
            var m = System.Text.RegularExpressions.Regex.Match(label ?? string.Empty, @"^(.+?): (\d+) (ajustes?|entradas?)$");
            if (!m.Success || !int.TryParse(m.Groups[2].Value, out int n)) return label;
            switch (m.Groups[1].Value)
            {
                case "Registro": return L.P("regfix.session", n);
                case "Privacidad": return L.P("tweak.session.label", n, L.T("tweak.area.privacy.label"));
                case "Rendimiento": return L.P("tweak.session.label", n, L.T("tweak.area.performance.label"));
                case "Windows":
                case "Ajustes de Windows": return L.P("tweak.session.label", n, L.T("tweak.area.windows.label"));
                default: return label;
            }
        }

        public DateTime CreatedDate =>
            DateTime.TryParse(Created, out var d) ? d : File.GetLastWriteTime(FilePath ?? string.Empty);
    }

    /// <summary>Copias de seguridad en JSON (%ProgramData%\Turbix\Backups) y su reversión.</summary>
    public static class BackupStore
    {
        /// <summary>Sesión cuyo nombre es un texto con plural: L.P(key, count[, L.T(argKey)]).</summary>
        public static BackupSession NewSession(string labelKey, int count, string labelArgKey = null)
        {
            var s = NewSession(labelArgKey != null ? L.P(labelKey, count, L.T(labelArgKey)) : L.P(labelKey, count));
            s.LabelKey = labelKey;
            s.LabelCount = count;
            s.LabelArgKey = labelArgKey;
            return s;
        }

        public static BackupSession NewSession(string label)
        {
            return new BackupSession
            {
                Version = AppInfo.Version,
                Created = DateTime.Now.ToString("s"),
                Label = label,
                Computer = Environment.MachineName,
                User = Environment.UserDomainName + "\\" + Environment.UserName,
                FilePath = UniquePath()
            };
        }

        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Nombre de archivo que no usa ninguna otra sesión. El archivo se crea recién al primer cambio, así que
        /// dos sesiones abiertas en el mismo instante (por ejemplo, la optimización rápida) no deben recibir el
        /// mismo nombre: la segunda pisaría la copia de la primera.
        /// </summary>
        private static string UniquePath()
        {
            lock (Reserved)
            {
                string stem = Path.Combine(AppPaths.Backups, $"backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}");
                string path = stem + ".json";
                for (int i = 2; Reserved.Contains(path) || File.Exists(path); i++) path = $"{stem}-{i}.json";
                Reserved.Add(path);
                return path;
            }
        }

        public static void Save(BackupSession session)
        {
            if (session.Entries.Count == 0) return;
            Directory.CreateDirectory(AppPaths.Backups);
            var ser = new DataContractJsonSerializer(typeof(BackupSession));

            // Se escribe en un archivo temporal y se sustituye de golpe: un corte de luz a mitad
            // nunca deja la copia truncada.
            string tmp = session.FilePath + ".tmp";
            using (var fs = File.Create(tmp))
            using (var w = JsonReaderWriterFactory.CreateJsonWriter(fs, Encoding.UTF8, true, true, "  "))
            {
                ser.WriteObject(w, session);
                w.Flush();
            }
            if (File.Exists(session.FilePath)) File.Replace(tmp, session.FilePath, null);
            else File.Move(tmp, session.FilePath);
        }

        /// <summary>
        /// Solo se aceptan copias creadas por un administrador, por el sistema o por la cuenta actual.
        /// Evita que otro usuario del equipo deje un archivo preparado para que Turbix lo «revierta».
        /// </summary>
        private static bool IsTrusted(string path)
        {
            try
            {
                var owner = File.GetAccessControl(path).GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                if (owner == null) return false;
                if (owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                    owner.IsWellKnown(WellKnownSidType.LocalSystemSid)) return true;
                using (var me = WindowsIdentity.GetCurrent()) return owner.Equals(me.User);
            }
            catch
            {
                return false;
            }
        }

        public static BackupSession Load(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(BackupSession));
            using (var fs = File.OpenRead(path))
            {
                var s = (BackupSession)ser.ReadObject(fs);
                s.FilePath = path;
                if (s.Entries == null) s.Entries = new List<BackupEntry>();
                return s;
            }
        }

        public static List<BackupSession> List()
        {
            var result = new List<BackupSession>();
            if (!Directory.Exists(AppPaths.Backups)) return result;
            foreach (var file in Directory.GetFiles(AppPaths.Backups, "backup-*.json").OrderByDescending(f => f))
            {
                if (!IsTrusted(file))
                {
                    Logger.Warn(L.F("backup.log.untrusted", Path.GetFileName(file)));
                    continue;
                }
                try { result.Add(Load(file)); }
                catch { Logger.Warn(L.F("backup.log.damaged", Path.GetFileName(file))); }
            }
            return result;
        }

        // ------------------------------------------------------------------ registro

        public static void RecordRegistry(BackupSession s, RegistryHive hive, string subKey, string name,
            object oldValue, RegistryValueKind oldKind, bool keyExisted)
        {
            var e = new BackupEntry
            {
                Type = "Registry",
                Hive = RegistryHelper.HiveName(hive),
                SubKey = subKey,
                Name = name,
                Existed = oldValue != null,
                KeyCreated = !keyExisted
            };
            if (oldValue != null)
            {
                e.OldKind = oldKind.ToString();
                switch (oldKind)
                {
                    case RegistryValueKind.DWord:
                    case RegistryValueKind.QWord:
                        e.OldNumber = Convert.ToInt64(oldValue);
                        break;
                    case RegistryValueKind.MultiString:
                        e.OldStrings = (string[])oldValue;
                        break;
                    case RegistryValueKind.Binary:
                        e.OldString = Convert.ToBase64String((byte[])oldValue);
                        break;
                    default:
                        e.OldString = Convert.ToString(oldValue);
                        break;
                }
            }
            s.Entries.Add(e);
        }

        // ------------------------------------------------------------------ reversión

        public static int[] Revert(BackupSession session, IProgress<ProgressInfo> progress)
        {
            var entries = session.Entries.AsEnumerable().Reverse().ToList();
            Logger.Step(L.P("backup.log.reverting", entries.Count, session.DisplayLabel));
            int ok = 0, fail = 0, i = 0;
            foreach (var e in entries)
            {
                i++;
                progress?.Report(new ProgressInfo(100.0 * i / Math.Max(1, entries.Count), L.F("backup.progress.restoring", e.Describe())));
                try
                {
                    switch (e.Type)
                    {
                        case "Registry": RevertRegistry(e); break;
                        case "Service":
                            ServiceOps.SetStartType(e.Name, e.OldStart, e.OldDelayed);
                            if (e.OldStart == ServiceOps.Automatic) ServiceOps.Start(e.Name);
                            break;
                        case "Task": TaskOps.SetEnabled(e.TaskFolder, e.Name, true); break;
                        case "PowerPlan": PowerOps.SetActive(e.OldString); break;
                        case "RegFile": RegistryRepair.ImportFile(e.OldString); break;
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Logger.Warn(L.F("backup.log.restoreFailed", e.Describe(), ex.Message));
                }
            }
            // Solo se da por revertida si no falló nada; si no, queda disponible para reintentar.
            if (fail == 0)
            {
                session.Reverted = DateTime.Now.ToString("s");
                Save(session);
                Logger.Success(L.P("backup.log.revertDone", ok));
            }
            else
            {
                Logger.Warn(L.P("backup.log.revertPartial", ok, fail));
            }
            return new[] { ok, fail };
        }

        private static void RevertRegistry(BackupEntry e)
        {
            RegistryHelper.Parse(e.Hive + "\\" + e.SubKey, out var hive, out var subKey);
            if (e.Existed)
            {
                var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), e.OldKind);
                object value;
                switch (kind)
                {
                    case RegistryValueKind.DWord: value = unchecked((int)e.OldNumber); break;
                    case RegistryValueKind.QWord: value = e.OldNumber; break;
                    case RegistryValueKind.MultiString: value = e.OldStrings ?? new string[0]; break;
                    case RegistryValueKind.Binary: value = Convert.FromBase64String(e.OldString ?? string.Empty); break;
                    default: value = e.OldString ?? string.Empty; break;
                }
                RegistryHelper.SetValue(hive, subKey, e.Name, value, kind);
                return;
            }
            RegistryHelper.DeleteValue(hive, subKey, e.Name);
            if (e.KeyCreated) RegistryHelper.DeleteKeyIfEmpty(hive, subKey);
        }
    }

    public sealed class ProgressInfo
    {
        public ProgressInfo(double percent, string status)
        {
            Percent = percent;
            Status = status;
        }

        public double Percent { get; }
        public string Status { get; }
    }
}
