using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Turbix.Views
{
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();
            // Al abrir se ve lo más reciente, y cada línea nueva baja la vista hasta ella.
            Loaded += (s, e) => ScrollToEnd();
            ((INotifyCollectionChanged)List.Items).CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && IsVisible) ScrollToEnd();
            };
            IsVisibleChanged += (s, e) => { if (IsVisible) ScrollToEnd(); };
        }

        private void ScrollToEnd()
        {
            if (List.Items.Count > 0) List.ScrollIntoView(List.Items[List.Items.Count - 1]);
        }
    }
}
