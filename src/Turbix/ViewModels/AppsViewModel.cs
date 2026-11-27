using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Una app preinstalada que se puede quitar.</summary>
    public sealed class BloatAppViewModel : ObservableObject
    {
        private bool _isSelected;

        public BloatAppViewModel(InstalledApp model)
        {
            Model = model;
            _isSelected = model.Info.Recommended;
            ToggleCommand = new RelayCommand(() => IsSelected = !IsSelected);
        }

        public InstalledApp Model { get; }
        public string Name => Model.Info.Name;
        public string Summary => Model.Info.Summary;
        public string Warning => Model.Info.Warning;
        public bool HasWarning => !string.IsNullOrEmpty(Model.Info.Warning);
        public bool IsAdvanced => !Model.Info.Recommended;
        public ICommand ToggleCommand { get; }

        public string Help
        {
            get
            {
                string text = Model.Info.Help;
                if (HasWarning) text += "\n\n⚠ " + Model.Info.Warning;
                text += "\n\n" + L.F("apps.store.helpFooter", Model.Version);
                return text;
            }
        }

        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    }

    /// <summary>Un programa instalado.</summary>
    public sealed class ProgramViewModel
    {
        public ProgramViewModel(InstalledProgram model, ImageSource icon, Func<ProgramViewModel, Task> uninstall, Func<bool> canRun)
        {
            Model = model;
            Icon = icon;
            UninstallCommand = new RelayCommand(async () => await uninstall(this), canRun);
        }

        public InstalledProgram Model { get; }
        public string Name => Model.Name;
        public ImageSource Icon { get; }
        public bool HasIcon => Icon != null;
        public ICommand UninstallCommand { get; }

        public string Details
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Model.Publisher)) parts.Add(Model.Publisher);
                if (!string.IsNullOrEmpty(Model.Version)) parts.Add("v" + Model.Version);
                if (Model.InstallDate.HasValue) parts.Add(L.F("apps.programs.installedOn", Model.InstallDate.Value.ToString("d", L.Culture)));
                return string.Join("  ·  ", parts);
            }
        }

        public string SizeText => Model.SizeBytes > 0 ? FileCleaner.FormatSize(Model.SizeBytes) : "";
    }

    /// <summary>Parte de <see cref="MainViewModel"/> para la sección «Aplicaciones».</summary>
    public sealed partial class MainViewModel
    {
        public ObservableCollection<BloatAppViewModel> BloatApps { get; } = new ObservableCollection<BloatAppViewModel>();
        public ObservableCollection<ProgramViewModel> Programs { get; } = new ObservableCollection<ProgramViewModel>();
        public ICollectionView ProgramsView { get; private set; }

        public ICommand RefreshAppsCommand { get; private set; }
        public ICommand SelectAppsPresetCommand { get; private set; }
        public ICommand RemoveAppsCommand { get; private set; }
        public ICommand RefreshProgramsCommand { get; private set; }

        private bool _appsLoaded, _isLoadingApps;
        private string _appsSummary = L.T("apps.store.loading"), _programsSummary = L.T("apps.programs.loading"), _programFilter = "";

        public bool IsLoadingApps
        {
            get => _isLoadingApps;
            private set { if (Set(ref _isLoadingApps, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public string AppsSummary { get => _appsSummary; private set => Set(ref _appsSummary, value); }
        public string ProgramsSummary { get => _programsSummary; private set => Set(ref _programsSummary, value); }
        public bool HasBloatApps => BloatApps.Count > 0;
        public int SelectedAppsCount => BloatApps.Count(a => a.IsSelected);
        public string RemoveAppsButtonText => L.P("apps.store.removeButton", SelectedAppsCount);

        public string ProgramFilter
        {
            get => _programFilter;
            set
            {
                if (Set(ref _programFilter, value ?? "")) ProgramsView?.Refresh();
            }
        }

        private void InitApps()
        {
            ProgramsView = CollectionViewSource.GetDefaultView(Programs);
            ProgramsView.Filter = o =>
            {
                if (string.IsNullOrWhiteSpace(_programFilter)) return true;
                var p = (ProgramViewModel)o;
                string f = _programFilter.Trim();
                return p.Name.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                       (p.Model.Publisher ?? "").IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };

            RefreshAppsCommand = new RelayCommand(async () => await RefreshAppsAsync(), () => !IsBusy && !IsLoadingApps);
            RefreshProgramsCommand = new RelayCommand(async () => await RefreshAppsAsync(), () => !IsBusy && !IsLoadingApps);
            SelectAppsPresetCommand = new RelayCommand(p =>
            {
                string preset = p as string;
                foreach (var a in BloatApps) a.IsSelected = preset == "all" || (preset == "recommended" && a.Model.Info.Recommended);
            }, _ => !IsBusy);
            RemoveAppsCommand = new RelayCommand(async () => await RemoveAppsAsync(), () => !IsBusy && SelectedAppsCount > 0);
        }

        /// <summary>La lista se arma la primera vez que se abre la sección.</summary>
        public async void EnsureAppsLoaded()
        {
            if (_appsLoaded) return;
            _appsLoaded = true;
            await RefreshAppsAsync();
        }

        public async Task RefreshAppsAsync()
        {
            if (IsLoadingApps) return;
            _appsLoaded = true;
            IsLoadingApps = true;
            AppsSummary = L.T("apps.store.loading");
            ProgramsSummary = L.T("apps.programs.loading");
            try
            {
                var data = await Task.Run(() => new
                {
                    Supported = AppxOps.IsSupported,
                    Apps = Safe(AppxOps.FindInstalled, "apps.store.log.readError"),
                    Programs = Safe(ProgramsOps.List, "apps.programs.log.listError").Select(p => new { Model = p, Icon = LoadIcon(p.IconPath) }).ToList(),
                });

                foreach (var a in BloatApps) a.PropertyChanged -= OnBloatAppChanged;
                BloatApps.Clear();
                foreach (var app in data.Apps)
                {
                    var vm = new BloatAppViewModel(app);
                    vm.PropertyChanged += OnBloatAppChanged;
                    BloatApps.Add(vm);
                }
                int recommended = data.Apps.Count(a => a.Info.Recommended);
                AppsSummary = !data.Supported ? L.T("apps.store.summary.notSupported")
                    : data.Apps.Count == 0 ? L.T("apps.store.summary.none")
                    : L.P("apps.store.summary.installed", data.Apps.Count) + " · " + L.P("apps.store.summary.recommended", recommended);

                Programs.Clear();
                foreach (var p in data.Programs) Programs.Add(new ProgramViewModel(p.Model, p.Icon, UninstallProgramAsync, () => !IsBusy));
                long total = data.Programs.Sum(p => p.Model.SizeBytes);
                ProgramsSummary = L.P("apps.programs.summary.count", data.Programs.Count) +
                                  (total > 0 ? " · " + L.F("apps.programs.summary.size", FileCleaner.FormatSize(total)) : "");
            }
            finally
            {
                IsLoadingApps = false;
                OnPropertyChanged(nameof(HasBloatApps));
                OnBloatAppChanged(null, new PropertyChangedEventArgs(nameof(BloatAppViewModel.IsSelected)));
            }
        }

        /// <summary><paramref name="errorKey"/>: clave del texto de error, con {0} = mensaje de Windows.</summary>
        private static List<T> Safe<T>(Func<List<T>> f, string errorKey)
        {
            try { return f(); }
            catch (Exception ex)
            {
                Logger.Warn(L.F(errorKey, ex.Message));
                return new List<T>();
            }
        }

        private void OnBloatAppChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BloatAppViewModel.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedAppsCount));
            OnPropertyChanged(nameof(RemoveAppsButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task RemoveAppsAsync()
        {
            var selected = BloatApps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0) return;
            string list = string.Join("\n", selected.Take(12).Select(a => "•  " + a.Name)) + (selected.Count > 12 ? "\n" + L.F("apps.store.confirm.more", selected.Count - 12) : "");
            string msg = L.P("apps.store.confirm.text", selected.Count, list);
            if (!Dialogs.Confirm(L.T("apps.store.confirm.title"), msg, L.T("apps.store.confirm.button"))) return;

            var failed = new List<string>();
            int ok = 0;
            try
            {
                await RunBusyAsync(L.T("apps.store.busy"), p =>
                {
                    Logger.Step(L.P("apps.store.log.removing", selected.Count));
                    int i = 0;
                    foreach (var app in selected)
                    {
                        p.Report(new ProgressInfo(100.0 * i++ / selected.Count, app.Name));
                        try
                        {
                            AppxOps.Remove(app.Model);
                            ok++;
                            Logger.Success("   " + L.F("apps.store.log.removed", app.Name));
                        }
                        catch (Exception ex)
                        {
                            failed.Add($"{app.Name}: {ex.Message}");
                            Logger.Warn("   " + L.F("apps.store.log.failed", app.Name, ex.Message));
                        }
                    }
                    Logger.Step(L.F("apps.store.log.done", ok, selected.Count));
                    return true;
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("app.unexpected.title"), ex.Message, DialogKind.Error);
            }

            await RefreshAppsAsync();
            if (failed.Count == 0)
                Dialogs.Show(L.T("apps.store.done.title"), L.P("apps.store.done.text", ok), DialogKind.Success);
            else
                Dialogs.Show(L.T("apps.store.doneWarn.title"),
                             L.P("apps.store.doneWarn.text", selected.Count, ok, string.Join("\n", failed.Take(8).Select(f => "•  " + f))),
                             DialogKind.Warning);
        }

        private async Task UninstallProgramAsync(ProgramViewModel program)
        {
            string msg = L.F("apps.programs.confirm.text", program.Name);
            if (!Dialogs.Confirm(L.T("apps.programs.confirm.title"), msg, L.T("apps.programs.confirm.button"))) return;

            bool still;
            try
            {
                still = await RunBusyAsync(L.F("apps.programs.uninstalling", program.Name), p =>
                {
                    p.Report(new ProgressInfo(50, L.T("apps.programs.waiting")));
                    ProgramsOps.RunUninstaller(program.Model);
                    return ProgramsOps.StillInstalled(program.Model);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(program.Name + ": " + ex.Message);
                Dialogs.Show(L.T("apps.programs.openFailed"), ex.Message, DialogKind.Error);
                return;
            }

            await RefreshAppsAsync();
            if (still)
                Dialogs.Show(L.T("apps.programs.pending.title"), L.F("apps.programs.pending.text", program.Name), DialogKind.Info);
            else
                Logger.Success(L.F("apps.programs.log.uninstalled", program.Name));
        }
    }
}
