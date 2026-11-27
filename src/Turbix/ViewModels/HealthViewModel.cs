using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Un componente del equipo con su estado de salud.</summary>
    public sealed class HealthItemViewModel
    {
        public HealthItemViewModel(HealthComponent model)
        {
            Model = model;
        }

        public HealthComponent Model { get; }
        public string Name => Model.Name;
        public string Glyph => Model.Glyph;
        public string Headline => Model.Headline;
        public string Advice => Model.Advice;
        public bool HasAdvice => !string.IsNullOrEmpty(Model.Advice) && Model.Level >= HealthLevel.Attention;
        public string Help => Model.Help;
        public List<HealthDetail> Details => Model.Details;
        public int DetailColumns => Model.WideDetails ? 1 : 2;
        public string LevelKey => Model.Level.ToString();

        public string LevelText
        {
            get
            {
                switch (Model.Level)
                {
                    case HealthLevel.Good: return L.T("health.level.good");
                    case HealthLevel.Attention: return L.T("health.level.attention");
                    case HealthLevel.Problem: return L.T("health.level.problem");
                    default: return L.T("common.noData");
                }
            }
        }
    }

    /// <summary>Una entrada rota del registro, con su casilla.</summary>
    public sealed class RegIssueViewModel : ObservableObject
    {
        private bool _isSelected = true;

        public RegIssueViewModel(RegIssue model)
        {
            Model = model;
            ToggleCommand = new RelayCommand(() => IsSelected = !IsSelected);
        }

        public RegIssue Model { get; }
        public string Name => Model.Name;
        public string Category => Model.Category.ToUpper(L.Culture);
        public string Detail => Model.Detail;
        public string Help => Model.Help + "\n\n" + L.F("health.reg.where", Model.FullKey) +
                              (Model.ValueNames != null ? "\n" + L.F("health.reg.values", Model.ValueNames.Count)
                                  : Model.DeleteKey ? "" : "\n" + L.F("health.reg.value", Model.ValueName)) +
                              "\n\n" + L.T("health.reg.backupNote");
        public ICommand ToggleCommand { get; }
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    }

    /// <summary>Parte de <see cref="MainViewModel"/> para «Salud» y la reparación del registro.</summary>
    public sealed partial class MainViewModel
    {
        public ObservableCollection<HealthItemViewModel> HealthItems { get; } = new ObservableCollection<HealthItemViewModel>();
        public ObservableCollection<RegIssueViewModel> RegistryIssues { get; } = new ObservableCollection<RegIssueViewModel>();

        public ICommand RefreshHealthCommand { get; private set; }
        public ICommand ScanRegistryCommand { get; private set; }
        public ICommand FixRegistryCommand { get; private set; }
        public ICommand SelectRegistryCommand { get; private set; }

        private void InitHealth()
        {
            RefreshHealthCommand = new RelayCommand(async () => await RefreshHealthAsync(), () => !IsCheckingHealth);
            ScanRegistryCommand = new RelayCommand(async () => await ScanRegistryAsync(), () => !IsBusy);
            FixRegistryCommand = new RelayCommand(async () => await FixRegistryAsync(), () => !IsBusy && SelectedRegistryCount > 0);
            SelectRegistryCommand = new RelayCommand(p =>
            {
                bool all = (p as string) == "all";
                foreach (var i in RegistryIssues) i.IsSelected = all;
            }, _ => !IsBusy);
        }

        // ================================================================== salud

        private bool _isCheckingHealth;
        private bool _healthLoaded;
        private string _healthSummary = L.T("health.analyzing");
        private string _healthLevelKey = "Unknown";

        public bool IsCheckingHealth
        {
            get => _isCheckingHealth;
            private set { if (Set(ref _isCheckingHealth, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public string HealthSummary { get => _healthSummary; private set => Set(ref _healthSummary, value); }

        /// <summary>Peor estado encontrado: Good, Attention, Problem o Unknown.</summary>
        public string HealthLevelKey { get => _healthLevelKey; private set => Set(ref _healthLevelKey, value); }

        /// <summary>El análisis tarda unos segundos, así que se hace la primera vez que se abre la sección.</summary>
        public async void EnsureHealthLoaded()
        {
            if (_healthLoaded) return;
            _healthLoaded = true;
            await RefreshHealthAsync();
        }

        public async Task RefreshHealthAsync()
        {
            if (IsCheckingHealth) return;
            _healthLoaded = true;
            IsCheckingHealth = true;
            HealthSummary = L.T("health.analyzing");
            try
            {
                var components = await Task.Run(() => HealthCheck.Run());
                HealthItems.Clear();
                foreach (var c in components.OrderByDescending(c => (int)c.Level))
                    HealthItems.Add(new HealthItemViewModel(c));

                int problems = components.Count(c => c.Level == HealthLevel.Problem);
                int attention = components.Count(c => c.Level == HealthLevel.Attention);
                if (problems > 0)
                {
                    HealthLevelKey = "Problem";
                    HealthSummary = attention > 0
                        ? L.P("health.summary.problemsAndWatch", problems, attention.ToString("N0", L.Culture))
                        : L.P("health.summary.problems", problems);
                }
                else if (attention > 0)
                {
                    HealthLevelKey = "Attention";
                    HealthSummary = L.P("health.summary.watch", attention);
                }
                else
                {
                    HealthLevelKey = "Good";
                    HealthSummary = components.Count == 0 ? L.T("health.summary.noData") : L.T("health.summary.allGood");
                }
                Logger.Info(L.F("health.log.summary", HealthSummary));
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("health.log.error", ex.Message));
                HealthSummary = L.T("health.summary.failed");
            }
            finally
            {
                IsCheckingHealth = false;
            }
        }

        // ================================================================== registro de Windows

        private bool _registryScanned;
        private string _registrySummary = L.T("health.reg.intro");

        public string RegistrySummary { get => _registrySummary; private set => Set(ref _registrySummary, value); }
        public bool HasRegistryIssues => RegistryIssues.Count > 0;
        public int SelectedRegistryCount => RegistryIssues.Count(i => i.IsSelected);

        public string FixRegistryButtonText =>
            L.P("health.reg.fixButton", SelectedRegistryCount);

        private void OnRegistryIssueChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(RegIssueViewModel.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedRegistryCount));
            OnPropertyChanged(nameof(FixRegistryButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        public async Task ScanRegistryAsync()
        {
            List<RegIssue> found;
            try
            {
                found = await RunBusyAsync(L.T("health.reg.scanning"), p => RegistryRepair.Scan(p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("health.reg.error.title"), ex.Message, DialogKind.Error);
                return;
            }
            SetRegistryIssues(found);
            _registryScanned = true;
            RegistrySummary = found.Count == 0
                ? L.T("health.reg.none")
                : L.P("health.reg.found", found.Count);
        }

        private void SetRegistryIssues(List<RegIssue> issues)
        {
            RegistryIssues.Clear();
            foreach (var issue in issues.OrderBy(i => i.Order).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var vm = new RegIssueViewModel(issue);
                vm.PropertyChanged += OnRegistryIssueChanged;
                RegistryIssues.Add(vm);
            }
            OnPropertyChanged(nameof(HasRegistryIssues));
            OnPropertyChanged(nameof(SelectedRegistryCount));
            OnPropertyChanged(nameof(FixRegistryButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task FixRegistryAsync()
        {
            var selected = RegistryIssues.Where(i => i.IsSelected).Select(i => i.Model).ToList();
            if (selected.Count == 0 || !_registryScanned) return;
            string msg = L.P("health.reg.confirm.text", selected.Count);
            if (!Dialogs.Confirm(L.T("health.reg.confirm.title"), msg, L.T("health.reg.confirm.button"))) return;

            int[] result;
            try
            {
                result = await RunBusyAsync(L.T("health.reg.fixing"), p => RegistryRepair.Fix(selected, p));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("health.reg.error.title"), ex.Message, DialogKind.Error);
                return;
            }

            LoadBackups();
            // Lo que no se reparó (desmarcado o con error) sigue en la lista.
            var remaining = await Task.Run(() => RegistryRepair.Scan(null));
            SetRegistryIssues(remaining);
            RegistrySummary = remaining.Count == 0
                ? L.T("health.reg.clean")
                : L.P("health.reg.remaining", remaining.Count);

            if (result[1] == 0)
                Dialogs.Show(L.T("health.reg.done.title"), L.P("health.reg.done.text", result[0]), DialogKind.Success);
            else
                Dialogs.Show(L.T("health.reg.doneWarn.title"),
                    L.P("health.reg.doneWarn.text", result[0], result[1].ToString("N0", L.Culture)),
                    DialogKind.Warning);
        }
    }
}
