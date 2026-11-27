using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Turbix.Cleanup;
using Turbix.Core;
using Turbix.Localization;
using Turbix.Tweaks;

namespace Turbix.ViewModels
{
    public sealed class TweakItemViewModel : ObservableObject
    {
        private bool _isSelected;
        private TweakState _state = TweakState.Unknown;

        public TweakItemViewModel(Tweak model)
        {
            Model = model;
            _isSelected = model.Recommended;
            ToggleCommand = new RelayCommand(() => IsSelected = !IsSelected);
        }

        public Tweak Model { get; }
        public string Name => Model.Name;
        public string Summary => Model.Summary;
        public string Warning => Model.Warning;
        public bool HasWarning => !string.IsNullOrEmpty(Model.Warning);
        public bool IsAdvanced => !Model.Recommended;
        public ICommand ToggleCommand { get; }

        public string Help
        {
            get
            {
                string text = Model.Help;
                if (HasWarning) text += "\n\n⚠ " + Model.Warning;
                text += "\n\n" + L.F("tweak.help.footer", Model.Actions.Count);
                return text;
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public TweakState State
        {
            get => _state;
            set
            {
                if (Set(ref _state, value))
                {
                    OnPropertyChanged(nameof(StateText));
                    OnPropertyChanged(nameof(StateKey));
                }
            }
        }

        public string StateKey => _state.ToString();

        public string StateText
        {
            get
            {
                switch (_state)
                {
                    case TweakState.Applied: return L.T("common.state.applied");
                    case TweakState.Partial: return L.T("common.state.partial");
                    case TweakState.NotApplied: return L.T("common.state.notApplied");
                    case TweakState.NotApplicable: return L.T("common.state.notApplicable");
                    default: return L.T("tweak.state.checking");
                }
            }
        }
    }

    public sealed class TweakGroupViewModel
    {
        public string Name { get; set; }
        public string Glyph { get; set; }
        public string Help { get; set; }
        public ObservableCollection<TweakItemViewModel> Items { get; } = new ObservableCollection<TweakItemViewModel>();
        public string CountText => L.P("tweak.group.count", Items.Count);
    }

    public sealed class CleanupItemViewModel : ObservableObject
    {
        private bool _isSelected;
        private long? _bytes;
        private string _sizeText = "—";
        private bool _freed;

        public CleanupItemViewModel(CleanupItem model)
        {
            Model = model;
            _isSelected = model.Recommended;
            ToggleCommand = new RelayCommand(() => IsSelected = !IsSelected);
        }

        public CleanupItem Model { get; }
        public string Name => Model.Name;
        public string Summary => Model.Summary;
        public string Glyph => Model.Glyph;
        public string Warning => Model.Warning;
        public bool HasWarning => !string.IsNullOrEmpty(Model.Warning);
        public bool IsAdvanced => !Model.Recommended;
        public ICommand ToggleCommand { get; }

        public string Help => HasWarning ? Model.Help + "\n\n⚠ " + Model.Warning : Model.Help;

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public long? Bytes
        {
            get => _bytes;
            private set => Set(ref _bytes, value);
        }

        public string SizeText
        {
            get => _sizeText;
            private set => Set(ref _sizeText, value);
        }

        /// <summary>true tras limpiar: el tamaño mostrado es lo liberado.</summary>
        public bool IsFreed
        {
            get => _freed;
            private set => Set(ref _freed, value);
        }

        public void SetMeasured(long? bytes)
        {
            IsFreed = false;
            Bytes = bytes;
            SizeText = bytes.HasValue ? FileCleaner.FormatSize(bytes.Value) : "—";
        }

        public void SetFreed(long? bytes)
        {
            Bytes = 0;
            IsFreed = true;
            SizeText = bytes.HasValue ? "−" + FileCleaner.FormatSize(bytes.Value) : L.T("tweak.cleanupDone");
        }
    }

    public sealed class BackupViewModel
    {
        public BackupViewModel(BackupSession session)
        {
            Session = session;
        }

        public BackupSession Session { get; }
        public string Title => Session.DisplayLabel;
        public string DateText => Session.CreatedDate.ToString(L.T("tweak.backup.dateFormat"), L.Culture);
        public string Details => L.P("tweak.backup.details", Session.Entries.Count, Session.User);
        public bool IsReverted => !string.IsNullOrEmpty(Session.Reverted);
        public int ServiceCount => Session.Entries.Count(e => e.Type == "Service");
    }
}
