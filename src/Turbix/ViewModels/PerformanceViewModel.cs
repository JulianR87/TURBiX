using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Turbix.Tweaks;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Un programa que se inicia con Windows, con su interruptor.</summary>
    public sealed class StartupItemViewModel : ObservableObject
    {
        private readonly Action<StartupItemViewModel, bool> _toggle;
        private bool _isEnabled;

        public StartupItemViewModel(StartupEntry model, ImageSource icon, Action<StartupItemViewModel, bool> toggle)
        {
            Model = model;
            Icon = icon;
            _isEnabled = model.Enabled;
            _toggle = toggle;
        }

        public StartupEntry Model { get; }
        public ImageSource Icon { get; }
        public bool HasIcon => Icon != null;
        /// <summary>Nombre sin los códigos largos que añaden algunos programas («…AutoLaunch_2F10E451…»).</summary>
        public string Name => System.Text.RegularExpressions.Regex.Replace(Model.Name, "_[0-9A-Fa-f]{16,}$", "");
        public bool IsProtected => Model.IsProtected;
        public bool CanToggle => !Model.IsProtected;

        public string Detail
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Model.Description) &&
                    !string.Equals(Model.Description, Model.Name, StringComparison.OrdinalIgnoreCase)) parts.Add(Model.Description);
                if (!string.IsNullOrEmpty(Model.Publisher)) parts.Add(Model.Publisher);
                parts.Add(Model.SourceText);
                return string.Join("  ·  ", parts);
            }
        }

        public string Help
        {
            get
            {
                string text = Model.Kind == StartupKind.StoreApp
                    ? L.F("startup.help.storeApp", Model.Command)
                    : L.F("startup.help.program", Model.Command);
                text += "\n\n" + L.F("startup.help.source", Model.SourceText);
                text += "\n\n" + (Model.IsProtected ? L.T("startup.help.protected") : L.T("startup.help.canDisable"));
                return text;
            }
        }

        public string StateText => _isEnabled ? L.T("common.state.enabled") : L.T("common.state.disabled");

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _toggle(this, value);
            }
        }

        /// <summary>Refleja el estado real después de intentar el cambio.</summary>
        public void SetState(bool enabled)
        {
            _isEnabled = enabled;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(StateText));
        }
    }

    public sealed class ProcessMemoryViewModel
    {
        public string Name { get; set; }
        public string SizeText { get; set; }
        public double Percent { get; set; }
    }

    /// <summary>Parte de <see cref="MainViewModel"/> dedicada a la sección «Rendimiento».</summary>
    public sealed partial class MainViewModel
    {
        private DispatcherTimer _autoRamTimer;

        public ObservableCollection<StartupItemViewModel> StartupItems { get; } = new ObservableCollection<StartupItemViewModel>();
        public ObservableCollection<ProcessMemoryViewModel> TopProcesses { get; } = new ObservableCollection<ProcessMemoryViewModel>();
        public ObservableCollection<TweakGroupViewModel> GamingGroups { get; } = new ObservableCollection<TweakGroupViewModel>();
        public List<TweakItemViewModel> GamingTweaks { get; } = new List<TweakItemViewModel>();

        public ICommand RefreshStartupCommand { get; private set; }
        public ICommand CleanMemoryCommand { get; private set; }
        public ICommand SelectGamingPresetCommand { get; private set; }
        public ICommand ApplyGamingCommand { get; private set; }
        public ICommand RefreshGamingCommand { get; private set; }

        private void InitPerformance()
        {
            foreach (var cat in GamingCatalog.Categories)
            {
                var group = new TweakGroupViewModel { Name = cat.Name, Glyph = cat.Glyph, Help = cat.Help };
                foreach (var t in GamingCatalog.All.Where(t => t.Category == cat.Name))
                {
                    var vm = new TweakItemViewModel(t);
                    vm.PropertyChanged += OnGamingChanged;
                    group.Items.Add(vm);
                    GamingTweaks.Add(vm);
                }
                if (group.Items.Count > 0) GamingGroups.Add(group);
            }

            RefreshStartupCommand = new RelayCommand(async () => await LoadStartupAsync(), () => !IsLoadingStartup);
            CleanMemoryCommand = new RelayCommand(async () => await CleanMemoryAsync(false), () => !IsCleaningMemory);
            SelectGamingPresetCommand = new RelayCommand(SelectGamingPreset, _ => !IsBusy);
            ApplyGamingCommand = new RelayCommand(async () => await ApplyGamingAsync(), () => !IsBusy && SelectedGamingCount > 0);
            RefreshGamingCommand = new RelayCommand(async () => await RefreshGamingStatesAsync(), () => !IsBusy);

            _autoRamEnabled = Settings.GetBool("AutoRamEnabled", false);
            _autoRamMinutes = NormalizeMinutes(Settings.GetInt("AutoRamMinutes", 15));
            _autoRamOnlyHigh = Settings.GetBool("AutoRamOnlyHigh", true);
            _autoRamNotify = Settings.GetBool("AutoRamNotify", true);
            _startWithWindows = !Settings.Disabled && SelfStartup.IsEnabled();

            _autoRamTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _autoRamTimer.Tick += async (s, e) => await AutoRamTickAsync();
            ScheduleAutoRam();
        }

        // ================================================================== inicio de Windows

        private bool _isLoadingStartup;
        private string _startupSummary = L.T("startup.loading");

        public bool IsLoadingStartup
        {
            get => _isLoadingStartup;
            private set { if (Set(ref _isLoadingStartup, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public string StartupSummary { get => _startupSummary; private set => Set(ref _startupSummary, value); }
        public bool HasStartupItems => StartupItems.Count > 0;

        public async Task LoadStartupAsync()
        {
            if (IsLoadingStartup) return;
            IsLoadingStartup = true;
            try
            {
                var data = await Task.Run(() =>
                    StartupManager.List().Select(e => new { Entry = e, Icon = LoadIcon(e.FilePath) }).ToList());
                StartupItems.Clear();
                foreach (var d in data)
                    StartupItems.Add(new StartupItemViewModel(d.Entry, d.Icon, ToggleStartup));
                UpdateStartupSummary();
                OnPropertyChanged(nameof(HasStartupItems));
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("startup.log.readError", ex.Message));
                StartupSummary = L.T("startup.readError");
            }
            finally
            {
                IsLoadingStartup = false;
            }
        }

        private void UpdateStartupSummary()
        {
            int total = StartupItems.Count, on = StartupItems.Count(i => i.IsEnabled);
            StartupSummary = total == 0
                ? L.T("startup.summary.none")
                : L.P("startup.summary.total", total) + "  ·  " + L.P("startup.summary.enabled", on) + "  ·  " +
                  L.P("startup.summary.disabled", total - on);
        }

        private void ToggleStartup(StartupItemViewModel item, bool enable)
        {
            try
            {
                StartupManager.SetEnabled(item.Model, enable);
                item.SetState(enable);
                Logger.Success(L.F(enable ? "startup.log.enabled" : "startup.log.disabled", item.Name));
            }
            catch (Exception ex)
            {
                item.SetState(item.Model.Enabled);
                Logger.Warn(L.F("startup.log.toggleError", item.Name, ex.Message));
                Dialogs.Show(L.T("startup.toggleError.title"), L.F("startup.toggleError.text", item.Name, ex.Message), DialogKind.Warning);
            }
            UpdateStartupSummary();
        }

        /// <summary>Icono del ejecutable (se congela para poder crearlo fuera del hilo de la interfaz).</summary>
        private static ImageSource LoadIcon(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;
                    var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
            catch
            {
                return null;
            }
        }

        // ================================================================== memoria RAM

        private bool _isCleaningMemory;
        private bool _performanceVisible;
        private bool _loadingProcesses;
        private string _ramAvailableText = "…";
        private string _memoryLastText = L.T("memory.last.never");
        private bool _autoRamEnabled, _autoRamOnlyHigh, _autoRamNotify = true, _startWithWindows;
        private int _autoRamMinutes = 15;
        private DateTime _nextAutoRam;
        private string _autoRamStatus;

        public const int AutoRamThreshold = 80;

        public string RamPercentText => L.F("unit.percent", Math.Round(RamPercent));
        public string RamAvailableText { get => _ramAvailableText; private set => Set(ref _ramAvailableText, value); }
        public string MemoryLastText { get => _memoryLastText; private set => Set(ref _memoryLastText, value); }
        public string AutoRamStatus { get => _autoRamStatus; private set => Set(ref _autoRamStatus, value); }

        public bool IsCleaningMemory
        {
            get => _isCleaningMemory;
            private set { if (Set(ref _isCleaningMemory, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>La lista de procesos solo se actualiza mientras se ve la sección.</summary>
        public bool IsPerformanceVisible
        {
            get => _performanceVisible;
            set
            {
                if (_performanceVisible == value) return;
                _performanceVisible = value;
                if (value) RefreshTopProcesses();
            }
        }

        public bool AutoRamEnabled
        {
            get => _autoRamEnabled;
            set
            {
                if (!Set(ref _autoRamEnabled, value)) return;
                Settings.SetBool("AutoRamEnabled", value);
                Logger.Info(value
                    ? L.P("memory.auto.log.enabled", _autoRamMinutes)
                    : L.T("memory.auto.log.disabled"));
                ScheduleAutoRam();
            }
        }

        public int AutoRamMinutes
        {
            get => _autoRamMinutes;
            set
            {
                value = NormalizeMinutes(value);
                if (!Set(ref _autoRamMinutes, value)) return;
                Settings.SetInt("AutoRamMinutes", value);
                ScheduleAutoRam();
            }
        }

        public bool AutoRamOnlyHigh
        {
            get => _autoRamOnlyHigh;
            set
            {
                if (!Set(ref _autoRamOnlyHigh, value)) return;
                Settings.SetBool("AutoRamOnlyHigh", value);
            }
        }

        /// <summary>Avisar (dentro de la ventana o con una notificación de Windows) cada vez que la limpieza automática libera memoria.</summary>
        public bool AutoRamNotify
        {
            get => _autoRamNotify;
            set
            {
                if (!Set(ref _autoRamNotify, value)) return;
                Settings.SetBool("AutoRamNotify", value);
            }
        }

        /// <summary>Abrir Turbix (minimizado) al iniciar sesión en Windows. Es el mismo valor en Inicio de Windows y en Memoria RAM.</summary>
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (value == _startWithWindows) return;
                string error = value ? SelfStartup.Enable() : SelfStartup.Disable();
                if (error != null)
                {
                    Logger.Warn(L.F("startup.self.log.error", error));
                    Dialogs.Show(L.T("startup.self.error.title"), L.F("startup.self.error.text", error), DialogKind.Warning);
                }
                else
                {
                    Logger.Info(L.T(value ? "startup.self.log.on" : "startup.self.log.off"));
                }
                _startWithWindows = SelfStartup.IsEnabled();
                OnPropertyChanged(nameof(StartWithWindows));
            }
        }

        private void NotifyMemory(bool automatic, ToastKind kind, string title, string text)
        {
            if (automatic && !_autoRamNotify) return;
            Notifications.Show(new ToastMessage
            {
                Title = title,
                Text = text,
                Kind = kind,
                Glyph = kind == ToastKind.Warning ? "" : "",
            });
        }

        private static int NormalizeMinutes(int minutes) => Math.Max(1, Math.Min(240, minutes));

        private void ScheduleAutoRam()
        {
            if (_autoRamEnabled)
            {
                _nextAutoRam = DateTime.Now.AddMinutes(_autoRamMinutes);
                AutoRamStatus = L.F("memory.auto.next", _nextAutoRam.ToString("t", L.Culture));
                _autoRamTimer.Start();
            }
            else
            {
                _autoRamTimer.Stop();
                AutoRamStatus = L.T("memory.auto.off");
            }
        }

        private async Task AutoRamTickAsync()
        {
            if (!_autoRamEnabled || DateTime.Now < _nextAutoRam || IsBusy || IsCleaningMemory) return;
            if (_autoRamOnlyHigh && RamPercent < AutoRamThreshold)
            {
                _nextAutoRam = DateTime.Now.AddMinutes(_autoRamMinutes);
                AutoRamStatus = L.F("memory.auto.skipped", DateTime.Now.ToString("t", L.Culture),
                                    L.F("unit.percent", Math.Round(RamPercent)), _nextAutoRam.ToString("t", L.Culture));
                return;
            }
            await CleanMemoryAsync(true);
        }

        private async Task CleanMemoryAsync(bool automatic)
        {
            if (IsCleaningMemory) return;
            IsCleaningMemory = true;
            try
            {
                var result = await Task.Run(() => MemoryOps.Clean());
                RefreshLiveInfo();
                string when = DateTime.Now.ToString("t", L.Culture);
                if (!result.Ok)
                {
                    MemoryLastText = L.T("memory.last.noAdmin");
                    Logger.Warn(L.T("memory.log.noAdmin"));
                    NotifyMemory(automatic, ToastKind.Warning, L.T("memory.toast.failed.title"), L.T("memory.toast.noAdmin.text"));
                }
                else
                {
                    string freed = FileCleaner.FormatSize(result.FreedBytes);
                    MemoryLastText = result.FreedBytes > 0
                        ? L.F("memory.last.freed", when, freed)
                        : L.F("memory.last.nothing", when);
                    Logger.Success(L.F(automatic ? "memory.log.freedAuto" : "memory.log.freedManual", freed));

                    SystemInfo.GetMemory(out _, out ulong availNow);
                    string available = FileCleaner.FormatSize((long)availNow);
                    if (result.FreedBytes > 0)
                        NotifyMemory(automatic, ToastKind.Success, L.F("memory.toast.freed.title", freed),
                            automatic ? L.F("memory.toast.freedAuto.text", when, available)
                                      : L.F("memory.toast.freed.text", available));
                    else if (!automatic)   // en automático, si no se liberó nada no hace falta avisar
                        NotifyMemory(false, ToastKind.Info, L.T("memory.toast.nothing.title"),
                            L.F("memory.toast.nothing.text", available));
                }
            }
            catch (Exception ex)
            {
                MemoryLastText = L.F("memory.last.error", ex.Message);
                Logger.Warn(L.F("memory.log.error", ex.Message));
                NotifyMemory(automatic, ToastKind.Warning, L.T("memory.toast.failed.title"), ex.Message);
            }
            finally
            {
                IsCleaningMemory = false;
                if (_autoRamEnabled) ScheduleAutoRam();
            }
        }

        /// <summary>Se llama cada pocos segundos junto con el resto de datos en vivo.</summary>
        private void RefreshMemoryDetails()
        {
            SystemInfo.GetMemory(out _, out ulong avail);
            RamAvailableText = L.F("memory.available", FileCleaner.FormatSize((long)avail));
            OnPropertyChanged(nameof(RamPercentText));
            if (_performanceVisible) RefreshTopProcesses();
        }

        private async void RefreshTopProcesses()
        {
            if (_loadingProcesses) return;
            _loadingProcesses = true;
            try
            {
                SystemInfo.GetMemory(out ulong total, out _);
                var top = await Task.Run(() => MemoryOps.TopProcesses(7));
                TopProcesses.Clear();
                foreach (var p in top)
                {
                    TopProcesses.Add(new ProcessMemoryViewModel
                    {
                        Name = p.Count > 1 ? p.Name + "  (" + L.P("proc.count.processes", p.Count) + ")" : p.Name,
                        SizeText = FileCleaner.FormatSize(p.Bytes),
                        Percent = total > 0 ? Math.Min(100, 100.0 * p.Bytes / total) : 0,
                    });
                }
            }
            catch
            {
                // lista informativa: si falla una lectura se reintenta en el siguiente ciclo
            }
            finally
            {
                _loadingProcesses = false;
            }
        }

        // ================================================================== juegos

        public int SelectedGamingCount => GamingTweaks.Count(t => t.IsSelected);

        public string GamingApplyButtonText =>
            L.P("gaming.apply.button", SelectedGamingCount);

        private void OnGamingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TweakItemViewModel.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedGamingCount));
            OnPropertyChanged(nameof(GamingApplyButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SelectGamingPreset(object preset)
        {
            string p = preset as string;
            foreach (var t in GamingTweaks)
                t.IsSelected = p == "max" || (p == "recommended" && t.Model.Recommended);
        }

        public async Task RefreshGamingStatesAsync()
        {
            foreach (var t in GamingTweaks) t.State = TweakState.Unknown;
            var states = await Task.Run(() => TweakEngine.GetStates(GamingTweaks.Select(t => t.Model).ToList()));
            foreach (var t in GamingTweaks)
                t.State = states.TryGetValue(t.Model.Id, out var s) ? s : TweakState.Unknown;
        }

        private async Task ApplyGamingAsync()
        {
            var selected = GamingTweaks.Where(t => t.IsSelected).Select(t => t.Model).ToList();
            if (selected.Count == 0) return;
            var warnings = GamingTweaks.Where(t => t.IsSelected && t.HasWarning).Select(t => "•  " + t.Name + ": " + t.Warning).ToList();
            string msg = L.P("gaming.confirm.text", selected.Count) + "\n\n" + L.T("gaming.confirm.backup");
            if (warnings.Count > 0) msg += "\n\n" + L.T("gaming.confirm.warnings") + "\n" + string.Join("\n", warnings);
            if (!Dialogs.Confirm(L.T("gaming.confirm.title"), msg, L.T("gaming.confirm.button"))) return;

            bool rp = CreateRestorePoint;
            ApplyResult result;
            try
            {
                result = await RunBusyAsync(L.T("gaming.applying"), p => TweakEngine.Apply(selected, rp, p, "rendimiento"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("app.unexpected.title"), ex.Message, DialogKind.Error);
                return;
            }

            LoadBackups();
            await RefreshGamingStatesAsync();
            string summary = result.Changed == 0
                ? L.T("gaming.result.nothing")
                : L.P("gaming.result.changed", result.Changed) +
                  (result.Errors > 0 ? " " + L.P("gaming.result.errors", result.Errors) : "") +
                  "\n\n" + L.T("gaming.result.restart");
            summary += RestorePointNote(rp, result);
            Dialogs.Show(result.Errors > 0 ? L.T("gaming.result.titleWarnings") : L.T("gaming.result.title"), summary,
                result.Errors > 0 ? DialogKind.Warning : DialogKind.Success);
        }
    }
}
