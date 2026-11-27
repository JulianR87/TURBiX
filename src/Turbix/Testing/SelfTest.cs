#if SELFTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Turbix.Cleanup;
using Turbix.Core;
using Turbix.Tweaks;

namespace Turbix.Testing
{
    /// <summary>
    /// Autoprueba para máquinas virtuales de prueba. Solo existe en la compilación con
    /// -p:TurbixSelfTest=true: el exe que se publica no la incluye.
    ///
    /// Se ejecuta tres veces con un reinicio entre cada una (lo hace quien la lanza, con vmrun):
    ///   fase 1: foto del estado original, aplica todo y comprueba que quedó aplicado.
    ///   fase 2: comprueba que todo sigue igual tras reiniciar, revierte y compara con la foto.
    ///   fase 3: comprueba que la reversión también sobrevivió al reinicio.
    /// </summary>
    public static class SelfTest
    {
        private static string _dir;
        private static StreamWriter _report;
        private static int _fails, _warns;

        private static readonly KeyValuePair<string, IReadOnlyList<Tweak>>[] Catalogs =
        {
            new KeyValuePair<string, IReadOnlyList<Tweak>>("privacidad", TweakCatalog.All),
            new KeyValuePair<string, IReadOnlyList<Tweak>>("rendimiento", GamingCatalog.All),
            new KeyValuePair<string, IReadOnlyList<Tweak>>("Windows", WindowsCatalog.All),
        };

        public static int Run(string dir)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
            string phaseFile = Path.Combine(dir, "phase.txt");
            int phase = File.Exists(phaseFile) ? int.Parse(File.ReadAllText(phaseFile).Trim()) : 1;

            using (_report = new StreamWriter(Path.Combine(dir, "report.txt"), true, new UTF8Encoding(true)) { AutoFlush = true })
            {
                Line("");
                Line($"==================== FASE {phase} · Turbix {AppInfo.Version} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Line($"Sistema: {SystemInfo.GetOs().Caption} {SystemInfo.GetOs().DisplayVersion} · usuario {Environment.UserName}");
                try
                {
                    switch (phase)
                    {
                        case 1: Phase1(); break;
                        case 2: Phase2(); break;
                        case 3: Phase3(); break;
                        default: Line("Nada que hacer: la autoprueba ya terminó."); return 0;
                    }
                }
                catch (Exception ex)
                {
                    Fail("Excepción no controlada: " + ex);
                }
                Line($"---- Fin de la fase {phase}: {_fails} fallos, {_warns} avisos.");
                File.WriteAllText(phaseFile, (phase + 1).ToString());
            }
            return _fails == 0 ? 0 : 1;
        }

        // ================================================================== fases

        private static void Phase1()
        {
            var baseline = Snapshot();
            Save("baseline.txt", baseline);
            Line($"Foto del estado original: {baseline.Count} elementos.");

            // ---- ajustes
            var applied = new List<string>();
            bool firstCatalog = true;
            foreach (var cat in Catalogs)
            {
                string area = cat.Key; var tweaks = cat.Value;
                Section("Aplicar " + area);
                var list = tweaks.ToList();
                ApplyResult r = Guard(() => TweakEngine.Apply(list, firstCatalog, null, area));
                if (r == null) continue;
                if (firstCatalog) Line("Punto de restauración: " + (r.RestorePointCreated ? "creado" : "no se pudo (se esperaba si no hay Protección del sistema)"));
                firstCatalog = false;
                Line($"Cambios: {r.Changed} · errores: {r.Errors}");
                if (r.Errors > 0) Fail($"{r.Errors} acciones con error al aplicar (ver el registro de Turbix).");

                var cache = new TaskCache();
                foreach (var t in list)
                {
                    var st = TweakEngine.GetState(t, cache);
                    if (st == TweakState.NotApplicable) { Line($"  ·  {t.Name}: no aplica"); continue; }
                    if (st == TweakState.Applied) { Line($"  ok {t.Name}"); applied.Add(Key(area, t)); continue; }
                    Fail($"{t.Name}: quedó «{st}» después de aplicar");
                    foreach (var a in t.Actions) Line("       " + DescribeAction(a, cache));
                }
            }
            Save("applied.txt", applied.ToDictionary(x => x, x => "1"));

            // ---- inicio de Windows
            Section("Inicio de Windows: desactivar");
            var disabled = new Dictionary<string, string>();
            foreach (var e in Guard(StartupManager.List) ?? new List<StartupEntry>())
            {
                if (e.IsProtected || !e.Enabled) { Line($"  ·  {e.Name} ({e.SourceText}): {(e.IsProtected ? "protegido" : "ya desactivado")}"); continue; }
                // Sin el proceso de usuario de VMware Tools, vmrun no puede lanzar las fases siguientes.
                if ((e.Name + " " + e.Command).IndexOf("vmware", StringComparison.OrdinalIgnoreCase) >= 0) { Line($"  ·  {e.Name}: se deja activo (lo necesita la prueba)"); continue; }
                if (Guard(() => { StartupManager.SetEnabled(e, false); return true; }))
                    disabled[StartupKey(e)] = e.Name;
            }
            var after = Guard(StartupManager.List) ?? new List<StartupEntry>();
            foreach (var kv in disabled)
            {
                var e = after.FirstOrDefault(x => StartupKey(x) == kv.Key);
                if (e == null) Warn($"{kv.Value}: desapareció de la lista");
                else if (e.Enabled) Fail($"{kv.Value}: sigue activado después de desactivarlo");
                else Line($"  ok {kv.Value} desactivado");
            }
            Save("startup.txt", disabled);

            // ---- memoria
            Section("Memoria RAM");
            var mem = Guard(MemoryOps.Clean);
            if (mem != null)
            {
                if (mem.Ok) Line($"  ok liberados {FileCleaner.FormatSize(mem.FreedBytes)} (conjuntos de trabajo: {mem.WorkingSetsTrimmed}, lista en espera: {mem.StandbyPurged})");
                else Fail("No se pudo liberar memoria por ningún método.");
            }

            // ---- limpieza
            Section("Limpieza");
            var items = CleanupCatalog.All.ToList();
            var before = Guard(() => CleanupEngine.Measure(items, false, null));
            var cleaned = Guard(() => CleanupEngine.Clean(items, false, null));
            var again = Guard(() => CleanupEngine.Measure(items, false, null));
            if (before != null && cleaned != null && again != null)
            {
                foreach (var item in items)
                {
                    long? b = before.FirstOrDefault(x => x.Id == item.Id)?.Bytes;
                    var c = cleaned.FirstOrDefault(x => x.Id == item.Id);
                    long? a = again.FirstOrDefault(x => x.Id == item.Id)?.Bytes;
                    Line($"  {item.Name}: antes {Size(b)} · liberado {Size(c?.Bytes)} · después {Size(a)}" +
                         (c != null && c.Failed > 0 ? $" · {c.Failed} en uso" : "") +
                         (c != null && c.SkippedApps.Count > 0 ? " · omitido: " + string.Join(", ", c.SkippedApps) : ""));
                }
            }
            if (Process.GetProcessesByName("explorer").Length == 0)
                Fail("El Explorador de Windows no está abierto después de la limpieza.");
            else
                Line("  ok el Explorador sigue abierto");

            // ---- apps de Windows (no se revierte: se reinstalan desde la Tienda)
            Section("Apps de Windows");
            var apps = Guard(AppxOps.FindInstalled) ?? new List<InstalledApp>();
            Line($"  {apps.Count} apps de la lista instaladas: " + string.Join(", ", apps.Select(a => a.Info.Name + (a.Info.Recommended ? "*" : ""))));
            foreach (var app in apps.Where(a => a.Info.Recommended))
            {
                var sw = Stopwatch.StartNew();
                if (Guard(() => { AppxOps.Remove(app); return true; })) Line($"  ok {app.Info.Name} quitada ({sw.Elapsed.TotalSeconds:0.0} s)");
            }
            var left = Guard(AppxOps.FindInstalled) ?? new List<InstalledApp>();
            foreach (var app in left.Where(a => a.Info.Recommended)) Fail($"{app.Info.Name}: sigue instalada después de quitarla");

            Section("Programas instalados (lectura)");
            var programs = Guard(ProgramsOps.List) ?? new List<InstalledProgram>();
            Line($"  {programs.Count} programas: " + string.Join(", ", programs.Take(15).Select(p => p.Name)));
            foreach (var p in programs.Where(p => string.IsNullOrEmpty(p.UninstallCommand))) Fail($"{p.Name}: sin desinstalador");

            Line("  " + HiberFile());

            // ---- registro, salud y diagnóstico (solo lectura)
            Section("Registro de Windows (análisis)");
            var issues = Guard(() => RegistryRepair.Scan(null));
            if (issues != null)
            {
                Line($"  {issues.Count} entradas marcadas");
                foreach (var i in issues) Warn($"{i.Category}: {i.Name} · {i.Detail}");
            }

            Section("Salud");
            foreach (var c in Guard(HealthCheck.Run) ?? new List<HealthComponent>())
                Line($"  {c.Level,-10} {c.Name}: {c.Headline}" + string.Concat(c.Details.Select(d => $" | {d.Label}: {d.Value}")));

            Section("Diagnóstico");
            foreach (var f in Guard(Diagnostics.Run) ?? new List<Finding>())
                Line($"  {f.Level,-8} {f.Title}");
        }

        private static void Phase2()
        {
            // ---- ¿sobrevivió todo al reinicio?
            Section("Tras reiniciar: ¿sigue todo aplicado?");
            var applied = Load("applied.txt");
            int still = 0;
            foreach (var cat in Catalogs)
            {
                string area = cat.Key; var tweaks = cat.Value;
                var cache = new TaskCache();
                foreach (var t in tweaks)
                {
                    if (!applied.ContainsKey(Key(area, t))) continue;
                    var st = TweakEngine.GetState(t, cache);
                    if (st == TweakState.Applied) { still++; continue; }
                    Fail($"{t.Name} ({area}): era «Applied» y tras reiniciar es «{st}»");
                    foreach (var a in t.Actions) Line("       " + DescribeAction(a, cache));
                }
            }
            Line($"  {still} de {applied.Count} ajustes siguen aplicados");
            Line("  " + HiberFile() + " (con la hibernación desactivada debería haber desaparecido)");
            var appsBack = (Guard(AppxOps.FindInstalled) ?? new List<InstalledApp>()).Where(a => a.Info.Recommended).ToList();
            foreach (var app in appsBack) Fail($"{app.Info.Name}: volvió a instalarse tras reiniciar");
            if (appsBack.Count == 0) Line("  ok ninguna app quitada volvió tras reiniciar");

            var disabled = Load("startup.txt");
            var now = Guard(StartupManager.List) ?? new List<StartupEntry>();
            foreach (var kv in disabled)
            {
                var e = now.FirstOrDefault(x => StartupKey(x) == kv.Key);
                if (e == null) Warn($"Inicio: {kv.Value} ya no aparece en la lista");
                else if (e.Enabled) Fail($"Inicio: {kv.Value} volvió a activarse tras reiniciar ({DescribeStartup(e)})");
                else Line($"  ok inicio: {kv.Value} sigue desactivado");
            }

            // ---- revertir
            Section("Revertir todo desde «Restaurar»");
            var sessions = (Guard(BackupStore.List) ?? new List<BackupSession>()).Where(s => string.IsNullOrEmpty(s.Reverted)).ToList();
            foreach (var s in sessions)
            {
                int[] r = Guard(() => BackupStore.Revert(s, null));
                if (r == null) continue;
                if (r[1] > 0) Fail($"«{s.Label}»: {r[0]} restaurados, {r[1]} con errores");
                else Line($"  ok «{s.Label}»: {r[0]} restaurados");
            }

            foreach (var kv in disabled)
            {
                var e = now.FirstOrDefault(x => StartupKey(x) == kv.Key);
                if (e != null) Guard(() => { StartupManager.SetEnabled(e, true); return true; });
            }

            Compare("Comparación con el estado original (antes de reiniciar)");
        }

        private static void Phase3()
        {
            Compare("Comparación con el estado original (después de reiniciar)");
            Line("  " + HiberFile() + " (con la hibernación restaurada debería volver a existir)");
        }

        private static string HiberFile()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            var f = new FileInfo(Path.Combine(drive, "hiberfil.sys"));
            try { return f.Exists ? $"hiberfil.sys: existe ({FileCleaner.FormatSize(f.Length)})" : "hiberfil.sys: no existe"; }
            catch (Exception ex) { return "hiberfil.sys: " + ex.Message; }
        }

        private static void Compare(string title)
        {
            Section(title);
            var baseline = Load("baseline.txt");
            var current = Snapshot();
            int same = 0;
            foreach (var kv in baseline)
            {
                current.TryGetValue(kv.Key, out string now);
                if (now == kv.Value) { same++; continue; }
                Fail($"{kv.Key}: original «{kv.Value}», ahora «{now ?? "(no está)"}»");
            }
            foreach (var kv in current.Where(x => !baseline.ContainsKey(x.Key)))
                Warn($"{kv.Key}: nuevo, «{kv.Value}»");
            Line($"  {same} de {baseline.Count} elementos iguales al original");
        }

        // ================================================================== foto del estado

        private static Dictionary<string, string> Snapshot()
        {
            var snap = new Dictionary<string, string>();
            var cache = new TaskCache();
            foreach (var cat in Catalogs)
            foreach (var t in cat.Value)
            foreach (var a in t.Actions)
            {
                try
                {
                    switch (a.Type)
                    {
                        case TweakActionType.Registry:
                            snap["REG " + a.Describe()] = RegText(a.Hive, a.SubKey, a.ValueName);
                            break;
                        case TweakActionType.Service:
                            var info = ServiceOps.GetStartInfo(a.ServiceName);
                            snap["SVC " + a.ServiceName] = info == null ? "(no existe)" : $"inicio {info.Start}{(info.Delayed ? " retrasado" : "")}";
                            break;
                        case TweakActionType.Task:
                            foreach (var task in cache.Find(a.TaskFolder, a.TaskName))
                                snap[$"TASK {task.Folder}\\{task.Name}"] = task.Enabled ? "activada" : "desactivada";
                            break;
                        case TweakActionType.PowerPlan:
                            snap["POWER activo"] = PowerOps.GetActive() ?? "";
                            break;
                    }
                }
                catch (Exception ex)
                {
                    snap["ERR " + a.Describe()] = ex.Message;
                }
            }
            foreach (var e in Guard(StartupManager.List) ?? new List<StartupEntry>())
                snap["STARTUP " + StartupKey(e)] = e.Enabled ? "activado" : "desactivado";
            return snap;
        }

        private static string RegText(RegistryHive hive, string subKey, string name)
        {
            object v = RegistryHelper.GetValue(hive, subKey, name, out var kind);
            if (v == null) return "(no existe)";
            string text;
            if (v is byte[] b) text = BitConverter.ToString(b);
            else if (v is string[] arr) text = string.Join("|", arr);
            else text = Convert.ToString(v);
            return kind + ":" + text;
        }

        private static string DescribeAction(TweakAction a, TaskCache cache)
        {
            switch (a.Type)
            {
                case TweakActionType.Registry:
                    return $"{a.Describe()} = {RegText(a.Hive, a.SubKey, a.ValueName)} (se esperaba {a.Kind}:{a.Value})";
                case TweakActionType.Service:
                    var info = ServiceOps.GetStartInfo(a.ServiceName);
                    return $"servicio {a.ServiceName}: " + (info == null ? "no existe" : $"inicio {info.Start} (se esperaba 4)");
                case TweakActionType.Task:
                    var tasks = cache.Find(a.TaskFolder, a.TaskName);
                    return $"tarea {a.TaskFolder}{a.TaskName}: " +
                           (tasks.Count == 0 ? "no existe" : string.Join(", ", tasks.Select(x => x.Name + (x.Enabled ? " ACTIVADA" : " desactivada"))));
                case TweakActionType.PowerPlan:
                    return $"plan de energía activo {PowerOps.GetActive()} (se esperaba {a.Value})";
            }
            return a.Describe();
        }

        private static string DescribeStartup(StartupEntry e)
        {
            try { return "valor de estado " + RegText(e.Hive, e.StateSubKey, e.StateName) + " · comando " + e.Command; }
            catch (Exception ex) { return ex.Message; }
        }

        // ================================================================== utilidades

        private static string Key(string area, Tweak t) => area + "/" + t.Id;
        private static string StartupKey(StartupEntry e) => e.SourceText + "|" + e.Name;
        private static string Size(long? b) => b.HasValue ? FileCleaner.FormatSize(b.Value) : "—";

        private static T Guard<T>(Func<T> f)
        {
            try { return f(); }
            catch (Exception ex)
            {
                Fail("Excepción: " + ex.GetType().Name + ": " + ex.Message + " @ " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim());
                return default(T);
            }
        }

        private static void Save(string file, Dictionary<string, string> data) =>
            File.WriteAllLines(Path.Combine(_dir, file), data.Select(kv => kv.Key + "\t" + kv.Value), new UTF8Encoding(true));

        private static Dictionary<string, string> Load(string file)
        {
            var d = new Dictionary<string, string>();
            string path = Path.Combine(_dir, file);
            if (!File.Exists(path)) return d;
            foreach (string line in File.ReadAllLines(path))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) d[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return d;
        }

        private static void Section(string title) => Line("\n--- " + title);
        private static void Line(string text) => _report.WriteLine(text);
        private static void Fail(string text) { _fails++; Line("  FALLO " + text); }
        private static void Warn(string text) { _warns++; Line("  AVISO " + text); }
    }
}
#endif
