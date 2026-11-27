using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Tweaks
{
    public sealed class ApplyResult
    {
        public int Changed { get; set; }
        public int Errors { get; set; }
        public int Applied { get; set; }
        public string BackupPath { get; set; }
        public bool RestorePointCreated { get; set; }
    }

    public static class TweakEngine
    {
        // ------------------------------------------------------------ estado

        public static Dictionary<string, TweakState> GetStates(IEnumerable<Tweak> tweaks)
        {
            var cache = new TaskCache();
            var result = new Dictionary<string, TweakState>();
            foreach (var t in tweaks)
            {
                try { result[t.Id] = GetState(t, cache); }
                catch { result[t.Id] = TweakState.Unknown; }
            }
            return result;
        }

        public static TweakState GetState(Tweak tweak, TaskCache cache)
        {
            if (tweak.Condition != null && !tweak.Condition()) return TweakState.NotApplicable;
            int total = 0, done = 0;
            foreach (var a in tweak.Actions)
            {
                switch (a.Type)
                {
                    case TweakActionType.Registry:
                        total++;
                        var cur = RegistryHelper.GetValue(a.Hive, a.SubKey, a.ValueName, out var curKind);
                        // Debe coincidir también el tipo: un texto «0» no es lo mismo que el número 0 para Windows.
                        if (curKind == a.Kind && RegistryHelper.ValueEquals(cur, a.Value)) done++;
                        else if (cur == null && a.AbsentIsDefault) done++;
                        break;
                    case TweakActionType.Service:
                        var info = ServiceOps.GetStartInfo(a.ServiceName);
                        if (info != null)
                        {
                            total++;
                            if (info.Start == ServiceOps.Disabled) done++;
                        }
                        break;
                    case TweakActionType.Task:
                        foreach (var task in cache.Find(a.TaskFolder, a.TaskName))
                        {
                            total++;
                            if (!task.Enabled) done++;
                        }
                        break;
                    case TweakActionType.PowerPlan:
                        if (PowerOps.Exists((string)a.Value))
                        {
                            total++;
                            if (PowerOps.IsActive((string)a.Value)) done++;
                        }
                        break;
                }
            }
            if (total == 0) return TweakState.NotApplicable;
            if (done == total) return TweakState.Applied;
            return done == 0 ? TweakState.NotApplied : TweakState.Partial;
        }

        // ------------------------------------------------------------ aplicar

        /// <param name="area">
        /// Grupo de ajustes (identificador, no se muestra tal cual): «privacidad», «rendimiento» o «Windows».
        /// Se traduce con <see cref="AreaKey"/> para los textos del registro, el punto de restauración y la copia.
        /// </param>
        public static ApplyResult Apply(IList<Tweak> tweaks, bool createRestorePoint, IProgress<ProgressInfo> progress,
            string area = "privacidad")
        {
            var result = new ApplyResult();
            if (tweaks.Count == 0) return result;

            string areaKey = AreaKey(area);
            AreaTexts(areaKey, area, out string areaPhrase, out string label);

            var os = SystemInfo.GetOs();
            Logger.Step(L.P("tweak.log.applying", tweaks.Count, areaPhrase, os.Caption, os.DisplayVersion));
            if (areaKey == "privacy" && !os.SupportsTelemetryOff)
                Logger.Info(L.F("tweak.log.editionNote", os.EditionName));

            if (createRestorePoint)
            {
                progress?.Report(new ProgressInfo(2, L.T("tweak.progress.restorePoint")));
                result.RestorePointCreated = RestorePoint.Create(L.F("tweak.restorePoint.name", areaPhrase));
            }

            var session = (areaKey != null
                ? BackupStore.NewSession("tweak.session.label", tweaks.Count, "tweak.area." + areaKey + ".label")
                : BackupStore.NewSession(L.P("tweak.session.label", tweaks.Count, label)));
            int i = 0;
            foreach (var t in tweaks)
            {
                i++;
                progress?.Report(new ProgressInfo(5 + 93.0 * i / tweaks.Count, t.Name));
                if (t.Condition != null && !t.Condition())
                {
                    Logger.Info(L.F("tweak.log.notApplicable", t.Name));
                    continue;
                }

                int changed = 0, errors = 0;
                foreach (var a in t.Actions)
                {
                    try
                    {
                        if (ApplyAction(a, session)) changed++;
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Logger.Warn($"   {a.Describe()}: {ex.Message}");
                    }
                }
                BackupStore.Save(session);
                result.Changed += changed;
                result.Errors += errors;
                result.Applied++;

                if (errors > 0) Logger.Warn(L.F("tweak.log.withErrors", t.Name, Changes(changed), errors));
                else if (changed == 0) Logger.Success(L.F("tweak.log.alreadyApplied", t.Name));
                else Logger.Success(L.F("tweak.log.changed", t.Name, Changes(changed)));
            }

            if (session.Entries.Count > 0)
            {
                result.BackupPath = session.FilePath;
                Logger.Info(L.F("tweak.log.backupSaved", session.FilePath));
            }
            string errorsText = L.P("tweak.errors", result.Errors);
            Logger.Step(result.Changed > 0
                ? L.F("tweak.log.doneRestart", Changes(result.Changed), errorsText)
                : L.F("tweak.log.done", Changes(result.Changed), errorsText));
            progress?.Report(new ProgressInfo(100, L.T("tweak.progress.done")));
            return result;
        }

        private static string Changes(int n) => L.P("tweak.changes", n);

        /// <summary>Clave de texto del grupo de ajustes («privacy», «performance», «windows»), o null si no se conoce.</summary>
        private static string AreaKey(string area)
        {
            switch ((area ?? "").Trim().ToLowerInvariant())
            {
                case "privacidad":
                case "privacy":
                    return "privacy";
                case "rendimiento":
                case "performance":
                case "gaming":
                    return "performance";
                case "windows":
                case "ajustes de windows":
                    return "windows";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Nombre del grupo para meter en una frase («de privacidad», «Datenschutz»…) y como título de la copia
        /// («Privacidad»). Si el grupo no se conoce, se usa el texto recibido.
        /// </summary>
        private static void AreaTexts(string areaKey, string area, out string phrase, out string label)
        {
            switch (areaKey)
            {
                case "privacy":
                    phrase = L.T("tweak.area.privacy.phrase");
                    label = L.T("tweak.area.privacy.label");
                    break;
                case "performance":
                    phrase = L.T("tweak.area.performance.phrase");
                    label = L.T("tweak.area.performance.label");
                    break;
                case "windows":
                    phrase = L.T("tweak.area.windows.phrase");
                    label = L.T("tweak.area.windows.label");
                    break;
                default:
                    phrase = area ?? "";
                    label = string.IsNullOrEmpty(area) ? "" : char.ToUpper(area[0]) + area.Substring(1);
                    break;
            }
        }

        /// <summary>Aplica una acción guardando antes el valor original. Devuelve true si cambió algo.</summary>
        private static bool ApplyAction(TweakAction a, BackupSession session)
        {
            switch (a.Type)
            {
                case TweakActionType.Registry:
                {
                    bool keyExisted = RegistryHelper.KeyExists(a.Hive, a.SubKey);
                    object old = RegistryHelper.GetValue(a.Hive, a.SubKey, a.ValueName, out var oldKind);
                    if (old != null && oldKind == a.Kind && RegistryHelper.ValueEquals(old, a.Value)) return false;
                    if (old == null && a.AbsentIsDefault) return false;   // Windows ya usa ese valor de fábrica
                    // Primero se guarda el valor original en disco y después se cambia: si el equipo se apaga
                    // a mitad, la copia ya existe.
                    BackupStore.RecordRegistry(session, a.Hive, a.SubKey, a.ValueName, old, oldKind, keyExisted);
                    BackupStore.Save(session);
                    RegistryHelper.SetValue(a.Hive, a.SubKey, a.ValueName, a.Value, a.Kind);
                    return true;
                }
                case TweakActionType.Service:
                {
                    var info = ServiceOps.GetStartInfo(a.ServiceName);
                    if (info == null) return false;   // no existe en este equipo
                    if (info.Start == ServiceOps.Disabled)
                    {
                        ServiceOps.Stop(a.ServiceName);
                        return false;
                    }
                    session.Entries.Add(new BackupEntry
                    {
                        Type = "Service", Name = a.ServiceName, OldStart = info.Start, OldDelayed = info.Delayed
                    });
                    BackupStore.Save(session);
                    ServiceOps.SetStartType(a.ServiceName, ServiceOps.Disabled, false);
                    if (!ServiceOps.Stop(a.ServiceName))
                        Logger.Info("   " + L.F("tweak.log.serviceNotStopped", a.ServiceName));
                    return true;
                }
                case TweakActionType.Task:
                {
                    bool any = false;
                    foreach (var task in TaskOps.ListFolder(a.TaskFolder).Where(x => Glob.IsMatch(x.Name, a.TaskName)))
                    {
                        if (!task.Enabled) continue;
                        session.Entries.Add(new BackupEntry { Type = "Task", TaskFolder = task.Folder, Name = task.Name });
                        BackupStore.Save(session);
                        TaskOps.SetEnabled(task.Folder, task.Name, false);
                        any = true;
                    }
                    return any;
                }
                case TweakActionType.PowerPlan:
                {
                    string target = (string)a.Value;
                    if (!PowerOps.Exists(target))
                    {
                        Logger.Info("   " + L.T("tweak.log.noPowerPlan"));
                        return false;
                    }
                    string old = PowerOps.GetActive();
                    if (old == null || string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) return false;
                    session.Entries.Add(new BackupEntry { Type = "PowerPlan", Name = L.T("tweak.action.powerPlan"), OldString = old });
                    BackupStore.Save(session);
                    PowerOps.SetActive(target);
                    return true;
                }
            }
            return false;
        }
    }
}
