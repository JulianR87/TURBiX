using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Turbix.Localization;

namespace Turbix.Views
{
    public enum DialogKind { Info, Question, Success, Warning, Error }

    public partial class DialogWindow : Window
    {
        public DialogWindow()
        {
            InitializeComponent();
        }

        public void Setup(string title, string message, DialogKind kind, string okText, string cancelText)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            OkButton.Content = okText;
            if (cancelText == null) CancelButton.Visibility = Visibility.Collapsed;
            else CancelButton.Content = cancelText;

            string glyph, fg, bg;
            switch (kind)
            {
                case DialogKind.Success: glyph = ""; fg = "Success"; bg = "SuccessSoft"; break;
                case DialogKind.Warning: glyph = ""; fg = "Warning"; bg = "WarningSoft"; break;
                case DialogKind.Error: glyph = ""; fg = "Danger"; bg = "DangerSoft"; break;
                case DialogKind.Question: glyph = ""; fg = "AccentText"; bg = "AccentSoft"; break;
                default: glyph = ""; fg = "AccentText"; bg = "AccentSoft"; break;
            }
            IconGlyph.Text = glyph;
            IconGlyph.SetResourceReference(TextBlock.ForegroundProperty, fg);
            IconTile.SetResourceReference(Border.BackgroundProperty, bg);
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }

    /// <summary>Diálogos con el estilo de Turbix (sustituyen a MessageBox).</summary>
    public static class Dialogs
    {
        public static bool Confirm(string title, string message, string confirmText = null) =>
            ShowCore(title, message, DialogKind.Question, confirmText ?? L.T("common.continue"), L.T("common.cancel"));

        public static void Show(string title, string message, DialogKind kind = DialogKind.Info) =>
            ShowCore(title, message, kind, L.T("common.ok"), null);

        private static int _open;

        /// <summary>true mientras hay un diálogo esperando respuesta.</summary>
        public static bool IsOpen => _open > 0;

        private static bool ShowCore(string title, string message, DialogKind kind, string ok, string cancel)
        {
            var dlg = new DialogWindow();
            var owner = Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized && !ReferenceEquals(owner, dlg))
                dlg.Owner = owner;
            else
            {
                // Sin ventana a la vista (Turbix junto al reloj): que el diálogo no quede escondido detrás de otras.
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dlg.ShowInTaskbar = true;
                dlg.Topmost = true;
            }
            dlg.Setup(title, message, kind, ok, cancel);
            _open++;
            try
            {
                return dlg.ShowDialog() == true;
            }
            finally
            {
                _open--;
            }
        }
    }
}
