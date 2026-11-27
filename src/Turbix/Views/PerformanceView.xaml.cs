using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Turbix.ViewModels;

namespace Turbix.Views
{
    public partial class PerformanceView : UserControl
    {
        private bool _syncing;

        public PerformanceView()
        {
            InitializeComponent();
            Loaded += (s, e) => SyncInterval();
        }

        /// <summary>Cambia de pestaña: «startup», «processes», «memory» o «gaming».</summary>
        public void ShowTab(string tab)
        {
            switch (tab)
            {
                case "processes": TabProcessesButton.IsChecked = true; break;
                case "memory": TabMemoryButton.IsChecked = true; break;
                case "gaming": TabGamingButton.IsChecked = true; break;
                default: TabStartupButton.IsChecked = true; break;
            }
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (TabStartup == null || TabProcesses == null || TabMemory == null || TabGaming == null) return;
            TabProcesses.Visibility = ReferenceEquals(sender, TabProcessesButton) ? Visibility.Visible : Visibility.Collapsed;
            TabStartup.Visibility = ReferenceEquals(sender, TabStartupButton) ? Visibility.Visible : Visibility.Collapsed;
            TabMemory.Visibility = ReferenceEquals(sender, TabMemoryButton) ? Visibility.Visible : Visibility.Collapsed;
            TabGaming.Visibility = ReferenceEquals(sender, TabGamingButton) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Marca el intervalo guardado en las preferencias.</summary>
        private void SyncInterval()
        {
            if (!(DataContext is MainViewModel vm)) return;
            _syncing = true;
            foreach (var option in IntervalOptions.Children.OfType<RadioButton>())
                option.IsChecked = (option.CommandParameter as string) == vm.AutoRamMinutes.ToString();
            _syncing = false;
        }

        private void OnInterval(object sender, RoutedEventArgs e)
        {
            if (_syncing || !(DataContext is MainViewModel vm)) return;
            if (sender is RadioButton option && int.TryParse(option.CommandParameter as string, out int minutes))
                vm.AutoRamMinutes = minutes;
        }
    }
}
