using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Turbix.Cleanup;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Tweaks;
using Turbix.Views;

namespace Turbix.ViewModels
{
    public sealed partial class MainViewModel : ObservableObject
    {
        private const int MaxLogLines = 4000;

        public MainViewModel()
        {
            // --- Ajustes de privacidad agrupados por categoría
            foreach (var cat in TweakCatalog.Categories)
            {
                var group = new TweakGroupViewModel { Name = cat.Name, Glyph = cat.Glyph, Help = cat.Help };
                foreach (var t in TweakCatalog.All.Where(t => t.Category == cat.Name))
                {
                    var vm = new TweakItemViewModel(t);
                    vm.PropertyChanged += OnTweakChanged;
                    group.Items.Add(vm);
                    Tweaks.Add(vm);
                }
                if (group.Items.Count > 0) TweakGroups.Add(group);
            }

            // --- Limpieza
            foreach (var item in CleanupCatalog.All)
            {
                var vm = new CleanupItemViewModel(item);
                vm.PropertyChanged += OnCleanupChanged;
                CleanupItems.Add(vm);
            }

            // --- Comandos
            NavigateCommand = new RelayCommand(p => NavigateRequested?.Invoke(p as string));
            SelectTweakPresetCommand = new RelayCommand(SelectTweakPreset, _ => !IsBusy);
            SelectCleanupPresetCommand = new RelayCommand(SelectCleanupPreset, _ => !IsBusy);
            RefreshStatesCommand = new RelayCommand(async () => await RefreshStatesAsync(), () => !IsBusy && !IsLoadingStates);
            ApplyTweaksCommand = new RelayCommand(async () => await ApplyTweaksAsync(), () => !IsBusy && SelectedTweakCount > 0);
            AnalyzeCommand = new RelayCommand(async () => await AnalyzeAsync(), () => !IsBusy);
            CleanCommand = new RelayCommand(async () => await CleanAsync(), () => !IsBusy && SelectedCleanupCount > 0);
            QuickOptimizeCommand = new RelayCommand(async () => await QuickOptimizeAsync(), () => !IsBusy);
            RevertCommand = new RelayCommand(async p => await RevertAsync(p as BackupViewModel), p => !IsBusy && p is BackupViewModel b && !b.IsReverted);
            RefreshBackupsCommand = new RelayCommand(LoadBackups);
            OpenBackupsCommand = new RelayCommand(() => OpenFolder(AppPaths.Backups));
            OpenLogsCommand = new RelayCommand(() => OpenFolder(AppPaths.Logs));
            OpenSystemRestoreCommand = new RelayCommand(() => Launch(Path.Combine(Environment.SystemDirectory, "rstrui.exe")));
            ClearLogCommand = new RelayCommand(() => LogLines.Clear());
            CopyLogCommand = new RelayCommand(() =>
            {
                try { Clipboard.SetText(string.Join(Environment.NewLine, LogLines.Select(l => l.IsDayHeader ? "--- " + l.DayText + " ---" : $"{l.TimeText} {l.LevelText,-5} {l.Message}"))); }
                catch { /* portapapeles ocupado */ }
            });

            InitPerformance();
            InitTools();
            InitHealth();
            InitApps();
            InitSettings();
            LoadLogHistory();
            Logger.Logged += OnLogged;
        }

        // ================================================================== estado general

        public event Action<string> NavigateRequested;

        public ObservableCollection<TweakGroupViewModel> TweakGroups { get; } = new ObservableCollection<TweakGroupViewModel>();
        public List<TweakItemViewModel> Tweaks { get; } = new List<TweakItemViewModel>();
        public ObservableCollection<CleanupItemViewModel> CleanupItems { get; } = new ObservableCollection<CleanupItemViewModel>();
        public ObservableCollection<BackupViewModel> Backups { get; } = new ObservableCollection<BackupViewModel>();
        public ObservableCollection<LogEntry> LogLines { get; } = new ObservableCollection<LogEntry>();
        public ProcessesViewModel Processes { get; } = new ProcessesViewModel();
        public NetworkViewModel Network { get; } = new NetworkViewModel();
        public List<ChangelogRelease> Releases { get; } = Changelog.Load();

        public ICommand NavigateCommand { get; }
        public ICommand SelectTweakPresetCommand { get; }
        public ICommand SelectCleanupPresetCommand { get; }
        public ICommand RefreshStatesCommand { get; }
        public ICommand ApplyTweaksCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand QuickOptimizeCommand { get; }
        public ICommand RevertCommand { get; }
        public ICommand RefreshBackupsCommand { get; }
        public ICommand OpenBackupsCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand OpenSystemRestoreCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CopyLogCommand { get; }

        public string Version => "v" + AppInfo.Version;
        public bool IsAdmin { get; } = SystemInfo.IsAdministrator();
        public string AdminText => IsAdmin ? L.T("main.admin.on") : L.T("main.admin.off");
        public string ChangelogSubtitle => L.F("whatsnew.subtitle", Version);

        private string _greeting = L.T("main.greeting.hello");
        public string Greeting { get => _greeting; private set => Set(ref _greeting, value); }

        // ================================================================== tarea en curso

        private bool _isBusy;
        private string _busyTitle;
        private string _busyStatus;
        private string _busyDetail;
        private double _busyProgress;

        public bool IsBusy
        {
            get => _isBusy;
            private set { if (Set(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public string BusyTitle { get => _busyTitle; private set => Set(ref _busyTitle, value); }
        public string BusyStatus { get => _busyStatus; private set => Set(ref _busyStatus, value); }
        public string BusyDetail { get => _busyDetail; private set => Set(ref _busyDetail, value); }
        public double BusyProgress { get => _busyProgress; private set => Set(ref _busyProgress, value); }

        private async Task<T> RunBusyAsync<T>(string title, Func<IProgress<ProgressInfo>, T> work)
        {
            IsBusy = true;
            BusyTitle = title;
            BusyStatus = L.T("main.busy.preparing");
            BusyDetail = string.Empty;
            BusyProgress = 0;
            var progress = new Progress<ProgressInfo>(p =>
            {
                BusyProgress = p.Percent;
                if (!string.IsNullOrEmpty(p.Status)) BusyStatus = p.Status;
            });
            try
            {
                return await Task.Run(() => work(progress));
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ================================================================== registro

        /// <summary>Muestra lo que Turbix hizo en sesiones anteriores, con un separador por día.</summary>
        private void LoadLogHistory()
        {
            foreach (var entry in Logger.LoadHistory(MaxLogLines)) AddLogLine(entry);
        }

        private DateTime _lastLogDay;

        private void AddLogLine(LogEntry entry)
        {
            if (LogLines.Count == 0 || _lastLogDay != entry.Time.Date)
            {
                bool dayChanged = LogLines.Count > 0;
                LogLines.Add(LogEntry.DayHeader(entry.Time));
                // Los encabezados anteriores decían «Hoy»: que se vuelvan a calcular («Ayer», la fecha…).
                if (dayChanged) System.Windows.Data.CollectionViewSource.GetDefaultView(LogLines)?.Refresh();
            }
            _lastLogDay = entry.Time.Date;
            LogLines.Add(entry);
            while (LogLines.Count > MaxLogLines) LogLines.RemoveAt(0);
            while (LogLines.Count > 1 && LogLines[0].IsDayHeader && LogLines[1].IsDayHeader) LogLines.RemoveAt(0);
        }

        private void OnLogged(LogEntry entry)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                AddLogLine(entry);
                if (IsBusy) BusyDetail = entry.Message.Trim();
            }));
        }

        // ================================================================== sistema

        private string _osCaption = "…", _osVersion = "…", _cpu = "…", _gpu = "…", _ram = "…", _uptime = "…";
        private string _diskFree = "…", _diskDetail = "…", _editionNote;
        private double _ramPercent, _diskPercent;
        private OsInfo _os;

        public string OsCaption { get => _osCaption; private set => Set(ref _osCaption, value); }
        public string OsVersion { get => _osVersion; private set => Set(ref _osVersion, value); }
        public string CpuName { get => _cpu; private set => Set(ref _cpu, value); }
        public string GpuName { get => _gpu; private set => Set(ref _gpu, value); }
        public string RamText { get => _ram; private set => Set(ref _ram, value); }
        public double RamPercent { get => _ramPercent; private set => Set(ref _ramPercent, value); }
        public string UptimeText { get => _uptime; private set => Set(ref _uptime, value); }
        public string ComputerName => Environment.MachineName;
        public string UserName => Environment.UserName;
        public string DiskFreeText { get => _diskFree; private set => Set(ref _diskFree, value); }
        public string DiskDetail { get => _diskDetail; private set => Set(ref _diskDetail, value); }
        public double DiskPercent { get => _diskPercent; private set => Set(ref _diskPercent, value); }
        public string EditionNote { get => _editionNote; private set => Set(ref _editionNote, value); }
        public bool HasEditionNote => !string.IsNullOrEmpty(_editionNote);

        public async Task InitializeAsync()
        {
            int hour = DateTime.Now.Hour;
            string salute = hour < 12 ? "main.greeting.morning" : hour < 20 ? "main.greeting.afternoon" : "main.greeting.evening";
            Greeting = L.F(salute, Environment.UserName);

            Logger.Info(L.F("main.log.started", Version));
            LoadBackups();
            await LoadSystemInfoAsync();
            await RefreshStatesAsync();
            await LoadStartupAsync();
            await RefreshDiagnosticsAsync();
        }

        private async Task LoadSystemInfoAsync()
        {
            var data = await Task.Run(() => new
            {
                Os = SystemInfo.GetOs(),
                Cpu = SystemInfo.GetCpuName(),
                Gpu = SystemInfo.GetGpuName()
            });
            _os = data.Os;
            OsCaption = data.Os.Caption;
            OsVersion = data.Os.VersionText;
            CpuName = data.Cpu;
            GpuName = data.Gpu;
            Logger.Info(L.F("main.log.system", data.Os.Caption, data.Os.VersionText));
            if (!data.Os.SupportsTelemetryOff)
            {
                EditionNote = L.F("main.editionNote", data.Os.EditionName);
                OnPropertyChanged(nameof(HasEditionNote));
            }
            RefreshLiveInfo();
        }

        /// <summary>Datos que cambian (memoria, disco, tiempo encendido).</summary>
        public void RefreshLiveInfo()
        {
            SystemInfo.GetMemory(out ulong total, out ulong avail);
            if (total > 0)
            {
                ulong used = total - avail;
                RamPercent = 100.0 * used / total;
                RamText = L.F("main.ram", FileCleaner.FormatSize((long)used), FileCleaner.FormatSize((long)total));
            }
            RefreshMemoryDetails();
            UptimeText = SystemInfo.FormatUptime(SystemInfo.Uptime);
            try
            {
                var d = SystemInfo.SystemDrive;
                long used = d.TotalSize - d.AvailableFreeSpace;
                DiskPercent = 100.0 * used / d.TotalSize;
                DiskFreeText = FileCleaner.FormatSize(d.AvailableFreeSpace);
                DiskDetail = L.F("main.disk", d.Name.TrimEnd('\\'), FileCleaner.FormatSize(used), FileCleaner.FormatSize(d.TotalSize));
            }
            catch
            {
                DiskFreeText = "—";
                DiskDetail = string.Empty;
            }
        }

        // ================================================================== privacidad

        private bool _isLoadingStates;
        private double _privacyScore;
        private string _privacySummary = L.T("main.checking");

        public bool IsLoadingStates
        {
            get => _isLoadingStates;
            private set { if (Set(ref _isLoadingStates, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public double PrivacyScore { get => _privacyScore; private set => Set(ref _privacyScore, value); }
        public string PrivacyScoreText => L.F("unit.percent", Math.Round(_privacyScore));
        public string PrivacySummary { get => _privacySummary; private set => Set(ref _privacySummary, value); }

        public string PrivacyLevel
        {
            get
            {
                if (_privacyScore >= 85) return L.T("main.privacy.level.excellent");
                if (_privacyScore >= 60) return L.T("main.privacy.level.good");
                if (_privacyScore >= 30) return L.T("main.privacy.level.fair");
                return L.T("main.privacy.level.low");
            }
        }

        public int SelectedTweakCount => Tweaks.Count(t => t.IsSelected);

        public string ApplyButtonText => L.P("main.privacy.applyButton", SelectedTweakCount);

        private bool _createRestorePoint = true;
        public bool CreateRestorePoint { get => _createRestorePoint; set => Set(ref _createRestorePoint, value); }

        private void OnTweakChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TweakItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedTweakCount));
                OnPropertyChanged(nameof(ApplyButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectTweakPreset(object preset)
        {
            string p = preset as string;
            foreach (var t in Tweaks)
                t.IsSelected = p == "max" || (p == "recommended" && t.Model.Recommended);
        }

        public async Task RefreshStatesAsync()
        {
            IsLoadingStates = true;
            foreach (var t in Tweaks) t.State = TweakState.Unknown;
            try
            {
                var states = await Task.Run(() => TweakEngine.GetStates(Tweaks.Select(t => t.Model).ToList()));
                foreach (var t in Tweaks)
                    t.State = states.TryGetValue(t.Model.Id, out var s) ? s : TweakState.Unknown;
                UpdateScore();
                await RefreshGamingStatesAsync();
                await RefreshWindowsStatesAsync();
            }
            finally
            {
                IsLoadingStates = false;
            }
        }

        private void UpdateScore()
        {
            var applicable = Tweaks.Where(t => t.State != TweakState.NotApplicable && t.State != TweakState.Unknown).ToList();
            int applied = applicable.Count(t => t.State == TweakState.Applied);
            int partial = applicable.Count(t => t.State == TweakState.Partial);
            PrivacyScore = applicable.Count == 0 ? 0 : 100.0 * (applied + 0.5 * partial) / applicable.Count;
            PrivacySummary = L.P("main.privacy.summary", applicable.Count, applied);
            OnPropertyChanged(nameof(PrivacyScoreText));
            OnPropertyChanged(nameof(PrivacyLevel));
        }

        private async Task ApplyTweaksAsync()
        {
            var selected = Tweaks.Where(t => t.IsSelected).Select(t => t.Model).ToList();
            if (selected.Count == 0) return;
            int advanced = selected.Count(t => !t.Recommended);
            string msg = L.P("main.apply.confirm.text", selected.Count);
            if (advanced > 0)
                msg += "\n\n" + L.P("main.apply.confirm.advanced", advanced);
            if (!Dialogs.Confirm(L.T("main.apply.confirm.title"), msg, L.T("main.apply.confirm.button"))) return;

            bool rp = CreateRestorePoint;
            ApplyResult result;
            try
            {
                result = await RunBusyAsync(L.T("main.apply.busy"), p => TweakEngine.Apply(selected, rp, p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("main.error.title"), ex.Message, DialogKind.Error);
                return;
            }

            LoadBackups();
            await RefreshStatesAsync();
            string summary = result.Changed == 0
                ? L.T("main.apply.nothing")
                : (result.Errors > 0 ? L.P("main.apply.changedErrors", result.Changed, result.Errors) : L.P("main.apply.changed", result.Changed)) +
                  "\n\n" + L.T("main.restartNeeded");
            summary += RestorePointNote(rp, result);
            Dialogs.Show(result.Errors > 0 ? L.T("main.apply.doneWarn.title") : L.T("main.apply.done.title"), summary,
                result.Errors > 0 ? DialogKind.Warning : DialogKind.Success);
        }

        // ================================================================== limpieza

        private bool _aggressive;
        private bool _hasAnalysis;
        private string _lastAnalysisText = L.T("main.cleanup.notAnalyzed");
        private string _recoverableText = "—";

        public bool AggressiveMode { get => _aggressive; set => Set(ref _aggressive, value); }
        public bool HasAnalysis { get => _hasAnalysis; private set => Set(ref _hasAnalysis, value); }
        public string LastAnalysisText { get => _lastAnalysisText; private set => Set(ref _lastAnalysisText, value); }

        /// <summary>Total recuperable de todas las categorías analizadas (panel de inicio).</summary>
        public string RecoverableText { get => _recoverableText; private set => Set(ref _recoverableText, value); }

        public int SelectedCleanupCount => CleanupItems.Count(c => c.IsSelected);

        public string SelectedRecoverableText
        {
            get
            {
                if (!HasAnalysis) return "—";
                long sum = CleanupItems.Where(c => c.IsSelected && c.Bytes.HasValue).Sum(c => c.Bytes.Value);
                return FileCleaner.FormatSize(sum);
            }
        }

        public string CleanButtonText => L.P("main.cleanup.button", SelectedCleanupCount);

        private void OnCleanupChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CleanupItemViewModel.IsSelected) || e.PropertyName == nameof(CleanupItemViewModel.Bytes))
            {
                OnPropertyChanged(nameof(SelectedCleanupCount));
                OnPropertyChanged(nameof(SelectedRecoverableText));
                OnPropertyChanged(nameof(CleanButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectCleanupPreset(object preset)
        {
            string p = preset as string;
            foreach (var c in CleanupItems)
                c.IsSelected = p == "all" || (p == "recommended" && c.Model.Recommended);
        }

        public async Task AnalyzeAsync()
        {
            var items = CleanupItems.Select(c => c.Model).ToList();
            bool aggressive = AggressiveMode;
            List<CleanupResult> results;
            try
            {
                results = await RunBusyAsync(L.T("main.cleanup.busyAnalyze"), p => CleanupEngine.Measure(items, aggressive, p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return;
            }
            foreach (var r in results)
                CleanupItems.FirstOrDefault(c => c.Model.Id == r.Id)?.SetMeasured(r.Bytes);

            HasAnalysis = true;
            long total = results.Where(r => r.Bytes.HasValue).Sum(r => r.Bytes.Value);
            RecoverableText = FileCleaner.FormatSize(total);
            LastAnalysisText = L.F("main.cleanup.analyzedAt", DateTime.Now.ToString("t", L.Culture));
            OnPropertyChanged(nameof(SelectedRecoverableText));
        }

        private async Task CleanAsync()
        {
            var selected = CleanupItems.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0) return;
            var warnings = selected.Where(c => c.HasWarning).Select(c => "•  " + c.Name + ": " + c.Warning).ToList();
            string msg = L.P("main.cleanup.confirm.text", selected.Count);
            if (warnings.Count > 0) msg += "\n\n" + L.T("main.warnings") + "\n" + string.Join("\n", warnings);
            if (!Dialogs.Confirm(L.T("main.cleanup.confirm.title"), msg, L.T("main.cleanup.confirm.button"))) return;

            var items = selected.Select(c => c.Model).ToList();
            bool aggressive = AggressiveMode;
            List<CleanupResult> results;
            try
            {
                results = await RunBusyAsync(L.T("main.cleanup.busyClean"), p => CleanupEngine.Clean(items, aggressive, p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("main.error.title"), ex.Message, DialogKind.Error);
                return;
            }
            ShowCleanResults(results);
        }

        private void ShowCleanResults(List<CleanupResult> results)
        {
            foreach (var r in results)
                CleanupItems.FirstOrDefault(c => c.Model.Id == r.Id)?.SetFreed(r.Bytes);
            OnPropertyChanged(nameof(SelectedRecoverableText));
            RecoverableText = "—";
            LastAnalysisText = L.F("main.cleanup.cleanedAt", DateTime.Now.ToString("t", L.Culture));
            RefreshLiveInfo();

            long freed = results.Where(r => r.Bytes.HasValue).Sum(r => r.Bytes.Value);
            var skipped = results.SelectMany(r => r.SkippedApps).Distinct().ToList();
            string msg = L.F("main.cleanup.freed", FileCleaner.FormatSize(freed));
            if (skipped.Count > 0)
                msg += "\n\n" + L.F("main.cleanup.skipped", string.Join(", ", skipped));
            if (results.Sum(r => r.Failed) > 0)
                msg += "\n\n" + L.T("main.cleanup.inUse");
            Dialogs.Show(L.T("main.cleanup.done.title"), msg, DialogKind.Success);
        }

        // ================================================================== optimización rápida

        private async Task QuickOptimizeAsync()
        {
            var tweaks = Tweaks.Where(t => t.Model.Recommended).Select(t => t.Model).ToList();
            var cleanItems = CleanupItems.Where(c => c.Model.Recommended).ToList();
            var items = cleanItems.Select(c => c.Model).ToList();
            var warnings = cleanItems.Where(c => c.HasWarning).Select(c => "•  " + c.Name + ": " + c.Warning).ToList();
            string msg = L.P("main.quick.confirm.tweaks", tweaks.Count) + " " + L.P("main.quick.confirm.clean", items.Count) +
                         "\n\n" + L.T("main.quick.confirm.safe");
            if (warnings.Count > 0) msg += "\n\n" + L.T("main.warnings") + "\n" + string.Join("\n", warnings);
            if (!Dialogs.Confirm(L.T("main.quick.confirm.title"), msg, L.T("main.quick.confirm.button"))) return;

            bool rp = CreateRestorePoint;
            ApplyResult applied = null;
            List<CleanupResult> cleaned = null;
            try
            {
                await RunBusyAsync(L.T("main.quick.busy"), p =>
                {
                    var half1 = new Progress<ProgressInfo>(x => ((IProgress<ProgressInfo>)p).Report(new ProgressInfo(x.Percent * 0.5, x.Status)));
                    var half2 = new Progress<ProgressInfo>(x => ((IProgress<ProgressInfo>)p).Report(new ProgressInfo(50 + x.Percent * 0.5, x.Status)));
                    applied = TweakEngine.Apply(tweaks, rp, half1);
                    cleaned = CleanupEngine.Clean(items, false, half2);
                    return true;
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("main.error.title"), ex.Message, DialogKind.Error);
                return;
            }

            LoadBackups();
            await RefreshStatesAsync();
            foreach (var r in cleaned) CleanupItems.FirstOrDefault(c => c.Model.Id == r.Id)?.SetFreed(r.Bytes);
            RefreshLiveInfo();
            RecoverableText = "—";
            LastAnalysisText = L.F("main.cleanup.cleanedAt", DateTime.Now.ToString("t", L.Culture));
            long freed = cleaned.Where(r => r.Bytes.HasValue).Sum(r => r.Bytes.Value);
            var skipped = cleaned.SelectMany(r => r.SkippedApps).Distinct().ToList();

            string summary = (applied.Errors > 0
                                 ? L.P("main.quick.summary.privacyErrors", applied.Changed, applied.Errors)
                                 : L.P("main.quick.summary.privacy", applied.Changed)) +
                             "\n" + L.F("main.quick.summary.freed", FileCleaner.FormatSize(freed));
            if (skipped.Count > 0)
                summary += "\n\n" + L.F("main.quick.summary.skipped", string.Join(", ", skipped));
            summary += "\n\n" + L.T("main.restartNeeded");
            summary += RestorePointNote(rp, applied);
            Dialogs.Show(applied.Errors > 0 ? L.T("main.quick.doneWarn.title") : L.T("main.quick.done.title"), summary,
                applied.Errors > 0 ? DialogKind.Warning : DialogKind.Success);
            await RefreshDiagnosticsAsync();
        }

        /// <summary>Texto extra para el resultado cuando se pidió un punto de restauración y Windows no lo creó.</summary>
        private static string RestorePointNote(bool requested, ApplyResult result) =>
            requested && result != null && !result.RestorePointCreated
                ? "\n\n" + L.T("main.restorePointNote")
                : string.Empty;

        // ================================================================== restaurar

        public bool HasBackups => Backups.Count > 0;

        private void LoadBackups()
        {
            Backups.Clear();
            foreach (var s in BackupStore.List()) Backups.Add(new BackupViewModel(s));
            OnPropertyChanged(nameof(HasBackups));
        }

        private async Task RevertAsync(BackupViewModel backup)
        {
            if (backup == null) return;
            string msg = L.P("main.revert.confirm.text", backup.Session.Entries.Count, backup.DateText);
            if (!Dialogs.Confirm(L.T("main.revert.confirm.title"), msg, L.T("restore.revert"))) return;
            int[] result;
            try
            {
                result = await RunBusyAsync(L.T("main.revert.busy"), p => BackupStore.Revert(backup.Session, p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("main.error.title"), ex.Message, DialogKind.Error);
                return;
            }
            LoadBackups();
            await RefreshStatesAsync();
            if (result[1] == 0)
                Dialogs.Show(L.T("main.revert.done.title"), L.T("main.revert.done.text"), DialogKind.Success);
            else
                Dialogs.Show(L.T("main.revert.partial.title"), L.P("main.revert.partial.text", result[0], result[1]), DialogKind.Warning);
        }

        // ================================================================== utilidades

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
        }

        private static void Launch(string file)
        {
            try { Process.Start(file); }
            catch (Exception ex) { Logger.Warn(ex.Message); }
        }
    }
}
