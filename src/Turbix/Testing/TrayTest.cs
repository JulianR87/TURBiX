#if SELFTEST
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Turbix.Views;

namespace Turbix.Testing
{
    /// <summary>
    /// Prueba en la máquina virtual de lo que normalmente se hace con el ratón: cerrar con la X, abrir el menú del
    /// icono junto al reloj y pedir «Salir». Cada paso deja anotado el estado; las capturas las toma el script de fuera.
    /// «TURBiX.exe --selftest-tray --out archivo.txt». Solo existe en la compilación de autoprueba.
    /// </summary>
    internal static class TrayTest
    {
        public static void Run(MainWindow main, string outFile)
        {
            outFile = outFile ?? "tray-test.txt";
            void Note(string step) => File.AppendAllText(outFile,
                $"{DateTime.Now:HH:mm:ss} {step} | ventana visible={main.IsVisible} | icono fijo={TrayIcon.IsPersistent}{Environment.NewLine}");

            void At(int seconds, Action action)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    try { action(); }
                    catch (Exception ex) { File.AppendAllText(outFile, "ERROR " + ex + Environment.NewLine); }
                };
                t.Start();
            }

            try { File.Delete(outFile); } catch { }
            At(4, () => Note("inicio"));
            At(8, () => { main.Close(); Note("tras cerrar con la X"); });
            At(20, () => { TrayIcon.ShowMenuForTest(); Note("menú abierto"); });
            At(32, () =>
            {
                TrayIcon.CloseMenuForTest();
                Note("antes de pedir salir");
                // El diálogo de confirmación es modal: el siguiente paso sigue corriendo por su temporizador.
                Application.Current.Dispatcher.BeginInvoke(new Action(App.RequestExit));
            });
            At(44, () =>
            {
                Note("fin (diálogo de salir abierto=" + Dialogs.IsOpen + ")");
                TrayIcon.Dispose();
                Environment.Exit(0);
            });
        }
    }
}
#endif
