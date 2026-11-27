using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Una fila de la lista de procesos. Si abrió otros procesos, se despliega para verlos.</summary>
    public sealed class ProcessNode : ObservableObject
    {
        private readonly ProcessesViewModel _owner;
        private string _title, _subtitle, _cpuText, _memoryText, _threadsText, _countText;
        private ImageSource _icon;
        private bool _isExpanded, _effectiveExpanded, _hasChildren, _isHot;
        private int _depth;

        internal ProcessNode(ProcessesViewModel owner, ProcessSample sample)
        {
            _owner = owner;
            Sample = sample;
            ToggleCommand = new RelayCommand(() => { IsExpanded = !EffectiveExpanded; _owner.Rebuild(); });
            EndCommand = new RelayCommand(() => _owner.End(this), () => CanEnd);
        }

        internal ProcessSample Sample { get; set; }
        internal ProcessNode Parent { get; set; }
        internal List<ProcessNode> Children { get; } = new List<ProcessNode>();
        internal long LastCpuTime { get; set; } = -1;
        internal double Cpu { get; set; }
        internal double GroupCpu { get; set; }
        internal long GroupMemory { get; set; }
        internal int GroupThreads { get; set; }
        internal int GroupCount { get; set; }
        internal string Path { get; set; }
        internal string Description { get; set; }
        internal List<string> Services { get; set; }
        internal bool Resolved { get; set; }

        public int Pid => Sample.Pid;
        public bool CanEnd => !ProcessOps.IsCritical(Sample);
        public string EndTooltip => CanEnd ? L.T("proc.end") : L.T("proc.critical");

        public string Title { get => _title; set => Set(ref _title, value); }
        public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
        public ImageSource Icon
        {
            get => _icon;
            set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(HasIcon)); }
        }
        public bool HasIcon => _icon != null;
        public string CpuText { get => _cpuText; set => Set(ref _cpuText, value); }
        public string MemoryText { get => _memoryText; set => Set(ref _memoryText, value); }
        public string ThreadsText { get => _threadsText; set => Set(ref _threadsText, value); }
        public string CountText { get => _countText; set => Set(ref _countText, value); }

        /// <summary>Uso de procesador alto: se resalta como en el Administrador de tareas.</summary>
        public bool IsHot { get => _isHot; set => Set(ref _isHot, value); }

        public int Depth
        {
            get => _depth;
            set { if (Set(ref _depth, value)) OnPropertyChanged(nameof(Indent)); }
        }
        public Thickness Indent => new Thickness(_depth * 22, 0, 0, 0);

        public bool HasChildren { get => _hasChildren; set => Set(ref _hasChildren, value); }

        /// <summary>Lo que eligió el usuario (se recuerda mientras el proceso exista).</summary>
        public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

        /// <summary>Desplegado de verdad: también al buscar, si coincide algo de dentro.</summary>
        public bool EffectiveExpanded { get => _effectiveExpanded; set => Set(ref _effectiveExpanded, value); }

        public ICommand ToggleCommand { get; }
        public ICommand EndCommand { get; }
    }

    /// <summary>
    /// Procesos abiertos, como en el Administrador de tareas, con los procesos que abrió cada programa agrupados
    /// debajo de él. Se actualiza cada 2 segundos mientras la pestaña está a la vista.
    /// </summary>
    public sealed class ProcessesViewModel : ObservableObject
    {
        /// <summary>
        /// Procesos de Windows que abren casi todo lo demás (el Explorador abre los programas, los servicios abren
        /// tareas…). Lo que abren se muestra suelto, no colgado de ellos; si no, todo quedaría dentro del Explorador.
        /// </summary>
        private static readonly HashSet<string> Launchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Registry", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "services.exe", "svchost.exe",
            "explorer.exe", "userinit.exe", "sihost.exe", "taskhostw.exe", "RuntimeBroker.exe", "dllhost.exe", "lsass.exe",
            "taskeng.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "ApplicationFrameHost.exe",
        };

        private readonly Dictionary<string, ProcessNode> _nodes = new Dictionary<string, ProcessNode>();
        private readonly Dictionary<string, ImageSource> _icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _timer;
        private List<ProcessNode> _roots = new List<ProcessNode>();
        private Dictionary<int, List<string>> _services = new Dictionary<int, List<string>>();
        private DateTime _lastSample, _lastServices;
        private bool _busy, _loaded;
        private string _search = string.Empty, _sort = "name";
        private bool _descending;
        private string _countsText = "…", _cpuUsageText = "", _memoryUsageText = "", _emptyText;
        private bool _isEmpty;

        public ProcessesViewModel()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += async (s, e) => await RefreshAsync();
            SortCommand = new RelayCommand(p => SortBy(p as string));
            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            ExpandAllCommand = new RelayCommand(() => SetAllExpanded(true));
            CollapseAllCommand = new RelayCommand(() => SetAllExpanded(false));
        }

        public ObservableCollection<ProcessNode> Rows { get; } = new ObservableCollection<ProcessNode>();

        public ICommand SortCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }

        public string CountsText { get => _countsText; private set => Set(ref _countsText, value); }
        public string CpuUsageText { get => _cpuUsageText; private set => Set(ref _cpuUsageText, value); }
        public string MemoryUsageText { get => _memoryUsageText; private set => Set(ref _memoryUsageText, value); }
        public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }
        public string EmptyText { get => _emptyText; private set => Set(ref _emptyText, value); }

        public string SearchText
        {
            get => _search;
            set { if (Set(ref _search, value ?? string.Empty)) Rebuild(); }
        }

        public string SortColumn => _sort;
        public bool SortDescending => _descending;

        // Flecha de orden en la cabecera de cada columna (vacía si no es la columna elegida).
        public string ArrowName => Arrow("name");
        public string ArrowCpu => Arrow("cpu");
        public string ArrowMemory => Arrow("memory");
        public string ArrowThreads => Arrow("threads");
        private string Arrow(string column) => _sort != column ? string.Empty : _descending ? "" : "";

        // ------------------------------------------------------------------ ciclo de vida

        /// <summary>Empieza a actualizar (al mostrar la pestaña).</summary>
        public void Start()
        {
            if (_timer.IsEnabled) return;
            _timer.Start();
            _ = RefreshAsync();
        }

        /// <summary>Deja de actualizar (al ocultarla): no gasta procesador si nadie mira.</summary>
        public void Stop() => _timer.Stop();

        private sealed class Resolution
        {
            public string Key, Path, Description;
            public ImageSource Icon;
        }

        public async Task RefreshAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var known = new HashSet<string>(_nodes.Values.Where(n => n.Resolved).Select(n => n.Sample.Key));
                bool needServices = (DateTime.UtcNow - _lastServices).TotalSeconds > 15;
                var result = await Task.Run(() =>
                {
                    var snap = ProcessOps.Take();
                    var services = needServices ? ProcessOps.ServicesByPid() : null;
                    // Ruta, descripción e icono solo de los procesos nuevos: abrir cada proceso cuesta.
                    var resolved = new List<Resolution>();
                    foreach (var p in snap.Processes)
                    {
                        if (known.Contains(p.Key)) continue;
                        string path = p.Pid > 4 ? ProcessOps.ImagePath(p.Pid) : null;
                        resolved.Add(new Resolution { Key = p.Key, Path = path, Description = ProcessOps.Description(path), Icon = IconFor(path) });
                    }
                    return new { snap, services, resolved };
                });
                if (result.services != null)
                {
                    _services = result.services;
                    _lastServices = DateTime.UtcNow;
                }
                Apply(result.snap, result.resolved);
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("proc.log.readError", ex.Message));
            }
            finally
            {
                _busy = false;
            }
        }

        private ImageSource IconFor(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (_icons)
            {
                if (_icons.TryGetValue(path, out var cached)) return cached;
            }
            ImageSource icon = null;
            try
            {
                using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (ico != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        icon = src;
                    }
                }
            }
            catch
            {
                // Sin icono: se muestra el genérico.
            }
            lock (_icons) _icons[path] = icon;
            return icon;
        }

        // ------------------------------------------------------------------ aplicar una toma

        private void Apply(ProcessSnapshot snap, List<Resolution> resolved)
        {
            double elapsed = _lastSample == default(DateTime) ? 0 : (snap.Taken - _lastSample).TotalMilliseconds * 10000;   // en 100 ns
            _lastSample = snap.Taken;
            int cores = Math.Max(1, Environment.ProcessorCount);
            var byResolution = resolved.ToDictionary(r => r.Key);

            // Nodos: se reutilizan los existentes para no perder lo desplegado ni la posición de la lista.
            var alive = new Dictionary<string, ProcessNode>();
            foreach (var s in snap.Processes)
            {
                if (!_nodes.TryGetValue(s.Key, out var node)) node = new ProcessNode(this, s);
                long previous = node.LastCpuTime;
                node.Sample = s;
                node.Cpu = previous >= 0 && elapsed > 0 ? Math.Max(0, Math.Min(100, 100.0 * (s.CpuTime - previous) / (elapsed * cores))) : 0;
                node.LastCpuTime = s.CpuTime;
                if (byResolution.TryGetValue(s.Key, out var r))
                {
                    node.Path = r.Path;
                    node.Description = r.Description;
                    node.Icon = r.Icon;
                    node.Resolved = true;
                }
                node.Services = _services.TryGetValue(s.Pid, out var svc) ? svc : null;
                node.Parent = null;
                node.Children.Clear();
                alive[s.Key] = node;
            }
            _nodes.Clear();
            foreach (var kv in alive) _nodes[kv.Key] = kv.Value;

            // Árbol: cada proceso cuelga del que lo abrió, si ese sigue abierto, es más antiguo
            // (un PID se reutiliza) y no es un proceso de Windows que abre de todo.
            var byPid = alive.Values.ToDictionary(n => n.Pid);
            var roots = new List<ProcessNode>();
            foreach (var node in alive.Values)
            {
                if (node.Sample.ParentPid != node.Pid &&
                    byPid.TryGetValue(node.Sample.ParentPid, out var parent) &&
                    parent.Sample.CreateTime <= node.Sample.CreateTime &&
                    !Launchers.Contains(parent.Sample.ImageName))
                {
                    node.Parent = parent;
                    parent.Children.Add(node);
                }
                else roots.Add(node);
            }
            // Un ciclo (muy raro, por PID reutilizados) dejaría procesos sin raíz: se sueltan.
            var reachable = new HashSet<ProcessNode>();
            foreach (var r in roots) Mark(r, reachable);
            foreach (var orphan in alive.Values.Where(n => !reachable.Contains(n)).ToList())
            {
                orphan.Parent?.Children.Remove(orphan);
                orphan.Parent = null;
                roots.Add(orphan);
                Mark(orphan, reachable);
            }
            foreach (var r in roots) Aggregate(r);
            _roots = roots;

            // Resumen, con los mismos datos que muestra el Administrador de tareas en Rendimiento › CPU.
            int count = snap.Processes.Count;
            long threads = snap.Processes.Sum(p => (long)p.Threads);
            long handles = snap.Processes.Sum(p => (long)p.Handles);
            CountsText = L.P("proc.count.processes", count) + "  ·  " + L.P("proc.count.threads", threads) + "  ·  " + L.P("proc.count.handles", handles);
            CpuUsageText = L.F("proc.usage.cpu", Percent(snap.CpuPercent));
            MemoryUsageText = snap.MemoryTotal > 0
                ? L.F("proc.usage.memory", FileCleaner.FormatSize(snap.MemoryUsed), FileCleaner.FormatSize(snap.MemoryTotal))
                : string.Empty;

            _loaded = true;
            Rebuild();
        }

        private static void Mark(ProcessNode n, HashSet<ProcessNode> seen)
        {
            if (!seen.Add(n)) return;
            foreach (var c in n.Children) Mark(c, seen);
        }

        private static void Aggregate(ProcessNode n)
        {
            n.GroupCpu = n.Cpu;
            n.GroupMemory = n.Sample.PrivateWorkingSet;
            n.GroupThreads = n.Sample.Threads;
            n.GroupCount = 1;
            foreach (var c in n.Children)
            {
                Aggregate(c);
                n.GroupCpu += c.GroupCpu;
                n.GroupMemory += c.GroupMemory;
                n.GroupThreads += c.GroupThreads;
                n.GroupCount += c.GroupCount;
            }
        }

        private static string Percent(double value) =>
            L.F("unit.percent", value < 0.05 ? "0" : value.ToString(value < 10 ? "0.0" : "0", L.Culture));

        // ------------------------------------------------------------------ lista visible

        internal void Rebuild()
        {
            if (!_loaded) return;
            string q = _search.Trim();
            var visible = new List<ProcessNode>();
            foreach (var root in Sorted(_roots)) Flatten(root, 0, q, visible);

            // Se actualiza la colección en el sitio (mover, insertar, quitar) para que la lista no salte.
            var target = new HashSet<ProcessNode>(visible);
            for (int i = Rows.Count - 1; i >= 0; i--)
                if (!target.Contains(Rows[i])) Rows.RemoveAt(i);
            for (int i = 0; i < visible.Count; i++)
            {
                var node = visible[i];
                if (i < Rows.Count && ReferenceEquals(Rows[i], node)) continue;
                int at = Rows.IndexOf(node);
                if (at >= 0) Rows.Move(at, i);
                else Rows.Insert(i, node);
            }

            IsEmpty = visible.Count == 0;
            EmptyText = q.Length > 0 ? L.T("proc.noMatches") : L.T("proc.loading");
        }

        /// <summary>Agrega el nodo y, si está desplegado, sus hijos. Devuelve si quedó algo visible.</summary>
        private bool Flatten(ProcessNode node, int depth, string q, List<ProcessNode> output)
        {
            UpdateTexts(node);
            bool selfMatch = q.Length == 0 || Matches(node, q);
            int index = output.Count;
            output.Add(node);
            node.Depth = depth;
            node.HasChildren = node.Children.Count > 0;

            // Al buscar se despliega lo necesario para mostrar lo que coincide.
            bool expanded = node.IsExpanded;
            var childOutput = new List<ProcessNode>();
            bool childMatch = false;
            if (node.HasChildren && (expanded || q.Length > 0))
            {
                foreach (var c in Sorted(node.Children))
                    childMatch |= Flatten(c, depth + 1, q, childOutput);
            }
            if (q.Length > 0 && childMatch) expanded = true;
            if (q.Length > 0 && !selfMatch && !childMatch)
            {
                output.RemoveAt(index);
                return false;
            }
            node.EffectiveExpanded = expanded && node.HasChildren;
            if (node.EffectiveExpanded) output.AddRange(childOutput);
            UpdateTexts(node);
            return true;
        }

        private static bool Matches(ProcessNode n, string q)
        {
            var cmp = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions opts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            if (cmp.IndexOf(n.Title ?? "", q, opts) >= 0) return true;
            if (cmp.IndexOf(n.Sample.ImageName ?? "", q, opts) >= 0) return true;
            if (n.Pid.ToString(CultureInfo.InvariantCulture) == q) return true;
            return n.Services != null && n.Services.Any(s => cmp.IndexOf(s, q, opts) >= 0);
        }

        private IEnumerable<ProcessNode> Sorted(IEnumerable<ProcessNode> nodes)
        {
            IOrderedEnumerable<ProcessNode> ordered;
            switch (_sort)
            {
                case "cpu":
                    ordered = _descending ? nodes.OrderByDescending(n => Math.Round(n.GroupCpu, 1)) : nodes.OrderBy(n => Math.Round(n.GroupCpu, 1));
                    break;
                case "memory":
                    ordered = _descending ? nodes.OrderByDescending(n => n.GroupMemory) : nodes.OrderBy(n => n.GroupMemory);
                    break;
                case "threads":
                    ordered = _descending ? nodes.OrderByDescending(n => n.GroupThreads) : nodes.OrderBy(n => n.GroupThreads);
                    break;
                default:
                    var cmp = StringComparer.Create(L.Culture, true);
                    ordered = _descending ? nodes.OrderByDescending(n => TitleOf(n), cmp) : nodes.OrderBy(n => TitleOf(n), cmp);
                    break;
            }
            return ordered.ThenBy(n => n.Pid);
        }

        private void SortBy(string column)
        {
            if (string.IsNullOrEmpty(column)) return;
            if (_sort == column) _descending = !_descending;
            else
            {
                _sort = column;
                _descending = column != "name";   // números: de mayor a menor primero
            }
            OnPropertyChanged(nameof(SortColumn));
            OnPropertyChanged(nameof(SortDescending));
            OnPropertyChanged(nameof(ArrowName));
            OnPropertyChanged(nameof(ArrowCpu));
            OnPropertyChanged(nameof(ArrowMemory));
            OnPropertyChanged(nameof(ArrowThreads));
            Rebuild();
        }

        private void SetAllExpanded(bool expanded)
        {
            foreach (var n in _nodes.Values) n.IsExpanded = expanded;
            Rebuild();
        }

        // ------------------------------------------------------------------ textos de cada fila

        private static string TitleOf(ProcessNode n)
        {
            var s = n.Sample;
            if (n.Services != null && n.Services.Count > 0)
            {
                string first = n.Services[0];
                return n.Services.Count == 1 ? L.F("proc.serviceHost", first) : L.P("proc.serviceHostMore", n.Services.Count - 1, first);
            }
            if (!string.IsNullOrEmpty(n.Description) && !string.Equals(n.Description, s.ImageName, StringComparison.OrdinalIgnoreCase))
                return n.Description;
            return s.ImageName;
        }

        private static void UpdateTexts(ProcessNode n)
        {
            var s = n.Sample;
            n.Title = TitleOf(n);
            n.Subtitle = string.Equals(n.Title, s.ImageName, StringComparison.OrdinalIgnoreCase)
                ? L.F("proc.pid", s.Pid)
                : s.ImageName + "  ·  " + L.F("proc.pid", s.Pid);

            // Plegado, la fila muestra el total del grupo (como el Administrador de tareas); desplegado, lo propio.
            bool group = n.Children.Count > 0 && !n.EffectiveExpanded;
            double cpu = group ? n.GroupCpu : n.Cpu;
            n.CpuText = Percent(cpu);
            n.IsHot = cpu >= 25;
            n.MemoryText = FileCleaner.FormatSize(group ? n.GroupMemory : s.PrivateWorkingSet);
            n.ThreadsText = (group ? n.GroupThreads : s.Threads).ToString("N0", L.Culture);
            n.CountText = n.Children.Count > 0 ? "  (" + n.GroupCount.ToString("N0", L.Culture) + ")" : string.Empty;
        }

        // ------------------------------------------------------------------ finalizar

        internal void End(ProcessNode node)
        {
            if (!node.CanEnd) return;
            var all = new List<ProcessNode>();
            Collect(node, all);
            var killable = all.Where(n => n.CanEnd).ToList();
            string text = killable.Count > 1
                ? L.P("proc.end.textGroup", killable.Count, node.Title)
                : L.F("proc.end.text", node.Title);
            if (!Dialogs.Confirm(L.F("proc.end.title", node.Title), text, L.T("proc.end.confirm"))) return;

            // Primero los de dentro, para que el principal no los vuelva a abrir mientras se cierran.
            var errors = new List<string>();
            foreach (var n in Enumerable.Reverse(killable))
            {
                string error = ProcessOps.Kill(n.Sample);
                if (error == null) Logger.Info(L.F("proc.log.ended", n.Title, n.Pid));
                else
                {
                    Logger.Warn(L.F("proc.log.failed", n.Title, n.Pid, error));
                    errors.Add(error);
                }
            }
            if (errors.Count > 0)
                Dialogs.Show(L.T("proc.end.failed.title"), L.F("proc.end.failed.text", node.Title, errors[0]), DialogKind.Warning);
            _ = RefreshAsync();
        }

        private static void Collect(ProcessNode n, List<ProcessNode> output)
        {
            output.Add(n);
            foreach (var c in n.Children) Collect(c, output);
        }
    }
}
