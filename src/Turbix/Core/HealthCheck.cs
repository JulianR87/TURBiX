using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Management;
using Turbix.Localization;

namespace Turbix.Core
{
    public enum HealthLevel { Good, Unknown, Attention, Problem }

    public sealed class HealthDetail
    {
        public HealthDetail(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }

    public sealed class HealthComponent
    {
        public string Name { get; set; }
        public string Glyph { get; set; }
        public HealthLevel Level { get; set; } = HealthLevel.Good;

        /// <summary>Una frase con la conclusión («En buen estado», «Desgaste alto»...).</summary>
        public string Headline { get; set; }

        /// <summary>Qué hacer si no está bien (opcional).</summary>
        public string Advice { get; set; }

        public string Help { get; set; }
        public List<HealthDetail> Details { get; } = new List<HealthDetail>();

        /// <summary>true = los datos se muestran en una sola columna (nombres largos).</summary>
        public bool WideDetails { get; set; }

        public void Raise(HealthLevel level, string headline, string advice = null)
        {
            if (level <= Level && Headline != null) return;
            Level = level;
            Headline = headline;
            if (advice != null) Advice = advice;
        }
    }

    /// <summary>
    /// Estado de salud de los componentes del equipo. Solo lee lo que Windows ya sabe:
    /// datos S.M.A.R.T. de los discos, batería, memoria, procesador, gráfica, dispositivos con
    /// problemas de controlador y los errores de hardware y cierres anotados en el Visor de eventos.
    /// </summary>
    public static class HealthCheck
    {
        private const int Days = 30;

        public static List<HealthComponent> Run()
        {
            var list = new List<HealthComponent>();
            Try(() => list.AddRange(Disks()));
            Try(() => { var b = Battery(); if (b != null) list.Add(b); });
            Try(() => list.Add(Memory()));
            Try(() => list.Add(Processor()));
            Try(() => list.AddRange(Graphics()));
            Try(() => list.Add(Devices()));
            Try(() => list.Add(Stability()));
            return list;
        }

        private static string Percent(int value) => L.F("unit.percent", value);

        // ------------------------------------------------------------------ discos

        private static IEnumerable<HealthComponent> Disks()
        {
            var result = new List<HealthComponent>();
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk")))
            {
                foreach (ManagementObject disk in s.Get())
                {
                    int media = ToInt(disk["MediaType"]);
                    string kind = media == 4 ? L.T("health.disk.kind.ssd") : media == 3 ? L.T("health.disk.kind.hdd") : L.T("health.disk.kind.other");
                    string friendly = Convert.ToString(disk["FriendlyName"])?.Trim();
                    var c = new HealthComponent
                    {
                        Name = L.F("health.disk.name", string.IsNullOrEmpty(friendly) ? L.T("health.disk.noName") : friendly),
                        Glyph = "\uEDA2",
                        Help = L.T("health.disk.help"),
                    };
                    c.Details.Add(new HealthDetail(L.T("health.disk.type"), kind));
                    long size = ToLong(disk["Size"]);
                    if (size > 0) c.Details.Add(new HealthDetail(L.T("health.disk.capacity"), FileCleaner.FormatSize(size)));

                    int health = ToInt(disk["HealthStatus"]);   // 0 correcto, 1 advertencia, 2 con fallos
                    if (health == 2)
                        c.Raise(HealthLevel.Problem, L.T("health.disk.failing"), L.T("health.disk.failing.advice"));
                    else if (health == 1)
                        c.Raise(HealthLevel.Attention, L.T("health.disk.warning"), L.T("health.disk.warning.advice"));

                    try
                    {
                        foreach (ManagementObject r in disk.GetRelated("MSFT_StorageReliabilityCounter"))
                        {
                            int wear = ToInt(r["Wear"], -1);
                            int temp = ToInt(r["Temperature"], -1);
                            long hours = ToLong(r["PowerOnHours"]);
                            long badReads = ToLong(r["ReadErrorsUncorrected"]);

                            if (media == 4 && wear >= 0)
                            {
                                c.Details.Add(new HealthDetail(L.T("health.disk.lifeLeft"), Percent(Math.Max(0, 100 - wear))));
                                if (wear >= 95) c.Raise(HealthLevel.Problem, L.T("health.disk.ssdEnd"), L.T("health.disk.ssdEnd.advice"));
                                else if (wear >= 80) c.Raise(HealthLevel.Attention, L.T("health.disk.ssdWear"), L.T("health.disk.ssdWear.advice"));
                            }
                            if (temp > 0 && temp < 120)
                            {
                                c.Details.Add(new HealthDetail(L.T("health.temperature"), L.F("health.unit.celsius", temp)));
                                if (temp >= 65) c.Raise(HealthLevel.Attention, L.T("health.disk.hot"), L.T("health.disk.hot.advice"));
                            }
                            if (hours > 0) c.Details.Add(new HealthDetail(L.T("health.disk.hours"), L.F("health.unit.hours", hours.ToString("N0", L.Culture))));
                            if (badReads > 0)
                            {
                                c.Details.Add(new HealthDetail(L.T("health.disk.readErrors"), badReads.ToString("N0", L.Culture)));
                                c.Raise(HealthLevel.Attention, L.T("health.disk.readErrors.headline"), L.T("health.disk.readErrors.advice"));
                            }
                            break;
                        }
                    }
                    catch
                    {
                        // Algunos discos (USB, RAID) no exponen contadores de fiabilidad.
                    }

                    if (c.Headline == null) c.Headline = L.T("health.goodCondition");
                    result.Add(c);
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ batería

        private static HealthComponent Battery()
        {
            int charge = -1;
            bool found = false;
            using (var s = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining FROM Win32_Battery"))
            {
                foreach (ManagementObject b in s.Get())
                {
                    found = true;
                    charge = ToInt(b["EstimatedChargeRemaining"], -1);
                }
            }
            if (!found) return null;   // equipo de sobremesa

            var c = new HealthComponent
            {
                Name = L.T("health.battery.name"), Glyph = "\uE83F",
                Help = L.T("health.battery.help"),
            };
            if (charge >= 0) c.Details.Add(new HealthDetail(L.T("health.battery.charge"), Percent(charge)));

            long design = WmiSum(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
            long full = WmiSum(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");
            long cycles = WmiSum(@"root\WMI", "SELECT CycleCount FROM BatteryCycleCount", "CycleCount");
            if (cycles > 0) c.Details.Add(new HealthDetail(L.T("health.battery.cycles"), cycles.ToString("N0", L.Culture)));

            if (design > 0 && full > 0)
            {
                int pct = (int)Math.Min(100, Math.Round(100.0 * full / design));
                c.Details.Add(new HealthDetail(L.T("health.battery.capacity"), Percent(pct)));
                if (pct < 40) c.Raise(HealthLevel.Problem, L.T("health.battery.worn.high"), L.T("health.battery.worn.high.advice"));
                else if (pct < 60) c.Raise(HealthLevel.Attention, L.T("health.battery.worn"), L.T("health.battery.worn.advice"));
                else c.Headline = L.T("health.goodCondition");
            }
            else
            {
                c.Level = HealthLevel.Unknown;
                c.Headline = L.T("health.battery.noWearData");
            }
            return c;
        }

        // ------------------------------------------------------------------ memoria

        private static HealthComponent Memory()
        {
            var c = new HealthComponent
            {
                Name = L.T("health.memory.name"), Glyph = "\uE964",
                Help = L.T("health.memory.help"),
            };
            int modules = 0;
            long total = 0;
            var speeds = new List<int>();
            using (var s = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer FROM Win32_PhysicalMemory"))
            {
                foreach (ManagementObject m in s.Get())
                {
                    modules++;
                    total += ToLong(m["Capacity"]);
                    int speed = ToInt(m["Speed"]);
                    if (speed > 0) speeds.Add(speed);
                }
            }
            if (total > 0) c.Details.Add(new HealthDetail(L.T("health.memory.installed"), FileCleaner.FormatSize(total)));
            if (modules > 0) c.Details.Add(new HealthDetail(L.T("health.memory.modules"), modules.ToString(L.Culture)));
            if (speeds.Count > 0) c.Details.Add(new HealthDetail(L.T("health.memory.speed"), L.F("health.unit.mhz", speeds.Min())));

            int results = CountEvents("System", "Microsoft-Windows-MemoryDiagnostics-Results", 1202);   // errores encontrados
            if (results > 0)
                c.Raise(HealthLevel.Problem, L.T("health.memory.errors"), L.T("health.memory.errors.advice"));
            else if (modules == 1 && total >= 8L * 1024 * 1024 * 1024)
                c.Headline = L.T("health.memory.singleModule");
            else
                c.Headline = L.T("health.noErrors");
            return c;
        }

        // ------------------------------------------------------------------ procesador

        /// <summary>«Intel(R) Core(TM) i7-10700K CPU @ 3.80GHz» → «Intel Core i7-10700K».</summary>
        private static string CpuModel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string s = System.Text.RegularExpressions.Regex.Replace(name, @"\((R|TM|tm|r)\)", "");
            int at = s.IndexOf('@');
            if (at > 0) s = s.Substring(0, at);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(CPU|Processor|[0-9]+-Core)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length > 0 ? s : name.Trim();
        }

        private static HealthComponent Processor()
        {
            var c = new HealthComponent
            {
                Name = L.T("health.cpu.name"), Glyph = "\uE950",
                Help = L.F("health.cpu.help", Days),
            };
            using (var s = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, LoadPercentage FROM Win32_Processor"))
            {
                // Puede haber más de un procesador (placas de dos zócalos y casi todas las máquinas virtuales):
                // se suman núcleos e hilos y se promedia la carga.
                string model = null;
                int sockets = 0, cores = 0, threads = 0, load = 0;
                foreach (ManagementObject p in s.Get())
                {
                    if (model == null) model = CpuModel(Convert.ToString(p["Name"]));
                    sockets++;
                    cores += ToInt(p["NumberOfCores"]);
                    threads += ToInt(p["NumberOfLogicalProcessors"]);
                    load += ToInt(p["LoadPercentage"]);
                }
                if (!string.IsNullOrEmpty(model)) c.Name = L.F("health.cpu.nameModel", model);
                if (sockets > 0)
                {
                    c.Details.Add(new HealthDetail(L.T("health.cpu.coresThreads"), L.F("health.cpu.coresThreads.value", cores, threads)));
                    if (sockets > 1) c.Details.Add(new HealthDetail(L.T("health.cpu.sockets"), sockets.ToString(L.Culture)));
                    c.Details.Add(new HealthDetail(L.T("health.cpu.load"), Percent(load / sockets)));
                }
            }

            double temp = ThermalZone();
            if (temp > 0)
            {
                c.Details.Add(new HealthDetail(L.T("health.cpu.temperature"), L.F("health.unit.celsius", Math.Round(temp))));
                if (temp >= 95)
                    c.Raise(HealthLevel.Attention, L.T("health.cpu.hot"), L.T("health.cpu.hot.advice"));
            }

            int whea = CountEvents("System", "Microsoft-Windows-WHEA-Logger", 0);
            if (whea > 0)
            {
                c.Details.Add(new HealthDetail(L.F("health.cpu.whea.label", Days), whea.ToString(L.Culture)));
                c.Raise(HealthLevel.Attention, L.T("health.cpu.whea"), L.T("health.cpu.whea.advice"));
            }
            if (c.Headline == null) c.Headline = L.T("health.noErrors");
            return c;
        }

        private static double ThermalZone()
        {
            try
            {
                double max = 0;
                using (var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject z in s.Get())
                    {
                        double celsius = ToLong(z["CurrentTemperature"]) / 10.0 - 273.15;   // décimas de kelvin
                        if (celsius > max && celsius < 130) max = celsius;
                    }
                }
                return max;
            }
            catch
            {
                return 0;   // muchos equipos no lo exponen
            }
        }

        // ------------------------------------------------------------------ gráfica

        private static IEnumerable<HealthComponent> Graphics()
        {
            var result = new List<HealthComponent>();
            using (var s = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate, ConfigManagerErrorCode FROM Win32_VideoController"))
            {
                foreach (ManagementObject g in s.Get())
                {
                    string name = Convert.ToString(g["Name"])?.Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    var c = new HealthComponent
                    {
                        Name = L.F("health.gpu.name", name), Glyph = "\uE7F4",
                        Help = L.T("health.gpu.help"),
                    };
                    string version = Convert.ToString(g["DriverVersion"]);
                    if (!string.IsNullOrEmpty(version)) c.Details.Add(new HealthDetail(L.T("health.gpu.driverVersion"), version));

                    int error = ToInt(g["ConfigManagerErrorCode"]);
                    if (error != 0 && error != 22)
                        c.Raise(HealthLevel.Problem, L.F("health.gpu.error", error), L.T("health.gpu.error.advice"));

                    DateTime? date = WmiDate(Convert.ToString(g["DriverDate"]));
                    if (date.HasValue)
                    {
                        c.Details.Add(new HealthDetail(L.T("health.gpu.driverDate"), date.Value.ToString(L.T("health.dateFormat"), L.Culture)));
                        bool basic = name.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (basic)
                            c.Raise(HealthLevel.Attention, L.T("health.gpu.basic"), L.T("health.gpu.basic.advice"));
                        else if ((DateTime.Now - date.Value).TotalDays > 730)
                            c.Raise(HealthLevel.Attention, L.T("health.gpu.oldDriver"), L.T("health.gpu.oldDriver.advice"));
                    }
                    if (c.Headline == null) c.Headline = L.T("health.gpu.ok");
                    result.Add(c);
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ dispositivos

        private static HealthComponent Devices()
        {
            var c = new HealthComponent
            {
                Name = L.T("health.dev.name"), Glyph = "\uE772", WideDetails = true,
                Help = L.T("health.dev.help"),
            };
            int count = 0;
            using (var s = new ManagementObjectSearcher("SELECT Name, Caption, PNPClass, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
            {
                foreach (ManagementObject d in s.Get())
                {
                    int code = ToInt(d["ConfigManagerErrorCode"]);
                    if (code == 22 || code == 45) continue;   // desactivado por el usuario / no conectado
                    count++;
                    if (count <= 8)
                        c.Details.Add(new HealthDetail(DeviceName(d), DeviceError(code)));
                }
            }
            if (count == 0)
            {
                c.Headline = L.T("health.dev.allOk");
            }
            else
            {
                c.Raise(HealthLevel.Attention, L.P("health.dev.problems", count), L.T("health.dev.problems.advice"));
            }
            return c;
        }

        private static string DeviceName(ManagementObject d)
        {
            foreach (string property in new[] { "Name", "Caption" })
            {
                string value = Convert.ToString(d[property]);
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            // Sin controlador Windows no sabe cómo se llama: se muestra su clase o su identificador de hardware.
            string cls = Convert.ToString(d["PNPClass"]);
            string id = Convert.ToString(d["DeviceID"]);
            if (!string.IsNullOrWhiteSpace(id) && id.Length > 60) id = id.Substring(0, 60) + "…";
            return L.T("health.dev.unknown") + (string.IsNullOrWhiteSpace(cls) ? "" : " (" + cls + ")") +
                   (string.IsNullOrWhiteSpace(id) ? "" : " · " + id);
        }

        private static string DeviceError(int code)
        {
            switch (code)
            {
                case 1: case 18: case 28: return L.T("health.dev.err.noDriver");
                case 10: return L.T("health.dev.err.cantStart");
                case 31: case 39: case 52: return L.T("health.dev.err.driverLoad");
                case 43: return L.T("health.dev.err.stopped");
                case 12: return L.T("health.dev.err.resources");
                default: return L.F("health.dev.err.code", code);
            }
        }

        // ------------------------------------------------------------------ estabilidad

        private static HealthComponent Stability()
        {
            var c = new HealthComponent
            {
                Name = L.T("health.stab.name"), Glyph = "\uE9D9",
                Help = L.F("health.stab.help", Days),
            };
            int blue = CountEvents("System", "Microsoft-Windows-WER-SystemErrorReporting", 1001);
            int power = CountEvents("System", "Microsoft-Windows-Kernel-Power", 41);
            int disk = CountEvents("System", "disk", 7) + CountEvents("System", "disk", 51);
            c.Details.Add(new HealthDetail(L.T("health.stab.bsod"), blue.ToString(L.Culture)));
            c.Details.Add(new HealthDetail(L.T("health.stab.power"), power.ToString(L.Culture)));
            c.Details.Add(new HealthDetail(L.T("health.stab.disk"), disk.ToString(L.Culture)));

            if (disk > 0)
                c.Raise(HealthLevel.Attention, L.T("health.stab.diskErrors"), L.T("health.stab.diskErrors.advice"));
            if (blue >= 3)
                c.Raise(HealthLevel.Problem, L.P("health.stab.bsodMany", blue, Days), L.T("health.stab.bsodMany.advice"));
            else if (blue > 0 || power >= 3)
                c.Raise(HealthLevel.Attention, L.T("health.stab.crashes"), L.T("health.stab.crashes.advice"));
            if (c.Headline == null) c.Headline = L.F("health.stab.ok", Days);
            return c;
        }

        // ------------------------------------------------------------------ utilidades

        private static int CountEvents(string log, string provider, int eventId)
        {
            try
            {
                long ms = Days * 24L * 3600 * 1000;
                string id = eventId > 0 ? $" and EventID={eventId}" : string.Empty;
                string query = $"*[System[Provider[@Name='{provider}']{id} and TimeCreated[timediff(@SystemTime) <= {ms}]]]";
                int count = 0;
                using (var reader = new EventLogReader(new EventLogQuery(log, PathType.LogName, query)))
                {
                    EventRecord rec;
                    while ((rec = reader.ReadEvent()) != null)
                    {
                        rec.Dispose();
                        if (++count >= 999) break;
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private static long WmiSum(string scope, string query, string property)
        {
            try
            {
                long sum = 0;
                using (var s = new ManagementObjectSearcher(scope, query))
                    foreach (ManagementObject mo in s.Get()) sum += ToLong(mo[property]);
                return sum;
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime? WmiDate(string value)
        {
            try { return string.IsNullOrEmpty(value) ? (DateTime?)null : ManagementDateTimeConverter.ToDateTime(value); }
            catch { return null; }
        }

        private static int ToInt(object value, int fallback = 0)
        {
            try { return value == null ? fallback : Convert.ToInt32(value); }
            catch { return fallback; }
        }

        private static long ToLong(object value)
        {
            try { return value == null ? 0 : Convert.ToInt64(value); }
            catch { return 0; }
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warn(L.F("health.log.error", ex.Message)); }
        }
    }
}
