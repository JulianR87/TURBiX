using System.Windows;
using System.Windows.Controls;

namespace Turbix.Views
{
    public partial class AppsView : UserControl
    {
        public AppsView()
        {
            InitializeComponent();
        }

        /// <summary>Cambia de pestaña: «store» o «programs».</summary>
        public void ShowTab(string tab)
        {
            if (tab == "programs") TabProgramsButton.IsChecked = true;
            else TabStoreButton.IsChecked = true;
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (TabStore == null || TabPrograms == null) return;
            TabStore.Visibility = ReferenceEquals(sender, TabStoreButton) ? Visibility.Visible : Visibility.Collapsed;
            TabPrograms.Visibility = ReferenceEquals(sender, TabProgramsButton) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
