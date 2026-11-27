using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Cleanup
{
    public static class CleanupEngine
    {
        private static bool IsAnyRunning(string[] processes) =>
            processes != null && processes.Any(p => Process.GetProcessesByName(p).Length > 0);

        private static CleanupResult RunPaths(CleanupItem item, bool measure, bool aggressive)
        {
            var result = new CleanupResult { Id = item.Id };
            var total = new CleanStats();
            int minAge = aggressive ? 0 : item.MinAgeHours;
            foreach (var g in item.Groups)
            {
                if (!measure && IsAnyRunning(g.Processes))
                {
                    result.SkippedApps.Add(g.Name);
                    Logger.Warn("   " + (g.Name.IndexOf("Edge", StringComparison.OrdinalIgnoreCase) >= 0
                        ? L.F("cleanup.log.appOpenEdge", g.Name)
                        : L.F("cleanup.log.appOpen", g.Name)));
                    continue;
                }
                total.Add(FileCleaner.Run(g.Paths, measure, minAge, g.Exclude));
            }
            result.Bytes = total.Bytes;
            result.Files = total.Files;
            result.Failed = total.Failed;
            return result;
        }

        public static List<CleanupResult> Measure(IList<CleanupItem> items, bool aggressive, IProgress<ProgressInfo> progress)
        {
            Logger.Step(L.P("cleanup.log.analyzing", items.Count));
            var results = new List<CleanupResult>();
            long grand = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                progress?.Report(new ProgressInfo(100.0 * i / items.Count, L.F("cleanup.progress.analyzing", item.Name)));
                CleanupResult r;
                try
                {
                    if (item.CustomMeasure != null) r = new CleanupResult { Id = item.Id, Bytes = item.CustomMeasure() };
                    else if (item.Groups.Count > 0) r = RunPaths(item, true, aggressive);
                    else r = new CleanupResult { Id = item.Id, Bytes = null };
                }
                catch (Exception ex)
                {
                    Logger.Warn($"   {item.Name}: {ex.Message}");
                    r = new CleanupResult { Id = item.Id };
                }
                if (r.Bytes.HasValue)
                {
                    grand += r.Bytes.Value;
                    Logger.Info($"{item.Name}: {FileCleaner.FormatSize(r.Bytes.Value)}");
                }
                results.Add(r);
            }
            Logger.Success(L.F("cleanup.log.recoverable", FileCleaner.FormatSize(grand)));
            progress?.Report(new ProgressInfo(100, L.T("cleanup.progress.analyzed")));
            return results;
        }

        public static List<CleanupResult> Clean(IList<CleanupItem> items, bool aggressive, IProgress<ProgressInfo> progress)
        {
            Logger.Step(aggressive
                ? L.P("cleanup.log.cleaningAggressive", items.Count)
                : L.P("cleanup.log.cleaning", items.Count));
            long freeBefore = SystemInfo.FreeSpace;
            var results = new List<CleanupResult>();
            long grand = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                progress?.Report(new ProgressInfo(100.0 * i / items.Count, L.F("cleanup.progress.cleaning", item.Name)));
                Logger.Info(item.Name + "...");
                var r = new CleanupResult { Id = item.Id };
                try
                {
                    item.Before?.Invoke();
                    if (item.Custom != null) r.Bytes = item.Custom();
                    else r = RunPaths(item, false, aggressive);
                }
                catch (Exception ex)
                {
                    Logger.Warn("   " + L.F("cleanup.log.error", ex.Message));
                }
                finally
                {
                    try { item.After?.Invoke(); }
                    catch (Exception ex) { Logger.Warn("   " + ex.Message); }
                }

                if (r.Bytes.HasValue)
                {
                    grand += r.Bytes.Value;
                    string size = FileCleaner.FormatSize(r.Bytes.Value);
                    Logger.Success("   " + (r.Failed > 0
                        ? L.P("cleanup.log.freedSkipped", r.Failed, size)
                        : L.F("cleanup.log.freed", size)));
                }
                results.Add(r);
            }
            long delta = Math.Max(0, SystemInfo.FreeSpace - freeBefore);
            Logger.Step(L.F("cleanup.log.finished", FileCleaner.FormatSize(grand), FileCleaner.FormatSize(delta)));
            progress?.Report(new ProgressInfo(100, L.T("cleanup.progress.cleaned")));
            return results;
        }
    }
}
