using System;

namespace Turbix.Core
{
    public enum ToastKind { Success, Info, Warning }

    public sealed class ToastMessage
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public ToastKind Kind { get; set; }

        /// <summary>Glifo de Segoe MDL2 Assets para el icono del aviso.</summary>
        public string Glyph { get; set; }
    }

    /// <summary>
    /// Avisos breves («Se liberaron 1,2 GB de RAM»). La ventana principal los muestra como notificación de Windows.
    /// Se llama desde el hilo de la interfaz.
    /// </summary>
    public static class Notifications
    {
        public static event Action<ToastMessage> Raised;

        public static void Show(ToastMessage message)
        {
            if (message == null) return;
            Raised?.Invoke(message);
        }
    }
}
