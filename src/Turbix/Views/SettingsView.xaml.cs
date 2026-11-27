using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Turbix.Themes;
using Turbix.ViewModels;

namespace Turbix.Views
{
    /// <summary>Página «Ajustes generales»: tema, idioma, inicio, icono junto al reloj, memoria y datos.</summary>
    public partial class SettingsView : UserControl
    {
        private bool _syncing;
        private MainViewModel _vm;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                Sync();
                ThemeManager.Changed += OnThemeChanged;
                _vm = DataContext as MainViewModel;
                if (_vm != null) _vm.SettingsReset += Sync;
            };
            Unloaded += (s, e) =>
            {
                ThemeManager.Changed -= OnThemeChanged;
                if (_vm != null) _vm.SettingsReset -= Sync;
            };
        }

        private void OnThemeChanged(object sender, EventArgs e) => Sync();

        /// <summary>Marca el tema y el intervalo guardados (pueden haber cambiado desde otra página).</summary>
        public void Sync()
        {
            _syncing = true;
            switch (ThemeManager.Mode)
            {
                case ThemeMode.Light: ThemeLight.IsChecked = true; break;
                case ThemeMode.Dark: ThemeDark.IsChecked = true; break;
                default: ThemeSystem.IsChecked = true; break;
            }
            if (DataContext is MainViewModel vm)
                foreach (var option in IntervalOptions.Children.OfType<RadioButton>())
                    option.IsChecked = (option.CommandParameter as string) == vm.AutoRamMinutes.ToString();
            _syncing = false;
        }

        private void OnThemeChoice(object sender, RoutedEventArgs e)
        {
            if (_syncing || !IsLoaded) return;
            if (ReferenceEquals(sender, ThemeLight)) ThemeManager.SetMode(ThemeMode.Light);
            else if (ReferenceEquals(sender, ThemeDark)) ThemeManager.SetMode(ThemeMode.Dark);
            else ThemeManager.SetMode(ThemeMode.System);
        }

        private void OnInterval(object sender, RoutedEventArgs e)
        {
            if (_syncing || !(DataContext is MainViewModel vm)) return;
            if (sender is RadioButton option && int.TryParse(option.CommandParameter as string, out int minutes))
                vm.AutoRamMinutes = minutes;
        }
    }
}
