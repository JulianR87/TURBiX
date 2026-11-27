using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Turbix.Controls
{
    /// <summary>
    /// Botón circular «?» que muestra una breve explicación al pasar el ratón
    /// o al hacer clic (útil también en pantallas táctiles).
    /// </summary>
    public class HelpTip : Button
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(HelpTip), new PropertyMetadata(null, OnTextChanged));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(HelpTip), new PropertyMetadata(null, OnTextChanged));

        private readonly ToolTip _tip;

        public HelpTip()
        {
            _tip = new ToolTip { Placement = PlacementMode.Bottom, PlacementTarget = this, HorizontalOffset = -150 };
            ToolTip = _tip;
            ToolTipService.SetInitialShowDelay(this, 150);
            ToolTipService.SetShowDuration(this, 60000);
            ToolTipService.SetBetweenShowDelay(this, 0);
            // Accesible con teclado: Tab llega al «?», Enter o Espacio lo abren y Esc lo cierra.
            Focusable = true;
            System.Windows.Automation.AutomationProperties.SetName(this, Turbix.Localization.L.T("shell.help"));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var h = (HelpTip)d;
            h._tip.Content = string.IsNullOrEmpty(h.Title) ? h.Text : h.Title + "\n" + h.Text;
            h._tip.Tag = h.Title;
            System.Windows.Automation.AutomationProperties.SetHelpText(h, h.Text ?? string.Empty);
        }

        protected override void OnClick()
        {
            base.OnClick();
            _tip.IsOpen = !_tip.IsOpen;
        }

        protected override void OnLostKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            _tip.IsOpen = false;
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && _tip.IsOpen)
            {
                _tip.IsOpen = false;
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _tip.IsOpen = false;
        }
    }
}
