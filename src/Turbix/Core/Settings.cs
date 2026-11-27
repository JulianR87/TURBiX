using System;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>Preferencias del usuario, guardadas en HKCU\Software\Turbix.</summary>
    public static class Settings
    {
        private const string Key = @"Software\TURBiX";

        /// <summary>true = no leer ni guardar nada (modo de capturas).</summary>
        public static bool Disabled { get; set; }

        public static int GetInt(string name, int fallback)
        {
            if (Disabled) return fallback;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                {
                    object v = key?.GetValue(name);
                    return v == null ? fallback : Convert.ToInt32(v);
                }
            }
            catch
            {
                return fallback;
            }
        }

        public static bool GetBool(string name, bool fallback) => GetInt(name, fallback ? 1 : 0) != 0;

        public static void SetInt(string name, int value)
        {
            if (Disabled) return;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(Key))
                    key?.SetValue(name, value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("core.settings.saveFailed", name, ex.Message));
            }
        }

        public static void SetBool(string name, bool value) => SetInt(name, value ? 1 : 0);

        public static string GetString(string name)
        {
            if (Disabled) return null;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                    return key?.GetValue(name) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Guarda un texto; null o vacío borra el valor (vuelve al predeterminado).</summary>
        public static void SetString(string name, string value)
        {
            if (Disabled) return;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (key == null) return;
                    if (string.IsNullOrEmpty(value)) key.DeleteValue(name, false);
                    else key.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("core.settings.saveFailed", name, ex.Message));
            }
        }

        /// <summary>Borra preferencias (para «Restablecer»). Nunca toca copias de seguridad ni registros.</summary>
        public static void Delete(params string[] names)
        {
            if (Disabled) return;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key, true))
                    if (key != null)
                        foreach (var n in names) key.DeleteValue(n, false);
            }
            catch (Exception ex)
            {
                Logger.Warn(L.F("core.settings.saveFailed", string.Join(", ", names), ex.Message));
            }
        }
    }
}
