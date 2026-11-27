using System.Windows.Controls;

namespace Turbix.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            // Rutas reales de este equipo (ProgramData no siempre está en C:).
            BackupsPath.Text = Turbix.Core.AppPaths.Backups;
            LogsPath.Text = Turbix.Core.AppPaths.Logs;
        }
    }
}
