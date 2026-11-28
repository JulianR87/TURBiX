using System.Windows;
using System.Windows.Controls;
using Turbix.Localization;
using Turbix.ViewModels;

namespace Turbix.Views
{
    public partial class NetworkView : UserControl
    {
        private NetworkViewModel _vm;

        public NetworkView()
        {
            InitializeComponent();
            // Solo mide mientras se ve: con la página oculta (o la ventana en el área de notificación) no gasta nada.
            IsVisibleChanged += (s, e) => Sync();
            DataContextChanged += (s, e) => Sync();
            Unloaded += (s, e) => _vm?.Stop();
            TabHelp.Title = L.T("net.tab.traffic");
            TabHelp.Text = L.T("net.help.traffic");
        }

        private void Sync()
        {
            var vm = (DataContext as MainViewModel)?.Network;
            if (vm != null && !ReferenceEquals(vm, _vm))
            {
                if (_vm != null) _vm.TabRequested -= ShowTab;
                _vm = vm;
                _vm.TabRequested += ShowTab;
            }
            if (_vm == null) return;
            if (IsVisible) _vm.Start();
            else _vm.Stop();
        }

        /// <summary>Cambia de pestaña: «traffic», «connections», «ports» o «blocks».</summary>
        public void ShowTab(string tab)
        {
            switch (tab)
            {
                case "connections": TabConnectionsButton.IsChecked = true; break;
                case "ports": TabPortsButton.IsChecked = true; break;
                case "blocks": TabBlocksButton.IsChecked = true; break;
                default: TabTrafficButton.IsChecked = true; break;
            }
        }

        private void OnTab(object sender, RoutedEventArgs e)
        {
            if (TabTraffic == null || TabConnections == null || TabPorts == null || TabBlocks == null) return;
            TabTraffic.Visibility = ReferenceEquals(sender, TabTrafficButton) ? Visibility.Visible : Visibility.Collapsed;
            TabConnections.Visibility = ReferenceEquals(sender, TabConnectionsButton) ? Visibility.Visible : Visibility.Collapsed;
            TabPorts.Visibility = ReferenceEquals(sender, TabPortsButton) ? Visibility.Visible : Visibility.Collapsed;
            TabBlocks.Visibility = ReferenceEquals(sender, TabBlocksButton) ? Visibility.Visible : Visibility.Collapsed;

            string title, help;
            if (ReferenceEquals(sender, TabConnectionsButton)) { title = "net.tab.connections"; help = "net.help.connections"; }
            else if (ReferenceEquals(sender, TabPortsButton)) { title = "net.tab.ports"; help = "net.help.ports"; }
            else if (ReferenceEquals(sender, TabBlocksButton)) { title = "net.tab.blocks"; help = "net.help.blocks"; }
            else { title = "net.tab.traffic"; help = "net.help.traffic"; }
            TabHelp.Title = L.T(title);
            TabHelp.Text = L.T(help);
        }

        // Con el ratón sobre una lista no se reordena, para no mover la fila que se va a pulsar.
        private void OnListEnter(object sender, System.Windows.Input.MouseEventArgs e) { if (_vm != null) _vm.HoldOrder = true; }
        private void OnListLeave(object sender, System.Windows.Input.MouseEventArgs e) { if (_vm != null) _vm.HoldOrder = false; }
    }
}
