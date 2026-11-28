using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
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
    // ====================================================================== filas

    /// <summary>Un programa y lo que mueve por la red (se juntan todos sus procesos).</summary>
    public sealed class AppTrafficRow : ObservableObject
    {
        private string _title, _subtitle, _downText = "—", _upText = "—", _rxText = "—", _txText = "—", _connText = "—";
        private ImageSource _icon;
        private bool _active, _isBlocked, _canBlock;

        internal AppTrafficRow(NetworkViewModel owner)
        {
            BlockCommand = new RelayCommand(() => owner.BlockApp(this), () => CanBlock && !IsBlocked);
            ConnectionsCommand = new RelayCommand(() => owner.ShowConnectionsOf(this));
        }

        internal string Key;
        internal string Path, Image;
        internal bool Critical, Running = true;
        internal double DownRate, UpRate;
        internal long TotalRx, TotalTx;
        internal int Connections;
        internal readonly HashSet<int> Pids = new HashSet<int>();

        public string Title { get => _title; set => Set(ref _title, value); }
        public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
        public ImageSource Icon { get => _icon; set { if (Set(ref _icon, value)) OnPropertyChanged(nameof(HasIcon)); } }
        public bool HasIcon => _icon != null;
        public string DownText { get => _downText; set => Set(ref _downText, value); }
        public string UpText { get => _upText; set => Set(ref _upText, value); }
        public string RxText { get => _rxText; set => Set(ref _rxText, value); }
        public string TxText { get => _txText; set => Set(ref _txText, value); }
        public string ConnText { get => _connText; set => Set(ref _connText, value); }

        /// <summary>Está moviendo datos ahora mismo: se resalta como el procesador alto en Procesos.</summary>
        public bool Active { get => _active; set => Set(ref _active, value); }

        public bool CanBlock { get => _canBlock; set { if (Set(ref _canBlock, value)) OnPropertyChanged(nameof(BlockTooltip)); } }
        public bool IsBlocked { get => _isBlocked; set { if (Set(ref _isBlocked, value)) OnPropertyChanged(nameof(BlockTooltip)); } }
        public string BlockTooltip => IsBlocked ? L.T("net.block.already") : CanBlock ? L.T("net.block.app") : L.T("net.block.appNo");

        public ICommand BlockCommand { get; }
        public ICommand ConnectionsCommand { get; }
    }

    /// <summary>Una conexión TCP abierta.</summary>
    public sealed class ConnectionRow : ObservableObject
    {
        private string _state;

        internal ConnectionRow(NetworkViewModel owner)
        {
            CloseCommand = new RelayCommand(() => owner.CloseConnection(this), () => Endpoint != null && Endpoint.CanClose);
            BlockAddressCommand = new RelayCommand(() => owner.BlockAddress(this), () => CanBlockAddress);
        }

        internal NetEndpoint Endpoint;
        internal string AppKey;
        private string _process, _detail;
        public string Process { get => _process; set => Set(ref _process, value); }
        public string Detail { get => _detail; set => Set(ref _detail, value); }
        public string Remote { get; set; }
        public string RemoteDetail { get; set; }
        public bool CanBlockAddress { get; set; }
        public bool CanClose => Endpoint != null && Endpoint.CanClose;
        public string CloseTooltip => CanClose ? L.T("net.close") : L.T("net.closeNo");
        public string BlockAddressTooltip => CanBlockAddress ? L.T("net.block.address") : L.T("net.block.addressNo");
        public bool IsInternet { get; set; }
        public string State { get => _state; set => Set(ref _state, value); }

        public ICommand CloseCommand { get; }
        public ICommand BlockAddressCommand { get; }
    }

    /// <summary>Un puerto en el que un programa espera conexiones (TCP) o recibe datos (UDP).</summary>
    public sealed class PortRow : ObservableObject
    {
        internal PortRow(NetworkViewModel owner)
        {
            BlockPortCommand = new RelayCommand(() => owner.BlockPort(this), () => Exposed);
        }

        internal NetEndpoint Endpoint;
        internal string Key;
        private string _process;
        public string Process { get => _process; set => Set(ref _process, value); }
        public string Detail { get; set; }
        public string Exposure { get; set; }
        public bool Exposed { get; set; }
        public bool Review { get; set; }
        public string BlockTooltip => Exposed ? L.T("net.block.port") : L.T("net.block.portNo");

        public ICommand BlockPortCommand { get; }
    }

    /// <summary>Un bloqueo creado por TURBiX en el Firewall de Windows.</summary>
    public sealed class BlockRow : ObservableObject
    {
        internal BlockRow(NetworkViewModel owner, BlockEntry entry)
        {
            Entry = entry;
            RemoveCommand = new RelayCommand(() => owner.RemoveBlock(this));
        }

        internal BlockEntry Entry;
        public string Glyph { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Directions { get; set; }
        public ICommand RemoveCommand { get; }
    }

    // ====================================================================== modelo de la página

    /// <summary>
    /// Monitor de red: qué programas usan la red y cuánto, las conexiones abiertas, los puertos que esperan conexiones
    /// y los bloqueos hechos con el Firewall de Windows. Se actualiza cada segundo solo mientras la página está a la
    /// vista, y no usa Internet: no resuelve nombres de las direcciones (eso enviaría consultas DNS), muestra la IP.
    /// </summary>
    public sealed class NetworkViewModel : ObservableObject
    {
        private sealed class ProcInfo
        {
            public string Name, Path, Description;
            public ImageSource Icon;
            public bool Critical;
        }

        private sealed class Snapshot
        {
            public List<NetEndpoint> Endpoints;
            public List<ProcessSample> Processes;
            public Dictionary<int, TrafficCount> Traffic;
            public Dictionary<int, List<string>> Services;
            public HashSet<string> Infrastructure;
            public Dictionary<string, ProcInfo> Resolved = new Dictionary<string, ProcInfo>();
            public bool HasTotals;
            public long Rx, Tx, Link;
            public DateTime Taken;
        }

        private const int MaxApps = 400;

        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, ProcInfo> _info = new Dictionary<string, ProcInfo>();
        private readonly Dictionary<string, ImageSource> _icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AppTrafficRow> _apps = new Dictionary<string, AppTrafficRow>();
        private readonly Dictionary<string, ConnectionRow> _conns = new Dictionary<string, ConnectionRow>();
        private readonly Dictionary<string, PortRow> _ports = new Dictionary<string, PortRow>();
        private Dictionary<int, TrafficCount> _lastTraffic = new Dictionary<int, TrafficCount>();
        private Dictionary<int, List<string>> _services = new Dictionary<int, List<string>>();
        private List<BlockEntry> _blockEntries = new List<BlockEntry>();
        private HashSet<string> _infrastructure = new HashSet<string>();
        private DateTime _lastTaken, _lastServices, _lastBlocks, _lastInfra;
        private long _lastAdapterRx = -1, _lastAdapterTx;
        private long _sessionRx, _sessionTx;
        private double _downRate, _upRate;
        private bool _busy, _trafficOk;
        private string _search = string.Empty;
        private string _appSort = "down", _connSort = "process", _portSort = "port";
        private bool _appDesc = true, _connDesc, _portDesc;
        private bool _showClosing, _hold;

        private string _downText = "—", _upText = "—", _downTotal = "", _upTotal = "", _sumConns = "…", _sumPorts = "", _sumExposed = "", _linkText = "";
        private bool _hasExposed;
        private string _trafficNotice;
        private bool _firewallOff, _loaded, _blocksBusy;

        public NetworkViewModel()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += async (s, e) => await RefreshAsync();
            SortCommand = new RelayCommand(p => SortBy(p as string));
            RefreshBlocksCommand = new RelayCommand(async () => await RefreshBlocksAsync());
        }

        public ObservableCollection<AppTrafficRow> Apps { get; } = new ObservableCollection<AppTrafficRow>();
        public ObservableCollection<ConnectionRow> Connections { get; } = new ObservableCollection<ConnectionRow>();
        public ObservableCollection<PortRow> Ports { get; } = new ObservableCollection<PortRow>();
        public ObservableCollection<BlockRow> Blocks { get; } = new ObservableCollection<BlockRow>();

        public ICommand SortCommand { get; }
        public ICommand RefreshBlocksCommand { get; }

        /// <summary>La vista pide mostrar otra pestaña (por ejemplo, las conexiones de un programa).</summary>
        public event Action<string> TabRequested;

        // Textos de la cabecera
        public string DownText { get => _downText; private set => Set(ref _downText, value); }
        public string UpText { get => _upText; private set => Set(ref _upText, value); }
        public string DownTotal { get => _downTotal; private set => Set(ref _downTotal, value); }
        public string UpTotal { get => _upTotal; private set => Set(ref _upTotal, value); }
        public string SumConns { get => _sumConns; private set => Set(ref _sumConns, value); }
        public string SumPorts { get => _sumPorts; private set => Set(ref _sumPorts, value); }
        public string SumExposed { get => _sumExposed; private set => Set(ref _sumExposed, value); }
        public bool HasExposed { get => _hasExposed; private set => Set(ref _hasExposed, value); }
        public string LinkText { get => _linkText; private set => Set(ref _linkText, value); }
        public string TrafficNotice { get => _trafficNotice; private set { if (Set(ref _trafficNotice, value)) OnPropertyChanged(nameof(HasTrafficNotice)); } }
        public bool HasTrafficNotice => !string.IsNullOrEmpty(_trafficNotice);
        public bool FirewallOff { get => _firewallOff; private set => Set(ref _firewallOff, value); }

        public bool HasApps => Apps.Count > 0;
        public bool HasConnections => Connections.Count > 0;
        public bool HasPorts => Ports.Count > 0;
        public bool HasBlocks => Blocks.Count > 0;
        public bool Loaded { get => _loaded; private set => Set(ref _loaded, value); }

        public string SearchText
        {
            get => _search;
            set { if (Set(ref _search, value ?? string.Empty)) Rebuild(); }
        }

        /// <summary>Incluir las conexiones que ya se están cerrando (TIME_WAIT y parecidas), que casi siempre son ruido.</summary>
        public bool ShowClosing
        {
            get => _showClosing;
            set { if (Set(ref _showClosing, value)) Rebuild(); }
        }

        /// <summary>Mientras el ratón está sobre una lista no se reordena, para no mover la fila que se va a pulsar.</summary>
        public bool HoldOrder { get => _hold; set => _hold = value; }

        // Flechas de orden: «apps.down», «conns.process»…
        public string this[string key]
        {
            get
            {
                string[] parts = (key ?? "").Split('.');
                if (parts.Length != 2) return string.Empty;
                string sort; bool desc;
                switch (parts[0])
                {
                    case "apps": sort = _appSort; desc = _appDesc; break;
                    case "conns": sort = _connSort; desc = _connDesc; break;
                    default: sort = _portSort; desc = _portDesc; break;
                }
                return sort == parts[1] ? (desc ? "" : "") : string.Empty;
            }
        }

        private void SortBy(string spec)
        {
            string[] parts = (spec ?? "").Split('.');
            if (parts.Length != 2) return;
            switch (parts[0])
            {
                case "apps": Toggle(ref _appSort, ref _appDesc, parts[1], parts[1] != "name"); break;
                case "conns": Toggle(ref _connSort, ref _connDesc, parts[1], false); break;
                default: Toggle(ref _portSort, ref _portDesc, parts[1], false); break;
            }
            OnPropertyChanged("Item[]");
            Rebuild(true);
        }

        private static void Toggle(ref string current, ref bool desc, string column, bool defaultDesc)
        {
            if (current == column) desc = !desc;
            else { current = column; desc = defaultDesc; }
        }

        // ------------------------------------------------------------------ ciclo de vida

        /// <summary>Empieza a medir (al mostrar la página).</summary>
        public void Start()
        {
            if (_timer.IsEnabled) return;
            ResetSession();
            _timer.Start();
            _ = StartTrafficAsync();
            _ = RefreshAsync();
            _ = RefreshBlocksAsync();
        }

        /// <summary>Deja de medir y cierra la sesión de seguimiento: nada queda funcionando con la página oculta.</summary>
        public void Stop()
        {
            if (!_timer.IsEnabled) return;
            _timer.Stop();
            _ = Task.Run(() => NetTraffic.Stop());
        }

        /// <summary>Al salir de TURBiX: cerrar la sesión de seguimiento si seguía abierta.</summary>
        public static void Shutdown() => NetTraffic.Stop();

        private void ResetSession()
        {
            _lastTraffic.Clear();
            _lastTaken = default(DateTime);
            _lastAdapterRx = -1;
            _sessionRx = _sessionTx = 0;
            _downRate = _upRate = 0;
            foreach (var a in _apps.Values) { a.TotalRx = a.TotalTx = 0; a.DownRate = a.UpRate = 0; }
            _apps.Clear();
            Apps.Clear();
        }

        private async Task StartTrafficAsync()
        {
            string error = await Task.Run(() => NetTraffic.Start());
            _trafficOk = error == null;
            TrafficNotice = _trafficOk ? null : L.F("net.traffic.unavailable", string.IsNullOrEmpty(error) ? "?" : error);
            if (!_trafficOk) Logger.Warn(L.F("net.log.trafficFailed", error));
        }

        // ------------------------------------------------------------------ lectura

        private async Task RefreshAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                bool needServices = (DateTime.UtcNow - _lastServices).TotalSeconds > 15;
                bool needInfra = (DateTime.UtcNow - _lastInfra).TotalSeconds > 20;
                var known = new HashSet<string>(_info.Keys);
                bool trafficOn = _trafficOk;
                var snap = await Task.Run(() =>
                {
                    var s = new Snapshot { Taken = DateTime.UtcNow };
                    s.Endpoints = NetworkOps.Read();
                    s.Processes = ProcessOps.Take().Processes;
                    s.Traffic = trafficOn && NetTraffic.IsRunning ? NetTraffic.Snapshot() : null;
                    s.HasTotals = NetworkOps.Totals(out s.Rx, out s.Tx, out s.Link);
                    if (needServices) s.Services = ProcessOps.ServicesByPid();
                    if (needInfra) s.Infrastructure = NetworkOps.InfrastructureAddresses();

                    // Ruta, descripción e icono solo de los procesos que aparecen por primera vez.
                    var wanted = new HashSet<int>(s.Endpoints.Select(e => e.Pid));
                    if (s.Traffic != null) foreach (int pid in s.Traffic.Keys) wanted.Add(pid);
                    foreach (var p in s.Processes)
                    {
                        if (!wanted.Contains(p.Pid) || known.Contains(p.Key)) continue;
                        string path = p.Pid > 4 ? ProcessOps.ImagePath(p.Pid) : null;
                        s.Resolved[p.Key] = new ProcInfo
                        {
                            Name = p.ImageName,
                            Path = path,
                            Description = ProcessOps.Description(path),
                            Icon = IconFor(path),
                            Critical = ProcessOps.IsCritical(p),
                        };
                    }
                    return s;
                });
                if (snap.Services != null) { _services = snap.Services; _lastServices = DateTime.UtcNow; }
                if (snap.Infrastructure != null) { _infrastructure = snap.Infrastructure; _lastInfra = DateTime.UtcNow; }
                foreach (var kv in snap.Resolved) _info[kv.Key] = kv.Value;
                Apply(snap);

                if ((DateTime.UtcNow - _lastBlocks).TotalSeconds > 10) _ = RefreshBlocksAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("net.log.readError", ex.Message));
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

        private Dictionary<int, ProcessSample> _byPid = new Dictionary<int, ProcessSample>();

        private ProcInfo InfoOf(int pid)
        {
            if (_byPid.TryGetValue(pid, out var s) && _info.TryGetValue(s.Key, out var info)) return info;
            return null;
        }

        private string NameOf(int pid)
        {
            if (_byPid.TryGetValue(pid, out var s)) return s.ImageName;
            return pid == 0 ? "System Idle Process" : "PID " + pid;
        }

        /// <summary>Un programa = un ejecutable. Los «Host de servicio» se separan por proceso, cada uno es otra cosa.</summary>
        private string AppKeyOf(int pid)
        {
            string name = NameOf(pid);
            if (string.Equals(name, "svchost.exe", StringComparison.OrdinalIgnoreCase)) return "svc:" + pid;
            var info = InfoOf(pid);
            if (info != null && !string.IsNullOrEmpty(info.Path)) return info.Path.ToLowerInvariant();
            return "name:" + name.ToLowerInvariant();
        }

        private string TitleOf(int pid)
        {
            string name = NameOf(pid);
            if (string.Equals(name, "svchost.exe", StringComparison.OrdinalIgnoreCase) && _services.TryGetValue(pid, out var svc) && svc.Count > 0)
            {
                return svc.Count == 1 ? L.F("proc.serviceHost", svc[0]) : L.P("proc.serviceHostMore", svc.Count - 1, svc[0]);
            }
            var info = InfoOf(pid);
            return !string.IsNullOrEmpty(info?.Description) ? info.Description : name;
        }

        private void Apply(Snapshot d)
        {
            _byPid = d.Processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
            double elapsed = _lastTaken == default(DateTime) ? 0 : (d.Taken - _lastTaken).TotalSeconds;
            _lastTaken = d.Taken;

            // ---- total de las tarjetas de red
            if (d.HasTotals)
            {
                if (_lastAdapterRx >= 0 && elapsed > 0)
                {
                    long dRx = Math.Max(0, d.Rx - _lastAdapterRx), dTx = Math.Max(0, d.Tx - _lastAdapterTx);
                    _sessionRx += dRx;
                    _sessionTx += dTx;
                    _downRate = _downRate * 0.4 + dRx / elapsed * 0.6;
                    _upRate = _upRate * 0.4 + dTx / elapsed * 0.6;
                }
                _lastAdapterRx = d.Rx;
                _lastAdapterTx = d.Tx;
                DownText = Rate(_downRate);
                UpText = Rate(_upRate);
                DownTotal = L.F("net.sinceOpened", FileCleaner.FormatSize(_sessionRx));
                UpTotal = L.F("net.sinceOpened", FileCleaner.FormatSize(_sessionTx));
                LinkText = d.Link > 0 ? L.F("net.link", LinkSpeed(d.Link)) : string.Empty;
            }

            // ---- tráfico por programa
            var instRx = new Dictionary<string, long>();
            var instTx = new Dictionary<string, long>();
            if (d.Traffic != null)
            {
                foreach (var kv in d.Traffic)
                {
                    int pid = kv.Key;
                    _lastTraffic.TryGetValue(pid, out var last);
                    long dRx = Math.Max(0, kv.Value.Received - last.Received), dTx = Math.Max(0, kv.Value.Sent - last.Sent);
                    if (dRx == 0 && dTx == 0 && !_byPid.ContainsKey(pid)) continue;
                    var app = AppFor(pid);
                    if (app == null) continue;
                    app.TotalRx += dRx;
                    app.TotalTx += dTx;
                    instRx[app.Key] = instRx.TryGetValue(app.Key, out long a) ? a + dRx : dRx;
                    instTx[app.Key] = instTx.TryGetValue(app.Key, out long b) ? b + dTx : dTx;
                }
                _lastTraffic = d.Traffic;
            }

            // ---- procesos vivos de cada programa
            var live = new Dictionary<string, HashSet<int>>();
            foreach (var p in d.Processes)
            {
                string key = AppKeyOf(p.Pid);
                if (!live.TryGetValue(key, out var set)) live[key] = set = new HashSet<int>();
                set.Add(p.Pid);
            }

            // ---- conexiones por programa
            var connCount = new Dictionary<string, int>();
            foreach (var e in d.Endpoints)
            {
                if (e.Pid == 0 || !e.IsTcp || e.State != TcpState.Established) continue;
                string key = AppKeyOf(e.Pid);
                connCount[key] = connCount.TryGetValue(key, out int c) ? c + 1 : 1;
                if (!_apps.ContainsKey(key)) AppFor(e.Pid);
            }

            // ---- actualizar las filas de programas
            foreach (var app in _apps.Values.ToList())
            {
                live.TryGetValue(app.Key, out var pids);
                app.Running = pids != null && pids.Count > 0;
                app.Connections = connCount.TryGetValue(app.Key, out int cc) ? cc : 0;
                if (elapsed > 0 && d.Traffic != null)
                {
                    double rx = instRx.TryGetValue(app.Key, out long r) ? r / elapsed : 0;
                    double tx = instTx.TryGetValue(app.Key, out long t) ? t / elapsed : 0;
                    app.DownRate = app.DownRate * 0.4 + rx * 0.6;
                    app.UpRate = app.UpRate * 0.4 + tx * 0.6;
                    if (app.DownRate < 1) app.DownRate = 0;
                    if (app.UpRate < 1) app.UpRate = 0;
                }
                if (app.Running) { app.Pids.Clear(); foreach (int p in pids) app.Pids.Add(p); }
                FillApp(app, d.Traffic != null);
            }

            // Programas cerrados y sin tráfico: fuera. Los cerrados con tráfico se quedan para ver qué consumieron.
            foreach (var gone in _apps.Values.Where(a => !a.Running && a.TotalRx + a.TotalTx == 0 && a.Connections == 0).ToList()) _apps.Remove(gone.Key);
            if (_apps.Count > MaxApps)
                foreach (var extra in _apps.Values.OrderBy(a => a.TotalRx + a.TotalTx).Take(_apps.Count - MaxApps).ToList()) _apps.Remove(extra.Key);

            BuildConnections(d.Endpoints);
            BuildPorts(d.Endpoints);

            // ---- resumen
            int conns = d.Endpoints.Count(e => e.IsTcp && e.State == TcpState.Established);
            int open = d.Endpoints.Count(e => e.IsListening || !e.IsTcp);
            int exposedCount = _ports.Values.Count(p => p.Exposed);
            SumConns = L.P("net.sum.conns", conns);
            SumPorts = L.P("net.sum.ports", open);
            SumExposed = L.P("net.sum.exposed", exposedCount);
            HasExposed = exposedCount > 0;
            Loaded = true;
            Rebuild();
        }

        private AppTrafficRow AppFor(int pid)
        {
            string key = AppKeyOf(pid);
            if (!_apps.TryGetValue(key, out var app))
            {
                if (pid == 0) return null;
                var info = InfoOf(pid);
                app = new AppTrafficRow(this) { Key = key, Path = info?.Path };
                app.Critical = info?.Critical ?? false;
                app.Icon = info?.Icon;
                _apps[key] = app;
            }
            app.Pids.Add(pid);
            return app;
        }

        private void FillApp(AppTrafficRow app, bool haveTraffic)
        {
            // Con el programa cerrado ya no hay proceso del que leer el nombre: se conserva el último.
            if (app.Running && app.Pids.Count > 0)
            {
                int pid = app.Pids.Min();
                app.Title = TitleOf(pid);
                app.Image = NameOf(pid);
            }
            else if (app.Title == null)
            {
                app.Title = app.Key.StartsWith("name:", StringComparison.Ordinal) ? app.Key.Substring(5) : System.IO.Path.GetFileName(app.Key);
                app.Image = app.Title;
            }
            string count = L.P("proc.count.processes", Math.Max(1, app.Pids.Count));
            app.Subtitle = !app.Running
                ? L.T("net.closed")
                : string.Equals(app.Title, app.Image, StringComparison.OrdinalIgnoreCase) ? count : app.Image + "  ·  " + count;
            app.Active = app.DownRate + app.UpRate > 0;
            app.DownText = haveTraffic ? Rate(app.DownRate) : "—";
            app.UpText = haveTraffic ? Rate(app.UpRate) : "—";
            app.RxText = haveTraffic && app.TotalRx > 0 ? FileCleaner.FormatSize(app.TotalRx) : "—";
            app.TxText = haveTraffic && app.TotalTx > 0 ? FileCleaner.FormatSize(app.TotalTx) : "—";
            app.ConnText = app.Connections > 0 ? app.Connections.ToString("N0", L.Culture) : "—";
            app.IsBlocked = !string.IsNullOrEmpty(app.Path) &&
                            _blockEntries.Any(b => b.Kind == BlockKind.App && string.Equals(b.Target, app.Path, StringComparison.OrdinalIgnoreCase));
            app.CanBlock = !string.IsNullOrEmpty(app.Path) && !app.Critical && app.Running && !app.IsBlocked;
        }

        private static string Rate(double bytesPerSecond) =>
            bytesPerSecond < 1 ? "—" : L.F("net.rate", FileCleaner.FormatSize((long)bytesPerSecond));

        private static string LinkSpeed(long bitsPerSecond)
        {
            double mbps = bitsPerSecond / 1e6;
            return mbps >= 1000 ? (mbps / 1000).ToString("0.#", L.Culture) + " Gbps" : mbps.ToString("0", L.Culture) + " Mbps";
        }

        // ------------------------------------------------------------------ conexiones y puertos

        private void BuildConnections(List<NetEndpoint> endpoints)
        {
            var seen = new HashSet<string>();
            foreach (var e in endpoints)
            {
                if (!e.IsTcp || e.State == TcpState.Listen || e.Pid == 0) continue;
                if (!_showClosing && (e.State == TcpState.TimeWait || e.State == TcpState.Closed || e.State == TcpState.DeleteTcb)) continue;
                string key = e.Key;
                if (!seen.Add(key)) continue;
                if (!_conns.TryGetValue(key, out var row)) { row = new ConnectionRow(this); _conns[key] = row; }
                row.Endpoint = e;
                row.AppKey = AppKeyOf(e.Pid);
                row.Process = TitleOf(e.Pid);
                row.Detail = (e.IsV6 ? "TCP · IPv6 · " : "TCP · ") + Format(e.Local, e.LocalPort) + "  ·  " + L.F("proc.pid", e.Pid);
                row.Remote = Format(e.Remote, e.RemotePort);
                var scope = NetworkOps.ScopeOf(e.Remote);
                string service = NetworkOps.ServiceName(e.RemotePort);
                row.IsInternet = scope == NetScope.Internet;
                row.RemoteDetail = (service != null ? service + "  ·  " : string.Empty) + ScopeText(scope);
                row.CanBlockAddress = scope == NetScope.Internet || (scope == NetScope.Lan && !IsMulticast(e.Remote));
                row.State = StateText(e.State);
            }
            foreach (var gone in _conns.Keys.Where(k => !seen.Contains(k)).ToList()) _conns.Remove(gone);
        }

        private void BuildPorts(List<NetEndpoint> endpoints)
        {
            var seen = new HashSet<string>();
            foreach (var e in endpoints)
            {
                if (!(e.IsListening || !e.IsTcp)) continue;
                string key = (e.IsTcp ? "T" : "U") + "|" + e.Pid + "|" + e.Local + ":" + e.LocalPort;
                if (!seen.Add(key)) continue;
                if (!_ports.TryGetValue(key, out var row)) { row = new PortRow(this); _ports[key] = row; }
                row.Endpoint = e;
                row.Key = key;
                row.Process = e.Pid == 0 ? L.T("net.port.system") : TitleOf(e.Pid);
                string service = NetworkOps.ServiceName(e.LocalPort);
                row.Detail = (e.IsTcp ? "TCP " : "UDP ") + e.LocalPort + (service != null ? " · " + service : string.Empty) + (e.IsV6 ? " · IPv6" : string.Empty) + "  ·  " + L.F("proc.pid", e.Pid);
                var scope = NetworkOps.ScopeOf(e.Local);
                bool local = scope == NetScope.ThisPc;
                row.Exposed = !local;
                row.Exposure = local ? L.T("net.expo.local") : scope == NetScope.Any ? L.T("net.expo.all") : L.F("net.expo.addr", NetworkOps.Normalize(e.Local));
                row.Review = e.IsTcp && !local && NetworkOps.IsSensitive(e.LocalPort);
            }
            foreach (var gone in _ports.Keys.Where(k => !seen.Contains(k)).ToList()) _ports.Remove(gone);
        }

        private static string Format(IPAddress a, int port)
        {
            string ip = NetworkOps.Normalize(a);
            return a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !a.IsIPv4MappedToIPv6 ? "[" + ip + "]:" + port : ip + ":" + port;
        }

        private static bool IsMulticast(IPAddress a)
        {
            if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
            var b = a.GetAddressBytes();
            return a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? b[0] >= 224 : b[0] == 0xFF;
        }

        private static string ScopeText(NetScope scope)
        {
            switch (scope)
            {
                case NetScope.ThisPc: return L.T("net.scope.pc");
                case NetScope.Lan: return L.T("net.scope.lan");
                default: return L.T("net.scope.internet");
            }
        }

        private static string StateText(TcpState s)
        {
            switch (s)
            {
                case TcpState.Established: return L.T("net.state.established");
                case TcpState.SynSent: return L.T("net.state.synSent");
                case TcpState.SynReceived: return L.T("net.state.synReceived");
                case TcpState.FinWait1: return L.T("net.state.finWait1");
                case TcpState.FinWait2: return L.T("net.state.finWait2");
                case TcpState.CloseWait: return L.T("net.state.closeWait");
                case TcpState.Closing: return L.T("net.state.closing");
                case TcpState.LastAck: return L.T("net.state.lastAck");
                case TcpState.TimeWait: return L.T("net.state.timeWait");
                default: return L.T("net.state.closed");
            }
        }

        // ------------------------------------------------------------------ filtrar y ordenar

        private bool Matches(params string[] fields)
        {
            if (_search.Length == 0) return true;
            string q = _search.Trim();
            return fields.Any(f => f != null && f.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private void Rebuild(bool force = false)
        {
            bool hold = _hold && !force;

            var apps = _apps.Values.Where(a => Matches(a.Title, a.Subtitle, a.Path)).ToList();
            Sync(Apps, OrderApps(apps), hold);

            var conns = _conns.Values.Where(c => Matches(c.Process, c.Detail, c.Remote, c.RemoteDetail, c.State)).ToList();
            Sync(Connections, OrderConns(conns), hold);

            var ports = _ports.Values.Where(p => Matches(p.Process, p.Detail, p.Exposure)).ToList();
            Sync(Ports, OrderPorts(ports), hold);

            OnPropertyChanged(nameof(HasApps));
            OnPropertyChanged(nameof(HasConnections));
            OnPropertyChanged(nameof(HasPorts));
        }

        private IList<AppTrafficRow> OrderApps(List<AppTrafficRow> list)
        {
            Func<AppTrafficRow, IComparable> key;
            switch (_appSort)
            {
                case "name": key = a => (a.Title ?? "").ToLowerInvariant(); break;
                case "up": key = a => a.UpRate; break;
                case "rx": key = a => a.TotalRx; break;
                case "tx": key = a => a.TotalTx; break;
                case "conns": key = a => a.Connections; break;
                default: key = a => a.DownRate; break;
            }
            var ordered = _appDesc ? list.OrderByDescending(key) : list.OrderBy(key);
            // Desempate estable: lo que más movió en total, y después el nombre.
            return ordered.ThenByDescending(a => a.TotalRx + a.TotalTx).ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private IList<ConnectionRow> OrderConns(List<ConnectionRow> list)
        {
            Func<ConnectionRow, IComparable> key;
            switch (_connSort)
            {
                case "remote": key = c => c.Remote ?? ""; break;
                case "state": key = c => c.State ?? ""; break;
                default: key = c => (c.Process ?? "").ToLowerInvariant(); break;
            }
            var ordered = _connDesc ? list.OrderByDescending(key) : list.OrderBy(key);
            return ordered.ThenBy(c => c.Remote, StringComparer.Ordinal).ToList();
        }

        private IList<PortRow> OrderPorts(List<PortRow> list)
        {
            Func<PortRow, IComparable> key;
            switch (_portSort)
            {
                case "process": key = p => (p.Process ?? "").ToLowerInvariant(); break;
                case "exposure": key = p => p.Exposed ? 0 : 1; break;
                default: key = p => p.Endpoint.LocalPort; break;
            }
            var ordered = _portDesc ? list.OrderByDescending(key) : list.OrderBy(key);
            return ordered.ThenBy(p => p.Endpoint.LocalPort).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
        }

        /// <summary>Deja la lista como se pidió moviendo filas, sin vaciarla: así no salta el scroll ni se pierde el ratón.</summary>
        private static void Sync<T>(ObservableCollection<T> target, IList<T> desired, bool hold)
        {
            var wanted = new HashSet<T>(desired);
            for (int i = target.Count - 1; i >= 0; i--)
                if (!wanted.Contains(target[i])) target.RemoveAt(i);
            if (hold)
            {
                var present = new HashSet<T>(target);
                foreach (var item in desired) if (present.Add(item)) target.Add(item);
                return;
            }
            for (int i = 0; i < desired.Count; i++)
            {
                if (i < target.Count && ReferenceEquals(target[i], desired[i])) continue;
                int at = target.IndexOf(desired[i]);
                if (at >= 0) target.Move(at, i);
                else target.Insert(i, desired[i]);
            }
        }

        // ------------------------------------------------------------------ acciones

        internal void ShowConnectionsOf(AppTrafficRow app)
        {
            SearchText = app.Title;
            TabRequested?.Invoke("connections");
        }

        internal void CloseConnection(ConnectionRow row)
        {
            var e = row.Endpoint;
            if (e == null || !e.CanClose) return;
            if (!Dialogs.Confirm(L.T("net.close.title"), L.F("net.close.text", row.Process, row.Remote), L.T("net.close.confirm"))) return;
            string error = NetworkOps.Close(e);
            if (error == null) Logger.Info(L.F("net.log.closed", row.Process, row.Remote));
            else
            {
                Logger.Warn(L.F("net.log.closeFailed", row.Process, row.Remote, error));
                Dialogs.Show(L.T("net.close.failed.title"), L.F("net.close.failed.text", row.Remote, error), DialogKind.Warning);
            }
            _ = RefreshAsync();
        }

        internal async void BlockApp(AppTrafficRow app)
        {
            if (!app.CanBlock || string.IsNullOrEmpty(app.Path)) return;
            if (!Dialogs.Confirm(L.F("net.blockApp.title", app.Title), L.F("net.blockApp.text", app.Title, app.Path), L.T("net.blockApp.confirm"))) return;
            string path = app.Path, title = app.Title;
            var open = _conns.Values.Where(c => c.AppKey == app.Key && c.Endpoint != null && c.Endpoint.CanClose).Select(c => c.Endpoint).ToList();
            string error = await Task.Run(() =>
            {
                string err = FirewallOps.BlockApp(path, L.F("net.rule.app", title));
                if (err == null) foreach (var e in open) NetworkOps.Close(e);   // lo ya conectado también se corta
                return err;
            });
            if (error == null) Logger.Info(L.F("net.log.blockedApp", title, path));
            else
            {
                Logger.Warn(L.F("net.log.blockFailed", title, error));
                Dialogs.Show(L.T("net.block.failed.title"), L.F("net.block.failed.text", error), DialogKind.Warning);
            }
            await RefreshBlocksAsync();
            _ = RefreshAsync();
        }

        internal async void BlockAddress(ConnectionRow row)
        {
            var e = row.Endpoint;
            if (e == null || !row.CanBlockAddress) return;
            string ip = NetworkOps.Normalize(e.Remote);
            // Bloquear el router o el servidor DNS cortaría la conexión a Internet de todo el equipo.
            if (_infrastructure.Contains(ip))
            {
                Dialogs.Show(L.T("net.blockIp.infra.title"), L.F("net.blockIp.infra.text", ip), DialogKind.Warning);
                return;
            }
            if (!Dialogs.Confirm(L.F("net.blockIp.title", ip), L.F("net.blockIp.text", ip, row.Process), L.T("net.blockIp.confirm"))) return;
            var open = _conns.Values.Where(c => c.Endpoint != null && c.Endpoint.CanClose && NetworkOps.Normalize(c.Endpoint.Remote) == ip).Select(c => c.Endpoint).ToList();
            string error = await Task.Run(() =>
            {
                string err = FirewallOps.BlockAddress(ip, L.F("net.rule.address", ip));
                if (err == null) foreach (var c in open) NetworkOps.Close(c);
                return err;
            });
            if (error == null) Logger.Info(L.F("net.log.blockedAddress", ip));
            else
            {
                Logger.Warn(L.F("net.log.blockFailed", ip, error));
                Dialogs.Show(L.T("net.block.failed.title"), L.F("net.block.failed.text", error), DialogKind.Warning);
            }
            await RefreshBlocksAsync();
            _ = RefreshAsync();
        }

        internal async void BlockPort(PortRow row)
        {
            var e = row.Endpoint;
            if (e == null || !row.Exposed) return;
            string proto = e.IsTcp ? "TCP" : "UDP";
            if (!Dialogs.Confirm(L.F("net.blockPort.title", proto, e.LocalPort), L.F("net.blockPort.text", proto, e.LocalPort, row.Process), L.T("net.blockPort.confirm"))) return;
            int port = e.LocalPort;
            bool tcp = e.IsTcp;
            string error = await Task.Run(() => FirewallOps.BlockPort(port, tcp, L.F("net.rule.port", proto, port)));
            if (error == null) Logger.Info(L.F("net.log.blockedPort", proto, port));
            else
            {
                Logger.Warn(L.F("net.log.blockFailed", proto + " " + port, error));
                Dialogs.Show(L.T("net.block.failed.title"), L.F("net.block.failed.text", error), DialogKind.Warning);
            }
            await RefreshBlocksAsync();
        }

        internal async void RemoveBlock(BlockRow row)
        {
            var entry = row.Entry;
            string error = await Task.Run(() => FirewallOps.Remove(entry));
            if (error == null) Logger.Info(L.F("net.log.unblocked", row.Title));
            else
            {
                Logger.Warn(L.F("net.log.unblockFailed", row.Title, error));
                Dialogs.Show(L.T("net.unblock.failed.title"), L.F("net.unblock.failed.text", error), DialogKind.Warning);
            }
            await RefreshBlocksAsync();
            _ = RefreshAsync();
        }

        // ------------------------------------------------------------------ bloqueos

        public async Task RefreshBlocksAsync()
        {
            if (_blocksBusy) return;
            _blocksBusy = true;
            try
            {
                var result = await Task.Run(() =>
                {
                    var list = FirewallOps.List(out string error);
                    return new { list, error, active = FirewallOps.IsActive() };
                });
                _lastBlocks = DateTime.UtcNow;
                _blockEntries = result.list;
                FirewallOff = !result.active;

                Blocks.Clear();
                foreach (var b in result.list)
                {
                    var row = new BlockRow(this, b);
                    switch (b.Kind)
                    {
                        case BlockKind.App:
                            row.Glyph = "";
                            row.Title = System.IO.Path.GetFileName(b.Target);
                            row.Subtitle = b.Target;
                            break;
                        case BlockKind.Address:
                            row.Glyph = "";
                            row.Title = b.Target;
                            row.Subtitle = L.T("net.kind.address");
                            break;
                        default:
                            row.Glyph = "";
                            row.Title = b.Target;
                            row.Subtitle = L.T("net.kind.port");
                            break;
                    }
                    row.Directions = b.Inbound && b.Outbound ? L.T("net.dir.both") : b.Inbound ? L.T("net.dir.in") : L.T("net.dir.out");
                    Blocks.Add(row);
                }
                OnPropertyChanged(nameof(HasBlocks));
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("net.log.readError", ex.Message));
            }
            finally
            {
                _blocksBusy = false;
            }
        }
    }
}
