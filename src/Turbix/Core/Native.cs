using System;
using System.Runtime.InteropServices;

namespace Turbix.Core
{
    /// <summary>Llamadas nativas a la API de Windows (todas incluidas en el sistema).</summary>
    internal static class Native
    {
        // --- Red ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        internal const int SW_SHOWMINNOACTIVE = 7;

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DnsFlushResolverCache();

        // --- Sistema ---
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // --- Memoria ---
        public const int SystemMemoryListInformation = 80;
        public const int MemoryEmptyWorkingSets = 2;
        public const int MemoryPurgeStandbyList = 4;

        [DllImport("ntdll.dll")]
        public static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr process);

        // --- Privilegios ---
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const uint TOKEN_QUERY = 0x0008;
        public const uint SE_PRIVILEGE_ENABLED = 0x0002;
        public const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue(string systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disableAll,
            ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        // --- Energía ---
        [DllImport("powrprof.dll")]
        public static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll")]
        public static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr mem);

        // --- Sesión ---
        public const int WTSUserName = 5;

        [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytes);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr memory);

        // --- Ventana ---
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;

        // --- Shell ---
        [DllImport("shell32.dll")]
        public static extern void SHUpdateRecycleBinIcon();

        // --- Servicios ---
        public const uint SC_MANAGER_CONNECT = 0x0001;
        public const uint SERVICE_QUERY_CONFIG = 0x0001;
        public const uint SERVICE_CHANGE_CONFIG = 0x0002;
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        public const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_DELAYED_AUTO_START_INFO
        {
            [MarshalAs(UnmanagedType.Bool)] public bool fDelayedAutostart;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfig(IntPtr service, uint serviceType, uint startType, uint errorControl,
            string binaryPathName, string loadOrderGroup, IntPtr tagId, string dependencies,
            string serviceStartName, string password, string displayName);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, ref SERVICE_DELAYED_AUTO_START_INFO info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseServiceHandle(IntPtr handle);
    }
}
