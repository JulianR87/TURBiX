using System.Windows;
using System.Windows.Controls;

namespace Turbix.Views
{
    public partial class ToolsView : UserControl
    {
        public ToolsView()
        {
            InitializeComponent();
        }

        /// <summary>Cambia de pestaña: «repair», «registry» o «windows».</summary>
        public void ShowTab(string tab)
        {
            if (tab == "windows") TabWindowsButton.IsChecked = true;
            else if (tab == "registry") TabRegistryButton.IsChecked = true;
            else TabRepairButton.IsChecked = true;
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (TabRepair == null || TabRegistry == null || TabWindows == null) return;
            TabRepair.Visibility = ReferenceEquals(sender, TabRepairButton) ? Visibility.Visible : Visibility.Collapsed;
            TabRegistry.Visibility = ReferenceEquals(sender, TabRegistryButton) ? Visibility.Visible : Visibility.Collapsed;
            TabWindows.Visibility = ReferenceEquals(sender, TabWindowsButton) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
