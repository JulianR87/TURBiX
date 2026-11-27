using System;
using System.ComponentModel;
using System.ServiceProcess;
using Microsoft.Win32;

namespace Turbix.Core
{
    public sealed class ServiceStartInfo
    {
        public int Start { get; set; }      // 0 boot, 1 system, 2 auto, 3 manual, 4 deshabilitado
        public bool Delayed { get; set; }
    }

    public static class ServiceOps
    {
        public const int Automatic = 2;
        public const int Disabled = 4;

        public static ServiceStartInfo GetStartInfo(string name)
        {
            string key = @"SYSTEM\CurrentControlSet\Services\" + name;
            object start = RegistryHelper.GetValue(RegistryHive.LocalMachine, key, "Start", out _);
            if (start == null) return null;
            object delayed = RegistryHelper.GetValue(RegistryHive.LocalMachine, key, "DelayedAutostart", out _);
            return new ServiceStartInfo
            {
                Start = Convert.ToInt32(start),
                Delayed = delayed != null && Convert.ToInt32(delayed) == 1
            };
        }

        /// <summary>Cambia el tipo de inicio a través del Administrador de servicios (efecto inmediato).</summary>
        public static void SetStartType(string name, int start, bool delayed)
        {
            IntPtr scm = Native.OpenSCManager(null, null, Native.SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero) throw new Win32Exception();
            try
            {
                IntPtr svc = Native.OpenService(scm, name, Native.SERVICE_CHANGE_CONFIG | Native.SERVICE_QUERY_CONFIG);
                if (svc == IntPtr.Zero) throw new Win32Exception();
                try
                {
                    if (!Native.ChangeServiceConfig(svc, Native.SERVICE_NO_CHANGE, (uint)start, Native.SERVICE_NO_CHANGE,
                            null, null, IntPtr.Zero, null, null, null, null))
                        throw new Win32Exception();

                    if (start == Automatic)
                    {
                        var info = new Native.SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = delayed };
                        Native.ChangeServiceConfig2(svc, Native.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref info);
                    }
                }
                finally
                {
                    Native.CloseServiceHandle(svc);
                }
            }
            finally
            {
                Native.CloseServiceHandle(scm);
            }
        }

        public static bool IsRunning(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    return sc.Status != ServiceControllerStatus.Stopped;
            }
            catch
            {
                return false;
            }
        }

        public static bool Stop(string name, int timeoutSeconds = 20)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) return true;
                    if (!sc.CanStop) return false;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeoutSeconds));
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Start(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
                }
            }
            catch
            {
                // Algunos servicios solo arrancan bajo demanda; no es un error.
            }
        }
    }
}
