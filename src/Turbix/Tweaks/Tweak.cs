using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Tweaks
{
    public enum TweakActionType { Registry, Service, Task, PowerPlan }

    public enum TweakState { Unknown, Applied, Partial, NotApplied, NotApplicable }

    public sealed class TweakAction
    {
        public TweakActionType Type { get; private set; }
        public RegistryHive Hive { get; private set; }
        public string SubKey { get; private set; }
        public string ValueName { get; private set; }
        public object Value { get; private set; }
        public RegistryValueKind Kind { get; private set; }
        public string ServiceName { get; private set; }
        public string TaskFolder { get; private set; }
        public string TaskName { get; private set; }

        /// <summary>
        /// true si, cuando el valor no existe, Windows ya se comporta como si tuviera este valor
        /// (por ejemplo, el Modo de juego viene activado de fábrica sin que exista la clave).
        /// </summary>
        public bool AbsentIsDefault { get; private set; }

        /// <summary>Marca que la ausencia del valor equivale a este valor (ver <see cref="AbsentIsDefault"/>).</summary>
        public TweakAction OrAbsent()
        {
            AbsentIsDefault = true;
            return this;
        }

        public static TweakAction Reg(string path, string name, object value, RegistryValueKind kind = RegistryValueKind.DWord)
        {
            RegistryHelper.Parse(path, out var hive, out var subKey);
            return new TweakAction
            {
                Type = TweakActionType.Registry, Hive = hive, SubKey = subKey,
                ValueName = name, Value = value, Kind = kind
            };
        }

        public static TweakAction Str(string path, string name, string value) => Reg(path, name, value, RegistryValueKind.String);

        public static TweakAction Svc(string name) => new TweakAction { Type = TweakActionType.Service, ServiceName = name };

        public static TweakAction Task(string folder, string name) =>
            new TweakAction { Type = TweakActionType.Task, TaskFolder = folder, TaskName = name };

        /// <summary>Activa un plan de energía (Value = GUID del plan).</summary>
        public static TweakAction Power(string schemeGuid) =>
            new TweakAction { Type = TweakActionType.PowerPlan, Value = schemeGuid };

        public string Describe()
        {
            switch (Type)
            {
                case TweakActionType.Registry: return $"{RegistryHelper.HiveName(Hive)}\\{SubKey}\\{ValueName}";
                case TweakActionType.Service: return L.F("tweak.action.service", ServiceName);
                case TweakActionType.PowerPlan: return L.T("tweak.action.powerPlan");
                default: return L.F("tweak.action.task", TaskFolder + TaskName);
            }
        }
    }

    public sealed class Tweak
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }

        /// <summary>Una línea visible en la lista.</summary>
        public string Summary { get; set; }

        /// <summary>Explicación del botón «?».</summary>
        public string Help { get; set; }

        /// <summary>Efecto secundario a tener en cuenta (opcional).</summary>
        public string Warning { get; set; }

        /// <summary>true = incluido en «Recomendado»; false = solo en «Máximo».</summary>
        public bool Recommended { get; set; } = true;

        /// <summary>Si devuelve false el ajuste no aplica a este equipo (p. ej. sin NVIDIA).</summary>
        public Func<bool> Condition { get; set; }

        public List<TweakAction> Actions { get; set; } = new List<TweakAction>();
    }

    public sealed class TweakCategory
    {
        public string Name { get; set; }
        public string Glyph { get; set; }
        public string Help { get; set; }
    }
}
