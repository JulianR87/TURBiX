using System;
using System.Management;
using Microsoft.Win32;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>Crea puntos de restauración del sistema mediante WMI (SystemRestore).</summary>
    public static class RestorePoint
    {
        private const string FrequencyKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyName = "SystemRestorePointCreationFrequency";

        public static bool Create(string description)
        {
            Logger.Info(L.T("core.restorePoint.creating"));
            // Windows solo permite un punto cada 24 h: se levanta el límite temporalmente.
            object old = RegistryHelper.GetValue(RegistryHive.LocalMachine, FrequencyKey, FrequencyName, out _);
            try
            {
                RegistryHelper.SetValue(RegistryHive.LocalMachine, FrequencyKey, FrequencyName, 0, RegistryValueKind.DWord);

                var scope = new ManagementScope(@"\\.\root\default");
                using (var cls = new ManagementClass(scope, new ManagementPath("SystemRestore"), null))
                {
                    ManagementBaseObject args = cls.GetMethodParameters("CreateRestorePoint");
                    args["Description"] = description;
                    args["RestorePointType"] = 12;   // MODIFY_SETTINGS
                    args["EventType"] = 100;         // BEGIN_SYSTEM_CHANGE
                    ManagementBaseObject result = cls.InvokeMethod("CreateRestorePoint", args, null);
                    uint code = Convert.ToUInt32(result?["ReturnValue"] ?? 1u);
                    if (code == 0)
                    {
                        Logger.Success(L.T("core.restorePoint.created"));
                        return true;
                    }
                    Logger.Warn(L.F("core.restorePoint.failedCode", code));
                }
            }
            catch (Exception ex)
            {
                // En equipos sin «Protección del sistema» Windows suele fallar sin dar ningún motivo.
                string reason = string.IsNullOrWhiteSpace(ex.Message)
                    ? L.F("core.restorePoint.noReason", ex.HResult.ToString("X8"))
                    : ex.Message.Trim();
                Logger.Warn(L.F("core.restorePoint.failed", reason));
            }
            finally
            {
                try
                {
                    if (old == null) RegistryHelper.DeleteValue(RegistryHive.LocalMachine, FrequencyKey, FrequencyName);
                    else RegistryHelper.SetValue(RegistryHive.LocalMachine, FrequencyKey, FrequencyName, old, RegistryValueKind.DWord);
                }
                catch
                {
                    // Sin importancia.
                }
            }
            Logger.Info(L.T("core.restorePoint.continuing"));
            return false;
        }
    }
}
