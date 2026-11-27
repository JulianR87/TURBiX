using System;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;

namespace Turbix.Core
{
    /// <summary>
    /// «Abrir Turbix al iniciar Windows».
    ///
    /// Turbix pide permisos de administrador, y Windows no abre programas así desde la clave «Run» del registro
    /// (los ignora sin avisar). Por eso se usa una tarea del Programador de tareas que se ejecuta al iniciar sesión
    /// con privilegios elevados: así arranca sin la ventana de confirmación de Windows.
    /// Se maneja con el objeto COM «Schedule.Service», incluido en Windows; sin schtasks.exe ni PowerShell.
    /// La tarea solo abre Turbix (minimizado): no aplica ajustes ni limpia nada por su cuenta.
    /// </summary>
    public static class SelfStartup
    {
        public const string TaskName = "TURBiX";
        public const string StartupArg = "--startup";

        private const int TaskCreateOrUpdate = 6;
        private const int TaskLogonInteractiveToken = 3;

        private static string ExePath => Process.GetCurrentProcess().MainModule.FileName;

        /// <summary>true si la tarea existe, está habilitada y abre este mismo Turbix (si se movió el exe, cuenta como apagado).</summary>
        public static bool IsEnabled()
        {
            try
            {
                dynamic task = Folder().GetTask(TaskName);
                if (!(bool)task.Enabled) return false;
                foreach (dynamic action in task.Definition.Actions)
                {
                    string path = ((string)action.Path ?? string.Empty).Trim('"');
                    if (string.Equals(path, ExePath, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch
            {
                return false;   // no existe o no se pudo leer
            }
        }

        /// <summary>true si existe la tarea «Turbix», apunte al exe que apunte (la usa el instalador).</summary>
        public static bool TaskExists()
        {
            try
            {
                Folder().GetTask(TaskName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Crea o actualiza la tarea. Devuelve null si salió bien, o el mensaje de error.</summary>
        public static string Enable() => Enable(ExePath);

        /// <summary>Igual, pero para que abra otro Turbix.exe (el que acaba de copiar el instalador).</summary>
        public static string Enable(string exePath)
        {
            try
            {
                string user = WindowsIdentity.GetCurrent().Name;
                Folder().RegisterTask(TaskName, BuildXml(user, exePath), TaskCreateOrUpdate, null, null, TaskLogonInteractiveToken, null);
                if (!string.Equals(exePath, ExePath, StringComparison.OrdinalIgnoreCase)) return null;
                return IsEnabled() ? null : "?";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Borra la tarea. Devuelve null si salió bien (o si no existía), o el mensaje de error.</summary>
        public static string Disable()
        {
            try
            {
                var folder = Folder();
                try { folder.GetTask(TaskName); }
                catch { return null; }
                folder.DeleteTask(TaskName, 0);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static dynamic Folder()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service", true);
            dynamic service = Activator.CreateInstance(type);
            service.Connect();
            return service.GetFolder("\\");
        }

        /// <summary>
        /// Definición de la tarea. Lo importante: sin límite de tiempo (por defecto Windows la cortaría a las 72 h),
        /// también con batería, prioridad normal y una sola copia.
        /// </summary>
        private static string BuildXml(string user, string exe)
        {
            string u = SecurityElement.Escape(user);
            return
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
  "<RegistrationInfo><Author>TURBiX</Author><Description>" + SecurityElement.Escape(Localization.L.T("startup.self.taskDescription")) + "</Description></RegistrationInfo>" +
  "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + u + "</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>" +
  "<Principals><Principal id=\"Author\"><UserId>" + u + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
  "<Settings>" +
    "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
    "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
    "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
    "<AllowHardTerminate>true</AllowHardTerminate>" +
    "<StartWhenAvailable>false</StartWhenAvailable>" +
    "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
    "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
    "<AllowStartOnDemand>true</AllowStartOnDemand>" +
    "<Enabled>true</Enabled>" +
    "<Hidden>false</Hidden>" +
    "<RunOnlyIfIdle>false</RunOnlyIfIdle>" +
    "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>" +
    "<Priority>5</Priority>" +
  "</Settings>" +
  "<Actions Context=\"Author\"><Exec><Command>\"" + SecurityElement.Escape(exe) + "\"</Command><Arguments>" + StartupArg + "</Arguments></Exec></Actions>" +
"</Task>";
        }
    }
}
