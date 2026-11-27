using System.Collections.Generic;
using Turbix.Core;
using Turbix.Localization;
using static Turbix.Tweaks.TweakAction;

namespace Turbix.Tweaks
{
    /// <summary>
    /// Ajustes de Windows que los usuarios piden una y otra vez en los foros: quitar molestias
    /// de Windows 11 y corregir comportamientos que causan lentitud en equipos modestos.
    /// Usan el mismo motor que privacidad: copia de seguridad y reversión.
    /// </summary>
    public static class WindowsCatalog
    {
        private const string Adv = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        // Nombres ya traducidos: se muestran y además unen cada ajuste con su categoría.
        public static string CatInterface => L.T("wintweak.cat.interface");
        public static string CatBoot => L.T("wintweak.cat.boot");
        public static string CatTaskbar => L.T("wintweak.cat.taskbar");
        public static string CatServices => L.T("wintweak.cat.services");

        private static List<TweakCategory> _categories;

        public static IReadOnlyList<TweakCategory> Categories => _categories ?? (_categories = new List<TweakCategory>
        {
            new TweakCategory { Name = CatInterface, Glyph = "",
                Help = L.T("wintweak.cat.interface.help") },
            new TweakCategory { Name = CatBoot, Glyph = "",
                Help = L.T("wintweak.cat.boot.help") },
            new TweakCategory { Name = CatTaskbar, Glyph = "\uE7C4",
                Help = L.T("wintweak.cat.taskbar.help") },
            new TweakCategory { Name = CatServices, Glyph = "\uE912",
                Help = L.T("wintweak.cat.services.help") },
        });

        private static List<Tweak> _all;

        public static IReadOnlyList<Tweak> All => _all ?? (_all = Build());

        private static bool IsWindows11 => SystemInfo.GetOs().Build >= 22000;
        private const string Search = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search";

        private static List<Tweak> Build() => new List<Tweak>
        {
            // ================================================================ Molestias
            new Tweak
            {
                Id = "file-extensions", Category = CatInterface,
                Name = L.T("wintweak.fileExtensions.name"),
                Summary = L.T("wintweak.fileExtensions.summary"),
                Help = L.T("wintweak.fileExtensions.help"),
                Actions = { Reg(Adv, "HideFileExt", 0) }
            },
            new Tweak
            {
                Id = "search-highlights", Category = CatInterface,
                Name = L.T("wintweak.searchHighlights.name"),
                Summary = L.T("wintweak.searchHighlights.summary"),
                Help = L.T("wintweak.searchHighlights.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0),
                }
            },
            new Tweak
            {
                Id = "start-account-notifications", Category = CatInterface,
                Name = L.T("wintweak.startAccountNotifications.name"),
                Summary = L.T("wintweak.startAccountNotifications.summary"),
                Help = L.T("wintweak.startAccountNotifications.help"),
                Condition = () => IsWindows11,
                Actions = { Reg(Adv, "Start_AccountNotifications", 0) }
            },
            new Tweak
            {
                Id = "taskbar-end-task", Category = CatInterface,
                Name = L.T("wintweak.taskbarEndTask.name"),
                Summary = L.T("wintweak.taskbarEndTask.summary"),
                Help = L.T("wintweak.taskbarEndTask.help"),
                Condition = () => SystemInfo.GetOs().Build >= 22631,
                Actions = { Reg(Adv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1) }
            },
            new Tweak
            {
                Id = "classic-context-menu", Category = CatInterface, Recommended = false,
                Name = L.T("wintweak.classicContextMenu.name"),
                Summary = L.T("wintweak.classicContextMenu.summary"),
                Help = L.T("wintweak.classicContextMenu.help"),
                Warning = L.T("wintweak.classicContextMenu.warning"),
                Condition = () => IsWindows11,
                Actions =
                {
                    Str(@"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", ""),
                }
            },
            new Tweak
            {
                Id = "explorer-this-pc", Category = CatInterface, Recommended = false,
                Name = L.T("wintweak.explorerThisPc.name"),
                Summary = L.T("wintweak.explorerThisPc.summary"),
                Help = L.T("wintweak.explorerThisPc.help"),
                Actions = { Reg(Adv, "LaunchTo", 1) }
            },

            // ================================================================ Arranque y disco
            new Tweak
            {
                Id = "storage-sense", Category = CatBoot,
                Name = L.T("wintweak.storageSense.name"),
                Summary = L.T("wintweak.storageSense.summary"),
                Help = L.T("wintweak.storageSense.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1),
                }
            },
            new Tweak
            {
                Id = "fast-startup", Category = CatBoot, Recommended = false,
                Name = L.T("wintweak.fastStartup.name"),
                Summary = L.T("wintweak.fastStartup.summary"),
                Help = L.T("wintweak.fastStartup.help"),
                Warning = L.T("wintweak.fastStartup.warning"),
                Actions =
                {
                    Reg(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0),
                }
            },
            new Tweak
            {
                Id = "sysmain-hdd", Category = CatBoot, Recommended = false,
                Name = L.T("wintweak.sysmainHdd.name"),
                Summary = L.T("wintweak.sysmainHdd.summary"),
                Help = L.T("wintweak.sysmainHdd.help"),
                Warning = L.T("wintweak.sysmainHdd.warning"),
                Condition = DiskInfo.SystemDiskIsHdd,
                Actions = { Svc("SysMain") }
            },
            new Tweak
            {
                Id = "hibernation", Category = CatBoot, Recommended = false,
                Name = L.T("wintweak.hibernation.name"),
                Summary = L.T("wintweak.hibernation.summary"),
                Help = L.T("wintweak.hibernation.help"),
                Warning = L.T("wintweak.hibernation.warning"),
                Actions =
                {
                    Reg(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0),
                    Reg(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabledDefault", 0),
                }
            },

            // ================================================================ Barra de tareas e Inicio
            new Tweak
            {
                Id = "edge-desktop-shortcut", Category = CatTaskbar,
                Name = L.T("wintweak.edgeShortcut.name"),
                Summary = L.T("wintweak.edgeShortcut.summary"),
                Help = L.T("wintweak.edgeShortcut.help"),
                Actions = { Reg(@"HKLM\SOFTWARE\Policies\Microsoft\EdgeUpdate", "CreateDesktopShortcutDefault", 0) }
            },
            new Tweak
            {
                Id = "meet-now", Category = CatTaskbar,
                Name = L.T("wintweak.meetNow.name"),
                Summary = L.T("wintweak.meetNow.summary"),
                Help = L.T("wintweak.meetNow.help"),
                Condition = () => !IsWindows11,
                Actions = { Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1) }
            },
            new Tweak
            {
                Id = "teams-chat", Category = CatTaskbar,
                Name = L.T("wintweak.teamsChat.name"),
                Summary = L.T("wintweak.teamsChat.summary"),
                Help = L.T("wintweak.teamsChat.help"),
                Condition = () => IsWindows11 && SystemInfo.GetOs().Build < 22631,
                Actions = { Reg(Adv, "TaskbarMn", 0) }
            },
            new Tweak
            {
                Id = "cortana-button", Category = CatTaskbar,
                Name = L.T("wintweak.cortana.name"),
                Summary = L.T("wintweak.cortana.summary"),
                Help = L.T("wintweak.cortana.help"),
                Condition = () => !IsWindows11,
                Actions = { Reg(Adv, "ShowCortanaButton", 0) }
            },
            new Tweak
            {
                Id = "task-view-button", Category = CatTaskbar, Recommended = false,
                Name = L.T("wintweak.taskView.name"),
                Summary = L.T("wintweak.taskView.summary"),
                Help = L.T("wintweak.taskView.help"),
                Actions = { Reg(Adv, "ShowTaskViewButton", 0) }
            },
            new Tweak
            {
                Id = "search-icon", Category = CatTaskbar, Recommended = false,
                Name = L.T("wintweak.searchIcon.name"),
                Summary = L.T("wintweak.searchIcon.summary"),
                Help = L.T("wintweak.searchIcon.help"),
                Actions = { Reg(Search, "SearchboxTaskbarMode", 1) }
            },
            new Tweak
            {
                Id = "taskbar-left", Category = CatTaskbar, Recommended = false,
                Name = L.T("wintweak.taskbarLeft.name"),
                Summary = L.T("wintweak.taskbarLeft.summary"),
                Help = L.T("wintweak.taskbarLeft.help"),
                Condition = () => IsWindows11,
                Actions = { Reg(Adv, "TaskbarAl", 0) }
            },
            new Tweak
            {
                Id = "recent-files", Category = CatTaskbar, Recommended = false,
                Name = L.T("wintweak.recentFiles.name"),
                Summary = L.T("wintweak.recentFiles.summary"),
                Help = L.T("wintweak.recentFiles.help"),
                Warning = L.T("wintweak.recentFiles.warning"),
                Actions =
                {
                    Reg(Adv, "Start_TrackDocs", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0),
                }
            },
            new Tweak
            {
                Id = "hidden-files", Category = CatTaskbar, Recommended = false,
                Name = L.T("wintweak.hiddenFiles.name"),
                Summary = L.T("wintweak.hiddenFiles.summary"),
                Help = L.T("wintweak.hiddenFiles.help"),
                Actions = { Reg(Adv, "Hidden", 1) }
            },

            // ================================================================ Servicios opcionales
            Service("fax", "Fax", true,
                L.T("wintweak.svc.fax.name"), L.T("wintweak.svc.fax.summary"), L.T("wintweak.svc.fax.help")),
            Service("retail-demo", "RetailDemo", true,
                L.T("wintweak.svc.retailDemo.name"), L.T("wintweak.svc.retailDemo.summary"), L.T("wintweak.svc.retailDemo.help")),
            Service("maps-broker", "MapsBroker", true,
                L.T("wintweak.svc.maps.name"), L.T("wintweak.svc.maps.summary"), L.T("wintweak.svc.maps.help"),
                L.T("wintweak.svc.maps.warning")),
            Service("wmp-sharing", "WMPNetworkSvc", true,
                L.T("wintweak.svc.wmp.name"), L.T("wintweak.svc.wmp.summary"), L.T("wintweak.svc.wmp.help")),
            Service("insider", "wisvc", false,
                L.T("wintweak.svc.insider.name"), L.T("wintweak.svc.insider.summary"), L.T("wintweak.svc.insider.help"),
                L.T("wintweak.svc.insider.warning")),
            Service("pca", "PcaSvc", false,
                L.T("wintweak.svc.pca.name"), L.T("wintweak.svc.pca.summary"), L.T("wintweak.svc.pca.help"),
                L.T("wintweak.svc.pca.warning")),
            Service("print-spooler", "Spooler", false,
                L.T("wintweak.svc.spooler.name"), L.T("wintweak.svc.spooler.summary"), L.T("wintweak.svc.spooler.help"),
                L.T("wintweak.svc.spooler.warning")),
            Service("windows-search", "WSearch", false,
                L.T("wintweak.svc.search.name"), L.T("wintweak.svc.search.summary"), L.T("wintweak.svc.search.help"),
                L.T("wintweak.svc.search.warning")),
        };

        private static Tweak Service(string id, string service, bool recommended, string name, string summary, string help, string warning = null) =>
            new Tweak
            {
                Id = "svc-" + id, Category = CatServices, Recommended = recommended,
                Name = name, Summary = summary, Help = help, Warning = warning,
                Actions = { Svc(service) }
            };

    }
}
