using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Tweaks;
using Turbix.Views;

namespace Turbix.ViewModels
{
    /// <summary>Un resultado del diagnóstico del equipo.</summary>
    public sealed class FindingViewModel
    {
        public FindingViewModel(Finding model, Action<string> navigate)
        {
            Model = model;
            ActionCommand = new RelayCommand(() => navigate(model.ActionPage));
        }

        public Finding Model { get; }
        public string Title => Model.Title;
        public string Text => Model.Text;
        public string ActionText => Model.ActionText;
        public bool HasAction => !string.IsNullOrEmpty(Model.ActionPage);
        public string LevelKey => Model.Level.ToString();
        public ICommand ActionCommand { get; }

        public string Glyph
        {
            get
            {
                switch (Model.Level)
                {
                    case FindingLevel.Ok: return "";
                    case FindingLevel.Info: return "";
                    default: return "";
                }
            }
        }
    }

    public sealed class RepairToolViewModel
    {
        public RepairToolViewModel(RepairTool model, Func<RepairTool, Task> run, Func<bool> canRun)
        {
            Model = model;
            RunCommand = new RelayCommand(async () => await run(model), canRun);
        }

        public RepairTool Model { get; }
        public string Name => Model.Name;
        public string Summary => Model.Summary;
        public string Glyph => Model.Glyph;
        public string Warning => Model.Warning;
        public bool HasWarning => !string.IsNullOrEmpty(Model.Warning);
        // Espacios duros: «Tarda menos de 1 min» no debe partirse dejando «min» solo en otra línea.
        public string Duration => L.F("tools.repair.takes", Model.Duration.Replace(' ', '\u00A0'));
        public ICommand RunCommand { get; }

        public string Help
        {
            get
            {
                string text = Model.Help + "\n\n" + L.F("tools.repair.helpDuration", Model.Duration);
                if (HasWarning) text += "\n\n⚠ " + Model.Warning;
                return text;
            }
        }
    }

    /// <summary>Parte de <see cref="MainViewModel"/> para «Soluciones» y el diagnóstico del inicio.</summary>
    public sealed partial class MainViewModel
    {
        public ObservableCollection<FindingViewModel> Findings { get; } = new ObservableCollection<FindingViewModel>();
        public List<RepairToolViewModel> RepairTools { get; } = new List<RepairToolViewModel>();
        public ObservableCollection<TweakGroupViewModel> WindowsGroups { get; } = new ObservableCollection<TweakGroupViewModel>();
        public List<TweakItemViewModel> WindowsTweaks { get; } = new List<TweakItemViewModel>();

        public ICommand RefreshDiagnosticsCommand { get; private set; }
        public ICommand SelectWindowsPresetCommand { get; private set; }
        public ICommand ApplyWindowsCommand { get; private set; }
        public ICommand RefreshWindowsCommand { get; private set; }

        private void InitTools()
        {
            foreach (var cat in WindowsCatalog.Categories)
            {
                var group = new TweakGroupViewModel { Name = cat.Name, Glyph = cat.Glyph, Help = cat.Help };
                foreach (var t in WindowsCatalog.All.Where(t => t.Category == cat.Name))
                {
                    var vm = new TweakItemViewModel(t);
                    vm.PropertyChanged += OnWindowsTweakChanged;
                    group.Items.Add(vm);
                    WindowsTweaks.Add(vm);
                }
                if (group.Items.Count > 0) WindowsGroups.Add(group);
            }

            foreach (var tool in RepairOps.All)
                RepairTools.Add(new RepairToolViewModel(tool, RunRepairAsync, () => !IsBusy));

            RefreshDiagnosticsCommand = new RelayCommand(async () => await RefreshDiagnosticsAsync(), () => !IsDiagnosing);
            SelectWindowsPresetCommand = new RelayCommand(SelectWindowsPreset, _ => !IsBusy);
            ApplyWindowsCommand = new RelayCommand(async () => await ApplyWindowsAsync(), () => !IsBusy && SelectedWindowsCount > 0);
            RefreshWindowsCommand = new RelayCommand(async () => await RefreshWindowsStatesAsync(), () => !IsBusy);
        }

        // ================================================================== diagnóstico

        private bool _isDiagnosing;
        private string _diagnosticsSummary = L.T("tools.diag.checking");

        public bool IsDiagnosing
        {
            get => _isDiagnosing;
            private set { if (Set(ref _isDiagnosing, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        public string DiagnosticsSummary { get => _diagnosticsSummary; private set => Set(ref _diagnosticsSummary, value); }

        public async Task RefreshDiagnosticsAsync()
        {
            if (IsDiagnosing) return;
            IsDiagnosing = true;
            try
            {
                var findings = await Task.Run(() => Diagnostics.Run());
                Findings.Clear();
                foreach (var f in findings)
                    Findings.Add(new FindingViewModel(f, page => NavigateRequested?.Invoke(page)));

                int issues = findings.Count(f => f.Level >= FindingLevel.Warning);
                int notes = findings.Count(f => f.Level == FindingLevel.Info);
                if (issues > 0) DiagnosticsSummary = L.P("tools.diag.issues", issues);
                else if (notes > 0) DiagnosticsSummary = L.P("tools.diag.notes", notes);
                else DiagnosticsSummary = L.T("tools.diag.none");
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("tools.diag.log.error", ex.Message));
                DiagnosticsSummary = L.T("tools.diag.failed");
            }
            finally
            {
                IsDiagnosing = false;
            }
        }

        // ================================================================== reparar

        private async Task RunRepairAsync(RepairTool tool)
        {
            string msg = tool.Summary + "\n\n" + L.F("tools.repair.confirm", tool.Duration);
            if (!string.IsNullOrEmpty(tool.Warning)) msg += "\n\n" + tool.Warning;
            if (!Dialogs.Confirm(tool.Name, msg, L.T("tools.repair.run"))) return;

            string result;
            try
            {
                result = await RunBusyAsync(tool.Name, p => tool.Run(p));
            }
            catch (Exception ex)
            {
                Logger.Error(L.F("tools.repair.log", tool.Name, ex.Message));
                Dialogs.Show(L.T("tools.repair.failed"), ex.Message, DialogKind.Error);
                return;
            }
            Logger.Info(L.F("tools.repair.log", tool.Name, result));
            Dialogs.Show(tool.Name, result, DialogKind.Info);
        }

        // ================================================================== ajustes de Windows

        public int SelectedWindowsCount => WindowsTweaks.Count(t => t.IsSelected);

        public string WindowsApplyButtonText => L.P("wintweak.apply.button", SelectedWindowsCount);

        private void OnWindowsTweakChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TweakItemViewModel.IsSelected)) return;
            OnPropertyChanged(nameof(SelectedWindowsCount));
            OnPropertyChanged(nameof(WindowsApplyButtonText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SelectWindowsPreset(object preset)
        {
            string p = preset as string;
            foreach (var t in WindowsTweaks)
                t.IsSelected = p == "max" || (p == "recommended" && t.Model.Recommended);
        }

        public async Task RefreshWindowsStatesAsync()
        {
            foreach (var t in WindowsTweaks) t.State = TweakState.Unknown;
            var states = await Task.Run(() => TweakEngine.GetStates(WindowsTweaks.Select(t => t.Model).ToList()));
            foreach (var t in WindowsTweaks)
                t.State = states.TryGetValue(t.Model.Id, out var s) ? s : TweakState.Unknown;
        }

        private async Task ApplyWindowsAsync()
        {
            var selected = WindowsTweaks.Where(t => t.IsSelected).Select(t => t.Model).ToList();
            if (selected.Count == 0) return;
            var warnings = WindowsTweaks.Where(t => t.IsSelected && t.HasWarning).Select(t => "•  " + t.Name + ": " + t.Warning).ToList();
            string msg = L.P("wintweak.apply.confirm", selected.Count);
            if (warnings.Count > 0) msg += "\n\n" + L.T("wintweak.apply.warnings") + "\n" + string.Join("\n", warnings);
            if (!Dialogs.Confirm(L.T("wintweak.apply.title"), msg, L.T("wintweak.apply.confirmButton"))) return;

            bool rp = CreateRestorePoint;
            ApplyResult result;
            try
            {
                result = await RunBusyAsync(L.T("wintweak.apply.busy"), p => TweakEngine.Apply(selected, rp, p, "Windows"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Dialogs.Show(L.T("wintweak.apply.error"), ex.Message, DialogKind.Error);
                return;
            }

            LoadBackups();
            await RefreshWindowsStatesAsync();
            string summary = result.Changed == 0
                ? L.T("wintweak.result.nothing")
                : (result.Errors > 0 ? L.P("wintweak.result.changedErrors", result.Changed, result.Errors) : L.P("wintweak.result.changed", result.Changed)) +
                  "\n\n" + L.T("wintweak.result.restart");
            summary += RestorePointNote(rp, result);
            Dialogs.Show(result.Errors > 0 ? L.T("wintweak.result.titleWarnings") : L.T("wintweak.result.title"), summary,
                result.Errors > 0 ? DialogKind.Warning : DialogKind.Success);
        }
    }
}
