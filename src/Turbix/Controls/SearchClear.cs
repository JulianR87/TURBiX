using System.Windows.Controls;
using System.Windows.Input;
using Turbix.ViewModels;

namespace Turbix.Controls
{
    /// <summary>Comando de la «X» de los cuadros de búsqueda: borra el texto y deja el cursor en el cuadro.</summary>
    public static class SearchClear
    {
        public static readonly ICommand Command = new RelayCommand(p =>
        {
            if (!(p is TextBox box)) return;
            box.Clear();
            box.Focus();
        });
    }
}
