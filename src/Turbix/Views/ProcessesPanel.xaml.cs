using System.Windows;
using System.Windows.Controls;
using Turbix.ViewModels;

namespace Turbix.Views
{
    public partial class ProcessesPanel : UserControl
    {
        public ProcessesPanel()
        {
            InitializeComponent();
            // Solo se actualiza mientras se ve: con la pestaña oculta no gasta procesador.
            IsVisibleChanged += (s, e) => Sync();
            DataContextChanged += (s, e) => Sync();
        }

        private void Sync()
        {
            if (!(DataContext is ProcessesViewModel vm)) return;
            if (IsVisible) vm.Start();
            else vm.Stop();
        }
    }
}
