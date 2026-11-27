using System.Collections.Generic;
using System.Linq;
using Turbix.Core;
using Turbix.Localization;
using static Turbix.Tweaks.TweakAction;

namespace Turbix.Tweaks
{
    /// <summary>
    /// Catálogo de ajustes de privacidad y telemetría.
    ///
    /// Principio del proyecto: Turbix NUNCA toca Windows Update, Microsoft Defender,
    /// SmartScreen, el firewall ni el archivo hosts. Desactivarlos deja el equipo inseguro
    /// o rompe las actualizaciones, y ningún ajuste de privacidad lo justifica.
    /// </summary>
    public static class TweakCatalog
    {
        private const string Pol = @"HKLM\SOFTWARE\Policies\Microsoft\Windows";
        private const string CuPol = @"HKCU\Software\Policies\Microsoft\Windows";
        private const string Cdm = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string Adv = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string EnvKey = @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        private const string Edge = @"HKLM\SOFTWARE\Policies\Microsoft\Edge";
        private const string Office = @"HKCU\Software\Policies\Microsoft\Office";

        // Nombres de categoría en el idioma de la interfaz. Los ajustes se agrupan comparando Tweak.Category con
        // TweakCategory.Name, así que ambos salen de la misma clave.
        public static string CatTelemetry => L.T("privacy.cat.telemetry");
        public static string CatPrivacy => L.T("privacy.cat.privacy");
        public static string CatAds => L.T("privacy.cat.ads");
        public static string CatAi => L.T("privacy.cat.ai");
        public static string CatApps => L.T("privacy.cat.apps");

        public static readonly IReadOnlyList<TweakCategory> Categories = new List<TweakCategory>
        {
            new TweakCategory { Name = CatTelemetry, Glyph = "",
                Help = L.T("privacy.cat.telemetry.help") },
            new TweakCategory { Name = CatPrivacy, Glyph = "",
                Help = L.T("privacy.cat.privacy.help") },
            new TweakCategory { Name = CatAds, Glyph = "",
                Help = L.T("privacy.cat.ads.help") },
            new TweakCategory { Name = CatAi, Glyph = "",
                Help = L.T("privacy.cat.ai.help") },
            new TweakCategory { Name = CatApps, Glyph = "",
                Help = L.T("privacy.cat.apps.help") },
        };

        private static List<Tweak> _all;

        public static IReadOnlyList<Tweak> All => _all ?? (_all = Build());

        public static Tweak Get(string id) => All.FirstOrDefault(t => t.Id == id);

        private static List<Tweak> Build() => new List<Tweak>
        {
            // ================================================================ Telemetría
            new Tweak
            {
                Id = "diagnostic-data", Category = CatTelemetry,
                Name = L.T("privacy.tweak.diagnosticData.name"),
                Summary = L.T("privacy.tweak.diagnosticData.summary"),
                Help = L.T("privacy.tweak.diagnosticData.help"),
                Actions =
                {
                    Reg(Pol + @"\DataCollection", "AllowTelemetry", 0),
                    Reg(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                    Reg(Pol + @"\DataCollection", "AllowDeviceNameInTelemetry", 0),
                    Reg(Pol + @"\DataCollection", "DoNotShowFeedbackNotifications", 1),
                    Reg(Pol + @"\DataCollection", "LimitDiagnosticLogCollection", 1),
                    Reg(Pol + @"\DataCollection", "LimitDumpCollection", 1),
                    Reg(Pol + @"\DataCollection", "DisableOneSettingsDownloads", 1),
                }
            },
            new Tweak
            {
                Id = "telemetry-services", Category = CatTelemetry,
                Name = L.T("privacy.tweak.telemetryServices.name"),
                Summary = L.T("privacy.tweak.telemetryServices.summary"),
                Help = L.T("privacy.tweak.telemetryServices.help"),
                Warning = L.T("privacy.tweak.telemetryServices.warning"),
                Actions = { Svc("DiagTrack"), Svc("dmwappushservice") }
            },
            new Tweak
            {
                Id = "telemetry-tasks", Category = CatTelemetry,
                Name = L.T("privacy.tweak.telemetryTasks.name"),
                Summary = L.T("privacy.tweak.telemetryTasks.summary"),
                Help = L.T("privacy.tweak.telemetryTasks.help"),
                Actions =
                {
                    Task(@"\Microsoft\Windows\Application Experience\", "Microsoft Compatibility Appraiser"),
                    Task(@"\Microsoft\Windows\Application Experience\", "ProgramDataUpdater"),
                    Task(@"\Microsoft\Windows\Application Experience\", "MareBackup"),
                    Task(@"\Microsoft\Windows\Customer Experience Improvement Program\", "Consolidator"),
                    Task(@"\Microsoft\Windows\Customer Experience Improvement Program\", "UsbCeip"),
                    Task(@"\Microsoft\Windows\Customer Experience Improvement Program\", "KernelCeipTask"),
                    Task(@"\Microsoft\Windows\Autochk\", "Proxy"),
                    Task(@"\Microsoft\Windows\DiskDiagnostic\", "Microsoft-Windows-DiskDiagnosticDataCollector"),
                    Task(@"\Microsoft\Windows\Device Information\", "Device"),
                    Task(@"\Microsoft\Windows\PI\", "Sqm-Tasks"),
                }
            },
            new Tweak
            {
                Id = "ceip-appcompat", Category = CatTelemetry,
                Name = L.T("privacy.tweak.ceip.name"),
                Summary = L.T("privacy.tweak.ceip.summary"),
                Help = L.T("privacy.tweak.ceip.help"),
                Actions =
                {
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0),
                    Reg(@"HKLM\SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable", 0),
                    Reg(Pol + @"\AppCompat", "AITEnable", 0),
                    Reg(Pol + @"\AppCompat", "DisableInventory", 1),
                    Reg(Pol + @"\AppCompat", "DisableUAR", 1),
                }
            },
            new Tweak
            {
                Id = "error-reporting", Category = CatTelemetry,
                Name = L.T("privacy.tweak.errorReporting.name"),
                Summary = L.T("privacy.tweak.errorReporting.summary"),
                Help = L.T("privacy.tweak.errorReporting.help"),
                Actions =
                {
                    Reg(Pol + @"\Windows Error Reporting", "Disabled", 1),
                    Reg(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
                    Reg(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "DontSendAdditionalData", 1),
                    Task(@"\Microsoft\Windows\Windows Error Reporting\", "QueueReporting"),
                }
            },
            new Tweak
            {
                Id = "feedback", Category = CatTelemetry,
                Name = L.T("privacy.tweak.feedback.name"),
                Summary = L.T("privacy.tweak.feedback.summary"),
                Help = L.T("privacy.tweak.feedback.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
                    Task(@"\Microsoft\Windows\Feedback\Siuf\", "DmClient"),
                    Task(@"\Microsoft\Windows\Feedback\Siuf\", "DmClientOnScenarioDownload"),
                }
            },

            // ================================================================ Privacidad
            new Tweak
            {
                Id = "advertising-id", Category = CatPrivacy,
                Name = L.T("privacy.tweak.advertisingId.name"),
                Summary = L.T("privacy.tweak.advertisingId.summary"),
                Help = L.T("privacy.tweak.advertisingId.help"),
                Actions =
                {
                    Reg(Pol + @"\AdvertisingInfo", "DisabledByGroupPolicy", 1),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                }
            },
            new Tweak
            {
                Id = "activity-history", Category = CatPrivacy,
                Name = L.T("privacy.tweak.activityHistory.name"),
                Summary = L.T("privacy.tweak.activityHistory.summary"),
                Help = L.T("privacy.tweak.activityHistory.help"),
                Actions =
                {
                    Reg(Pol + @"\System", "EnableActivityFeed", 0),
                    Reg(Pol + @"\System", "PublishUserActivities", 0),
                    Reg(Pol + @"\System", "UploadUserActivities", 0),
                }
            },
            new Tweak
            {
                Id = "tailored-experiences", Category = CatPrivacy,
                Name = L.T("privacy.tweak.tailoredExperiences.name"),
                Summary = L.T("privacy.tweak.tailoredExperiences.summary"),
                Help = L.T("privacy.tweak.tailoredExperiences.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0),
                    Reg(CuPol + @"\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1),
                }
            },
            new Tweak
            {
                Id = "inking-typing", Category = CatPrivacy,
                Name = L.T("privacy.tweak.inkingTyping.name"),
                Summary = L.T("privacy.tweak.inkingTyping.summary"),
                Help = L.T("privacy.tweak.inkingTyping.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                    Reg(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                    Reg(@"HKCU\Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                    Reg(@"HKCU\Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0),
                    Reg(@"HKCU\Software\Microsoft\Input\TIPC", "Enabled", 0),
                    Reg(Pol + @"\HandwritingErrorReports", "PreventHandwritingErrorReports", 1),
                    Reg(Pol + @"\TabletPC", "PreventHandwritingDataSharing", 1),
                }
            },
            new Tweak
            {
                Id = "language-list", Category = CatPrivacy,
                Name = L.T("privacy.tweak.languageList.name"),
                Summary = L.T("privacy.tweak.languageList.summary"),
                Help = L.T("privacy.tweak.languageList.help"),
                Actions = { Reg(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1) }
            },
            new Tweak
            {
                Id = "app-diagnostics", Category = CatPrivacy, Recommended = false,
                Name = L.T("privacy.tweak.appDiagnostics.name"),
                Summary = L.T("privacy.tweak.appDiagnostics.summary"),
                Help = L.T("privacy.tweak.appDiagnostics.help"),
                Actions =
                {
                    Str(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics", "Value", "Deny"),
                }
            },
            new Tweak
            {
                Id = "app-launch-tracking", Category = CatPrivacy, Recommended = false,
                Name = L.T("privacy.tweak.appLaunchTracking.name"),
                Summary = L.T("privacy.tweak.appLaunchTracking.summary"),
                Help = L.T("privacy.tweak.appLaunchTracking.help"),
                Warning = L.T("privacy.tweak.appLaunchTracking.warning"),
                Actions = { Reg(Adv, "Start_TrackProgs", 0) }
            },
            new Tweak
            {
                Id = "online-speech", Category = CatPrivacy, Recommended = false,
                Name = L.T("privacy.tweak.onlineSpeech.name"),
                Summary = L.T("privacy.tweak.onlineSpeech.summary"),
                Help = L.T("privacy.tweak.onlineSpeech.help"),
                Warning = L.T("privacy.tweak.onlineSpeech.warning"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0),
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", 0),
                }
            },
            new Tweak
            {
                Id = "location", Category = CatPrivacy, Recommended = false,
                Name = L.T("privacy.tweak.location.name"),
                Summary = L.T("privacy.tweak.location.summary"),
                Help = L.T("privacy.tweak.location.help"),
                Warning = L.T("privacy.tweak.location.warning"),
                Actions =
                {
                    Reg(Pol + @"\LocationAndSensors", "DisableLocation", 1),
                    Reg(Pol + @"\LocationAndSensors", "DisableWindowsLocationProvider", 1),
                }
            },

            // ================================================================ Anuncios
            new Tweak
            {
                Id = "web-search", Category = CatAds,
                Name = L.T("privacy.tweak.webSearch.name"),
                Summary = L.T("privacy.tweak.webSearch.summary"),
                Help = L.T("privacy.tweak.webSearch.help"),
                Actions =
                {
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0),
                    Reg(CuPol + @"\Explorer", "DisableSearchBoxSuggestions", 1),
                    Reg(Pol + @"\Windows Search", "AllowCortana", 0),
                    Reg(Pol + @"\Windows Search", "ConnectedSearchUseWeb", 0),
                    Reg(Pol + @"\Windows Search", "DisableWebSearch", 1),
                }
            },
            new Tweak
            {
                Id = "suggestions-ads", Category = CatAds,
                Name = L.T("privacy.tweak.suggestionsAds.name"),
                Summary = L.T("privacy.tweak.suggestionsAds.summary"),
                Help = L.T("privacy.tweak.suggestionsAds.help"),
                Actions =
                {
                    Reg(Cdm, "SilentInstalledAppsEnabled", 0),
                    Reg(Cdm, "SystemPaneSuggestionsEnabled", 0),
                    Reg(Cdm, "SoftLandingEnabled", 0),
                    Reg(Cdm, "PreInstalledAppsEnabled", 0),
                    Reg(Cdm, "PreInstalledAppsEverEnabled", 0),
                    Reg(Cdm, "OemPreInstalledAppsEnabled", 0),
                    Reg(Cdm, "RotatingLockScreenOverlayEnabled", 0),
                    Reg(Cdm, "SubscribedContent-310093Enabled", 0),
                    Reg(Cdm, "SubscribedContent-338387Enabled", 0),
                    Reg(Cdm, "SubscribedContent-338388Enabled", 0),
                    Reg(Cdm, "SubscribedContent-338389Enabled", 0),
                    Reg(Cdm, "SubscribedContent-338393Enabled", 0),
                    Reg(Cdm, "SubscribedContent-353694Enabled", 0),
                    Reg(Cdm, "SubscribedContent-353696Enabled", 0),
                    Reg(Adv, "ShowSyncProviderNotifications", 0),
                    Reg(Adv, "Start_IrisRecommendations", 0),
                    Reg(@"HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0),
                    Reg(Pol + @"\CloudContent", "DisableWindowsConsumerFeatures", 1),
                }
            },

            // ================================================================ IA
            new Tweak
            {
                Id = "copilot-recall", Category = CatAi, Recommended = false,
                Name = L.T("privacy.tweak.copilotRecall.name"),
                Summary = L.T("privacy.tweak.copilotRecall.summary"),
                Help = L.T("privacy.tweak.copilotRecall.help"),
                Warning = L.T("privacy.tweak.copilotRecall.warning"),
                Actions =
                {
                    Reg(CuPol + @"\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                    Reg(Pol + @"\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                    Reg(Adv, "ShowCopilotButton", 0),
                    Reg(Pol + @"\WindowsAI", "DisableAIDataAnalysis", 1),
                    Reg(Pol + @"\WindowsAI", "AllowRecallEnablement", 0),
                    Reg(Pol + @"\WindowsAI", "DisableClickToDo", 1),
                    Reg(CuPol + @"\WindowsAI", "DisableAIDataAnalysis", 1),
                    Reg(CuPol + @"\WindowsAI", "DisableClickToDo", 1),
                }
            },

            // ================================================================ Aplicaciones
            new Tweak
            {
                Id = "office", Category = CatApps,
                Name = L.T("privacy.tweak.office.name"),
                Summary = L.T("privacy.tweak.office.summary"),
                Help = L.T("privacy.tweak.office.help"),
                Actions =
                {
                    Reg(Office + @"\16.0\Common\ClientTelemetry", "DisableTelemetry", 1),
                    Reg(Office + @"\16.0\Common\ClientTelemetry", "SendTelemetry", 3),
                    Reg(Office + @"\Common\ClientTelemetry", "SendTelemetry", 3),
                    Reg(Office + @"\16.0\osm", "Enablelogging", 0),
                    Reg(Office + @"\16.0\osm", "EnableUpload", 0),
                    Task(@"\Microsoft\Office\", "OfficeTelemetryAgentLogOn2016"),
                    Task(@"\Microsoft\Office\", "OfficeTelemetryAgentFallBack2016"),
                }
            },
            new Tweak
            {
                Id = "nvidia", Category = CatApps,
                Name = L.T("privacy.tweak.nvidia.name"),
                Summary = L.T("privacy.tweak.nvidia.summary"),
                Help = L.T("privacy.tweak.nvidia.help"),
                Condition = () => RegistryHelper.KeyExists(Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\NVIDIA Corporation"),
                Actions =
                {
                    Reg(@"HKLM\SOFTWARE\NVIDIA Corporation\NvControlPanel2\Client", "OptInOrOutPreference", 0),
                    Svc("NvTelemetryContainer"),
                    Task(@"\", "NvTm*"),
                }
            },
            new Tweak
            {
                Id = "dev-tools", Category = CatApps,
                Name = L.T("privacy.tweak.devTools.name"),
                Summary = L.T("privacy.tweak.devTools.summary"),
                Help = L.T("privacy.tweak.devTools.help"),
                Actions =
                {
                    Str(EnvKey, "DOTNET_CLI_TELEMETRY_OPTOUT", "1"),
                    Str(EnvKey, "POWERSHELL_TELEMETRY_OPTOUT", "1"),
                    Reg(@"HKLM\SOFTWARE\Policies\Microsoft\VisualStudio\SQM", "OptIn", 0),
                }
            },
            new Tweak
            {
                Id = "edge", Category = CatApps, Recommended = false,
                Name = L.T("privacy.tweak.edge.name"),
                Summary = L.T("privacy.tweak.edge.summary"),
                Help = L.T("privacy.tweak.edge.help"),
                Warning = L.T("privacy.tweak.edge.warning"),
                Actions =
                {
                    Reg(Edge, "DiagnosticData", 0),
                    Reg(Edge, "PersonalizationReportingEnabled", 0),
                    Reg(Edge, "UserFeedbackAllowed", 0),
                }
            },
            new Tweak
            {
                Id = "delivery-optimization", Category = CatApps, Recommended = false,
                Name = L.T("privacy.tweak.deliveryOptimization.name"),
                Summary = L.T("privacy.tweak.deliveryOptimization.summary"),
                Help = L.T("privacy.tweak.deliveryOptimization.help"),
                Actions = { Reg(Pol + @"\DeliveryOptimization", "DODownloadMode", 0) }
            },
        };
    }
}
