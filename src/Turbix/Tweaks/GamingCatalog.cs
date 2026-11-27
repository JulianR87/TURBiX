using System.Collections.Generic;
using Turbix.Core;
using Turbix.Localization;
using static Turbix.Tweaks.TweakAction;

namespace Turbix.Tweaks
{
    /// <summary>
    /// Ajustes de rendimiento para juegos.
    ///
    /// Criterio: solo entra lo que tiene un efecto real y documentado. Se dejan fuera a propósito
    /// los «trucos» populares que no hacen nada o que rompen cosas:
    ///   - Desactivar los servicios de Xbox (rompe Game Pass, logros y el inicio de sesión en juegos).
    ///   - Desactivar SysMain/Superfetch o el archivo de paginación (causa cierres y cargas más lentas).
    ///   - Cambios de bcdedit (HPET, tscsyncpolicy...) o del temporizador: efecto nulo o negativo según el equipo.
    ///   - Desactivar la seguridad (Defender, aislamiento del núcleo): Turbix no toca la seguridad.
    ///   - Ajustes de red tipo «TcpAckFrequency»: no bajan el ping en juegos modernos (usan UDP).
    ///
    /// Los textos están en Strings\perf.*.txt (claves gaming.*).
    /// </summary>
    public static class GamingCatalog
    {
        private const string GameBar = @"HKCU\Software\Microsoft\GameBar";
        private const string Mmcss = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string Access = @"HKCU\Control Panel\Accessibility";

        public static readonly string CatGame = L.T("gaming.cat.game");
        public static readonly string CatInput = L.T("gaming.cat.input");
        public static readonly string CatPower = L.T("gaming.cat.power");
        public static readonly string CatBackground = L.T("gaming.cat.background");

        public static readonly IReadOnlyList<TweakCategory> Categories = new List<TweakCategory>
        {
            new TweakCategory { Name = CatGame, Glyph = "", Help = L.T("gaming.cat.game.help") },
            new TweakCategory { Name = CatInput, Glyph = "", Help = L.T("gaming.cat.input.help") },
            new TweakCategory { Name = CatPower, Glyph = "", Help = L.T("gaming.cat.power.help") },
            new TweakCategory { Name = CatBackground, Glyph = "", Help = L.T("gaming.cat.background.help") },
        };

        private static List<Tweak> _all;

        public static IReadOnlyList<Tweak> All => _all ?? (_all = Build());

        private static List<Tweak> Build() => new List<Tweak>
        {
            // ================================================================ Funciones de juego
            new Tweak
            {
                Id = "game-mode", Category = CatGame,
                Name = L.T("gaming.gameMode.name"),
                Summary = L.T("gaming.gameMode.summary"),
                Help = L.T("gaming.gameMode.help"),
                Actions =
                {
                    // Sin estos valores, Windows 10 (1703+) y 11 ya tienen el Modo de juego activado.
                    Reg(GameBar, "AutoGameModeEnabled", 1).OrAbsent(),
                    Reg(GameBar, "AllowAutoGameMode", 1).OrAbsent(),
                }
            },
            new Tweak
            {
                Id = "game-dvr", Category = CatGame,
                Name = L.T("gaming.gameDvr.name"),
                Summary = L.T("gaming.gameDvr.summary"),
                Help = L.T("gaming.gameDvr.help"),
                Warning = L.T("gaming.gameDvr.warning"),
                Actions =
                {
                    Reg(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0),
                }
            },
            new Tweak
            {
                Id = "game-bar-popups", Category = CatGame,
                Name = L.T("gaming.gameBarPopups.name"),
                Summary = L.T("gaming.gameBarPopups.summary"),
                Help = L.T("gaming.gameBarPopups.help"),
                Actions =
                {
                    Reg(GameBar, "ShowStartupPanel", 0),
                    Reg(GameBar, "UseNexusForGameBarEnabled", 0),
                    Reg(GameBar, "GamePanelStartupTipIndex", 3),
                }
            },

            // ================================================================ Teclado y ratón
            new Tweak
            {
                Id = "sticky-keys", Category = CatInput,
                Name = L.T("gaming.stickyKeys.name"),
                Summary = L.T("gaming.stickyKeys.summary"),
                Help = L.T("gaming.stickyKeys.help"),
                Warning = L.T("gaming.stickyKeys.warning"),
                Actions =
                {
                    Str(Access + @"\StickyKeys", "Flags", "506"),
                    Str(Access + @"\Keyboard Response", "Flags", "122"),
                    Str(Access + @"\ToggleKeys", "Flags", "58"),
                }
            },
            new Tweak
            {
                Id = "mouse-accel", Category = CatInput, Recommended = false,
                Name = L.T("gaming.mouseAccel.name"),
                Summary = L.T("gaming.mouseAccel.summary"),
                Help = L.T("gaming.mouseAccel.help"),
                Warning = L.T("gaming.mouseAccel.warning"),
                Actions =
                {
                    Str(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0"),
                    Str(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0"),
                    Str(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0"),
                }
            },

            // ================================================================ Gráficos y energía
            new Tweak
            {
                Id = "mmcss-games", Category = CatPower,
                Name = L.T("gaming.mmcss.name"),
                Summary = L.T("gaming.mmcss.summary"),
                Help = L.T("gaming.mmcss.help"),
                Actions =
                {
                    Reg(Mmcss, "SystemResponsiveness", 10),
                    Reg(Mmcss + @"\Tasks\Games", "GPU Priority", 8),
                    Reg(Mmcss + @"\Tasks\Games", "Priority", 6),
                    Str(Mmcss + @"\Tasks\Games", "Scheduling Category", "High"),
                    Str(Mmcss + @"\Tasks\Games", "SFIO Priority", "High"),
                }
            },
            new Tweak
            {
                Id = "hags", Category = CatPower, Recommended = false,
                Name = L.T("gaming.hags.name"),
                Summary = L.T("gaming.hags.summary"),
                Help = L.T("gaming.hags.help"),
                Warning = L.T("gaming.hags.warning"),
                Condition = () => SystemInfo.GetOs().Build >= 19041,
                Actions =
                {
                    Reg(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2),
                }
            },
            new Tweak
            {
                Id = "power-plan", Category = CatPower, Recommended = false,
                Name = L.T("gaming.powerPlan.name"),
                Summary = L.T("gaming.powerPlan.summary"),
                Help = L.T("gaming.powerPlan.help"),
                Warning = L.T("gaming.powerPlan.warning"),
                Actions = { Power(PowerOps.HighPerformance) }
            },
            new Tweak
            {
                Id = "power-throttling", Category = CatPower, Recommended = false,
                Name = L.T("gaming.powerThrottling.name"),
                Summary = L.T("gaming.powerThrottling.summary"),
                Help = L.T("gaming.powerThrottling.help"),
                Warning = L.T("gaming.powerThrottling.warning"),
                Actions =
                {
                    Reg(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1),
                }
            },

            // ================================================================ Segundo plano
            new Tweak
            {
                Id = "edge-background", Category = CatBackground,
                Name = L.T("gaming.edgeBackground.name"),
                Summary = L.T("gaming.edgeBackground.summary"),
                Help = L.T("gaming.edgeBackground.help"),
                Warning = L.T("gaming.edgeBackground.warning"),
                Actions =
                {
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0),
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0),
                }
            },
            new Tweak
            {
                Id = "background-apps", Category = CatBackground, Recommended = false,
                Name = L.T("gaming.backgroundApps.name"),
                Summary = L.T("gaming.backgroundApps.summary"),
                Help = L.T("gaming.backgroundApps.help"),
                Warning = L.T("gaming.backgroundApps.warning"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0),
                }
            },
            new Tweak
            {
                Id = "widgets", Category = CatBackground, Recommended = false,
                Name = L.T("gaming.widgets.name"),
                Summary = L.T("gaming.widgets.summary"),
                Help = L.T("gaming.widgets.help"),
                Warning = L.T("gaming.widgets.warning"),
                Actions =
                {
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0),
                }
            },
            new Tweak
            {
                Id = "visual-effects", Category = CatBackground, Recommended = false,
                Name = L.T("gaming.visualEffects.name"),
                Summary = L.T("gaming.visualEffects.summary"),
                Help = L.T("gaming.visualEffects.help"),
                Warning = L.T("gaming.visualEffects.warning"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0),
                    Str(@"HKCU\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0"),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0),
                }
            },
        };
    }
}
