using System;
using System.Collections.Generic;
using Turbix.Localization;

namespace Turbix.Core
{
    public sealed class TaskRef
    {
        public string Folder { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Tareas programadas mediante la API COM del Programador de tareas de Windows
    /// (Schedule.Service), incluida en el sistema.
    /// </summary>
    public static class TaskOps
    {
        private const int TASK_ENUM_HIDDEN = 1;

        private static dynamic Connect()
        {
            Type t = Type.GetTypeFromProgID("Schedule.Service", true);
            dynamic service = Activator.CreateInstance(t);
            service.Connect();
            return service;
        }

        public static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "\\";
            string f = folder.Trim();
            if (!f.StartsWith("\\")) f = "\\" + f;
            if (f.Length > 1) f = f.TrimEnd('\\');
            return f;
        }

        /// <summary>Lista todas las tareas de una carpeta (vacío si la carpeta no existe).</summary>
        public static List<TaskRef> ListFolder(string folder)
        {
            var result = new List<TaskRef>();
            string path = NormalizeFolder(folder);
            try
            {
                dynamic service = Connect();
                dynamic f = service.GetFolder(path);
                dynamic tasks = f.GetTasks(TASK_ENUM_HIDDEN);
                int count = tasks.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic task = tasks[i];
                    result.Add(new TaskRef { Folder = path, Name = (string)task.Name, Enabled = (bool)task.Enabled });
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // Carpeta inexistente en esta versión de Windows.
            }
            catch (System.IO.DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                // Otro tipo de fallo (Programador de tareas detenido, acceso denegado): que quede constancia.
                Logger.Warn(L.F("core.tasks.readFailed", path, ex.Message));
            }
            return result;
        }

        public static void SetEnabled(string folder, string name, bool enabled)
        {
            dynamic service = Connect();
            dynamic f = service.GetFolder(NormalizeFolder(folder));
            dynamic task = f.GetTask(name);
            task.Enabled = enabled;
        }
    }

    /// <summary>Caché de carpetas de tareas para consultar el estado de muchos ajustes de una vez.</summary>
    public sealed class TaskCache
    {
        private readonly Dictionary<string, List<TaskRef>> _folders =
            new Dictionary<string, List<TaskRef>>(StringComparer.OrdinalIgnoreCase);

        public List<TaskRef> Find(string folder, string namePattern)
        {
            string key = TaskOps.NormalizeFolder(folder);
            if (!_folders.TryGetValue(key, out var list))
            {
                list = TaskOps.ListFolder(key);
                _folders[key] = list;
            }
            return list.FindAll(t => Glob.IsMatch(t.Name, namePattern));
        }
    }
}
