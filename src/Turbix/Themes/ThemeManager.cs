using System;
using System.Windows;
using Microsoft.Win32;
using Turbix.Core;
using Turbix.Localization;

namespace Turbix.Themes
{
    public enum ThemeMode { System, Light, Dark }

    /// <summary>
    /// Cambia entre la paleta clara y la oscura en tiempo de ejecución.
    /// En modo «Sistema» sigue la opción «Modo de aplicación» de Windows
    /// (HKCU\...\Themes\Personalize\AppsUseLightTheme).
    /// </summary>
    public static class ThemeManager
    {
        private const string SettingsKey = @"Software\TURBiX";
        private const string SettingsValue = "Theme";
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private static readonly Uri DarkUri = new Uri("/Turbix;component/Themes/Colors.Dark.xaml", UriKind.Relative);
        private static readonly Uri LightUri = new Uri("/Turbix;component/Themes/Colors.Light.xaml", UriKind.Relative);

        private static ResourceDictionary _palette;
        private static bool _persist = true;

        /// <summary>Preferencia elegida por el usuario.</summary>
        public static ThemeMode Mode { get; private set; } = ThemeMode.System;

        /// <summary>Paleta que se está mostrando realmente.</summary>
        public static bool IsLight { get; private set; }

        /// <summary>Se dispara cada vez que cambia la paleta visible.</summary>
        public static event EventHandler Changed;

        /// <summary>Lee la preferencia guardada y aplica la paleta. <paramref name="persist"/> = false no guarda nada.</summary>
        public static void Initialize(bool persist)
        {
            _persist = persist;
            Mode = persist ? LoadMode() : ThemeMode.System;
            Apply();
        }

        public static void SetMode(ThemeMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            if (_persist) SaveMode(mode);
            Logger.Info(L.F("theme.log.changed", Describe(mode)));
            Apply();
        }

        /// <summary>Llamar cuando Windows avisa de un cambio de colores (WM_SETTINGCHANGE «ImmersiveColorSet»).</summary>
        public static void OnSystemThemeChanged()
        {
            if (Mode == ThemeMode.System) Apply();
        }

        /// <summary>Solo para generar capturas: fuerza una paleta sin guardar la preferencia.</summary>
        public static void Preview(ThemeMode mode)
        {
            Mode = mode;
            Apply();
        }

        public static string Describe(ThemeMode mode)
        {
            switch (mode)
            {
                case ThemeMode.Light: return L.T("theme.log.light");
                case ThemeMode.Dark: return L.T("theme.log.dark");
                default: return L.T("theme.log.system");
            }
        }

        private static void Apply()
        {
            bool light = Mode == ThemeMode.Light || (Mode == ThemeMode.System && SystemUsesLightTheme());
            if (_palette != null && light == IsLight) return;

            var app = Application.Current;
            if (app == null) return;
            var merged = app.Resources.MergedDictionaries;

            // La paleta inicial (la oscura) viene declarada en App.xaml como primer diccionario.
            if (_palette == null && merged.Count > 0 && merged[0].Source != null &&
                merged[0].Source.OriginalString.IndexOf("Colors.", StringComparison.OrdinalIgnoreCase) >= 0)
                _palette = merged[0];

            var next = new ResourceDictionary { Source = light ? LightUri : DarkUri };
            if (_palette != null) merged.Remove(_palette);
            merged.Insert(0, next);
            _palette = next;
            IsLight = light;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool SystemUsesLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    object v = key?.GetValue("AppsUseLightTheme");
                    // Sin valor = Windows usa el modo claro (es el predeterminado).
                    return v == null || Convert.ToInt32(v) != 0;
                }
            }
            catch
            {
                return true;
            }
        }

        private static ThemeMode LoadMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    string s = key?.GetValue(SettingsValue) as string;
                    if (string.Equals(s, "light", StringComparison.OrdinalIgnoreCase)) return ThemeMode.Light;
                    if (string.Equals(s, "dark", StringComparison.OrdinalIgnoreCase)) return ThemeMode.Dark;
                }
            }
            catch
            {
            }
            return ThemeMode.System;
        }

        private static void SaveMode(ThemeMode mode)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    key?.SetValue(SettingsValue, mode.ToString().ToLowerInvariant(), RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("theme.log.saveFailed", ex.Message));
            }
        }
    }
}
