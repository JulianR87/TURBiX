using System;
using System.Collections.Generic;

namespace Turbix.Cleanup
{
    /// <summary>Conjunto de rutas que pertenecen a un mismo programa.</summary>
    public sealed class CleanupGroup
    {
        public string Name { get; set; }

        /// <summary>Si alguno de estos procesos está abierto, el grupo se omite al limpiar.</summary>
        public string[] Processes { get; set; } = new string[0];

        public List<string> Paths { get; set; } = new List<string>();
        public string[] Exclude { get; set; }
    }

    public sealed class CleanupItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Help { get; set; }
        public string Warning { get; set; }
        public string Glyph { get; set; }
        public bool Recommended { get; set; }

        /// <summary>Archivos modificados hace menos horas se conservan (salvo en modo agresivo).</summary>
        public int MinAgeHours { get; set; }

        public List<CleanupGroup> Groups { get; set; } = new List<CleanupGroup>();

        public Action Before { get; set; }
        public Action After { get; set; }

        /// <summary>Acción especial en lugar de borrar rutas. Devuelve bytes liberados o null.</summary>
        public Func<long?> Custom { get; set; }

        /// <summary>Medición especial. Devuelve bytes o null si no se puede calcular.</summary>
        public Func<long?> CustomMeasure { get; set; }
    }

    public sealed class CleanupResult
    {
        public string Id { get; set; }
        public long? Bytes { get; set; }
        public int Files { get; set; }
        public int Failed { get; set; }
        public List<string> SkippedApps { get; } = new List<string>();
    }
}
