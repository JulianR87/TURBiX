using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Turbix.Localization;

namespace Turbix.Core
{
    /// <summary>Una app preinstalada de la lista de Turbix.</summary>
    public sealed class BloatApp
    {
        /// <summary>Nombre del paquete. Si termina en «*» se compara solo el comienzo.</summary>
        public string Package { get; set; }

        /// <summary>Parte de la clave de sus textos: apps.app.&lt;Key&gt;.name, .summary, .help y .warning (opcional).</summary>
        public string Key { get; set; }

        public string Name => L.T("apps.app." + Key + ".name");
        public string Summary => L.T("apps.app." + Key + ".summary");
        public string Help => L.T("apps.app." + Key + ".help");
        public string Warning => L.Has("apps.app." + Key + ".warning") ? L.T("apps.app." + Key + ".warning") : null;

        /// <summary>true = incluida en «Recomendado» (publicidad o apps que casi nadie usa).</summary>
        public bool Recommended { get; set; }

        public bool Matches(string packageName) =>
            Package.EndsWith("*")
                ? packageName.StartsWith(Package.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)
                : string.Equals(packageName, Package, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Una app de la lista que está instalada en este equipo.</summary>
    public sealed class InstalledApp
    {
        public BloatApp Info { get; set; }
        public string FullName { get; set; }
        public string FamilyName { get; set; }
        public string Version { get; set; }
    }

    /// <summary>
    /// Quita apps preinstaladas de Microsoft Store con el administrador de paquetes de Windows
    /// (Windows.Management.Deployment.PackageManager), sin PowerShell ni dependencias.
    ///
    /// Solo se ofrecen apps de una lista conocida: nada de componentes del sistema, la Tienda,
    /// Xbox (lo usan Game Pass y los juegos), la Calculadora, Recortes, el Bloc de notas ni
    /// «Obtener ayuda» (en Windows 11 contiene los solucionadores de problemas).
    /// Quitar una app no se revierte desde «Restaurar»: se vuelve a instalar gratis desde Microsoft Store.
    /// </summary>
    public static class AppxOps
    {
        private static Type PackageManagerType =>
            Type.GetType("Windows.Management.Deployment.PackageManager, Windows.Management.Deployment, ContentType=WindowsRuntime");

        private static Type AsyncInfoType =>
            Type.GetType("Windows.Foundation.IAsyncInfo, Windows.Foundation, ContentType=WindowsRuntime");

        public static readonly IReadOnlyList<BloatApp> Catalog = new List<BloatApp>
        {
            // ------------------------------------------------------------ publicidad y promociones
            App("king.com.*", "king", true),
            App("Microsoft.MicrosoftSolitaireCollection", "solitaire", true),
            App("Microsoft.MicrosoftOfficeHub", "officeHub", true),
            App("Microsoft.Getstarted", "tips", true),
            App("Microsoft.WindowsFeedbackHub", "feedbackHub", true),
            App("Microsoft.BingNews", "news", true),
            App("Microsoft.BingSearch", "bingSearch", true),
            App("Microsoft.SkypeApp", "skype", true),
            App("Microsoft.549981C3F5F10", "cortana", true),
            App("Microsoft.MixedReality.Portal", "mixedReality", true),
            App("Microsoft.Microsoft3DViewer", "viewer3d", true),
            App("Microsoft.Print3D", "print3d", true),
            App("Microsoft.3DBuilder", "builder3d", true),
            App("Microsoft.OneConnect", "mobilePlans", true),
            App("Microsoft.Messaging", "messaging", true),
            App("Microsoft.Wallet", "wallet", true),
            App("Clipchamp.Clipchamp", "clipchamp", true),
            App("MicrosoftTeams", "teams", true),
            App("Microsoft.MicrosoftJournal", "journal", false),
            App("Microsoft.PowerAutomateDesktop", "powerAutomate", false),
            App("Microsoft.Todos", "todo", false),
            App("Microsoft.BingWeather", "weather", false),
            App("Microsoft.WindowsMaps", "maps", false),
            App("Microsoft.People", "people", false),
            App("microsoft.windowscommunicationsapps", "mail", false),
            App("Microsoft.OutlookForWindows", "outlook", false),
            App("Microsoft.ZuneVideo", "moviesTv", false),
            App("Microsoft.WindowsAlarms", "clock", false),
            App("Microsoft.WindowsSoundRecorder", "soundRecorder", false),
            App("Microsoft.MSPaint", "paint3d", false),
            App("Microsoft.YourPhone", "phoneLink", false),
            App("MicrosoftCorporationII.QuickAssist", "quickAssist", false),
            App("Microsoft.Copilot", "copilot", false),

            // ------------------------------------------------------------ apps de terceros que Windows instala solo
            App("SpotifyAB.SpotifyMusic", "spotify", false),
            App("4DF9E0F8.Netflix", "netflix", false),
            App("Disney.37853FC22B2CE", "disneyPlus", false),
            App("AmazonVideo.PrimeVideo", "primeVideo", false),
            App("BytedancePte.Ltd.TikTok", "tiktok", true),
            App("Facebook.Facebook*", "facebook", true),
            App("Facebook.Instagram*", "instagram", true),
            App("Facebook.317180B0BB486", "messenger", true),
            App("5319275A.WhatsAppDesktop", "whatsapp", false),
            App("Flipboard.Flipboard", "flipboard", true),
            App("9E2F88E3.Twitter", "twitter", true),
            App("AdobeSystemsIncorporated.AdobePhotoshopExpress", "photoshopExpress", true),
            App("Duolingo-LearnLanguagesforFree", "duolingo", true),
            App("PandoraMediaInc.29680B314EFC2", "pandora", true),
            App("flaregamesGmbH.RoyalRevolt2", "royalRevolt", true),
            App("A278AB0D.MarchofEmpires", "marchOfEmpires", true),
            App("A278AB0D.DisneyMagicKingdoms", "disneyMagicKingdoms", true),
            App("DolbyLaboratories.DolbyAccess", "dolby", false),
        };

        /// <summary>Los textos de cada app están en Stringspps.*.txt: apps.app.&lt;clave&gt;.name / .summary / .help / .warning.</summary>
        private static BloatApp App(string package, string key, bool recommended) =>
            new BloatApp { Package = package, Key = key, Recommended = recommended };

        public static bool IsSupported
        {
            get
            {
                try { return PackageManagerType != null; }
                catch { return false; }
            }
        }

        /// <summary>Apps de la lista instaladas para el usuario actual.</summary>
        public static List<InstalledApp> FindInstalled()
        {
            var result = new List<InstalledApp>();
            Type pmType = PackageManagerType;
            if (pmType == null) return result;
            object pm = Activator.CreateInstance(pmType);
            var find = pmType.GetMethod("FindPackagesForUser", new[] { typeof(string) });
            foreach (object p in (IEnumerable)find.Invoke(pm, new object[] { string.Empty }))
            {
                try
                {
                    Type t = p.GetType();
                    if (Prop<bool>(p, t, "IsFramework")) continue;
                    if (Convert.ToString(t.GetProperty("SignatureKind")?.GetValue(p)) == "System") continue;   // parte de Windows
                    object id = t.GetProperty("Id").GetValue(p);
                    Type it = id.GetType();
                    string name = (string)it.GetProperty("Name").GetValue(id);
                    var info = Catalog.FirstOrDefault(b => b.Matches(name));
                    if (info == null) continue;
                    object ver = it.GetProperty("Version").GetValue(id);
                    Type vt = ver.GetType();
                    result.Add(new InstalledApp
                    {
                        Info = info,
                        FullName = (string)it.GetProperty("FullName").GetValue(id),
                        FamilyName = (string)it.GetProperty("FamilyName").GetValue(id),
                        Version = $"{vt.GetField("Major").GetValue(ver)}.{vt.GetField("Minor").GetValue(ver)}.{vt.GetField("Build").GetValue(ver)}",
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn(L.F("apps.log.readEntryError", ex.Message));
                }
            }
            // Un mismo nombre puede tener varias arquitecturas instaladas: se muestra una vez.
            return result.GroupBy(a => a.FamilyName).Select(g => g.First()).OrderBy(a => a.Info.Name, StringComparer.Create(L.Culture, true)).ToList();
        }

        /// <summary>Quita la app para el usuario actual y evita que se instale en cuentas nuevas.</summary>
        public static void Remove(InstalledApp app)
        {
            Type pmType = PackageManagerType ?? throw new NotSupportedException(L.T("apps.error.notSupported"));
            object pm = Activator.CreateInstance(pmType);

            // Todas las arquitecturas instaladas de la misma app (x64, x86, neutral).
            var fullNames = new List<string>();
            var find = pmType.GetMethod("FindPackagesForUser", new[] { typeof(string), typeof(string) });
            if (find != null)
            {
                foreach (object p in (IEnumerable)find.Invoke(pm, new object[] { string.Empty, app.FamilyName }))
                {
                    object id = p.GetType().GetProperty("Id").GetValue(p);
                    fullNames.Add((string)id.GetType().GetProperty("FullName").GetValue(id));
                }
            }
            if (fullNames.Count == 0) fullNames.Add(app.FullName);

            var remove = pmType.GetMethod("RemovePackageAsync", new[] { typeof(string) });
            foreach (string fullName in fullNames)
                Wait(remove.Invoke(pm, new object[] { fullName }), remove.ReturnType);

            // Sin esto, Windows la vuelve a instalar al crear una cuenta nueva o tras una actualización grande.
            var deprovision = pmType.GetMethod("DeprovisionPackageForAllUsersAsync", new[] { typeof(string) });
            if (deprovision != null)
            {
                try { Wait(deprovision.Invoke(pm, new object[] { app.FamilyName }), deprovision.ReturnType); }
                catch (Exception ex) { Logger.Info("   " + L.F("apps.log.notProvisioned", app.Info.Name, ex.Message.Trim())); }
            }
        }

        /// <summary>Espera una operación asíncrona de Windows y lanza su error, si lo hubo.</summary>
        private static void Wait(object operation, Type operationType)
        {
            Type info = AsyncInfoType;
            PropertyInfo status = info.GetProperty("Status");
            var deadline = DateTime.UtcNow.AddMinutes(5);
            string s;
            while ((s = Convert.ToString(status.GetValue(operation))) == "Started")
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException(L.T("apps.error.timeout"));
                Thread.Sleep(150);
            }
            if (s == "Error")
            {
                var error = info.GetProperty("ErrorCode").GetValue(operation) as Exception;
                string detail = null;
                try
                {
                    object result = operationType.GetMethod("GetResults").Invoke(operation, null);
                    detail = Convert.ToString(result?.GetType().GetProperty("ErrorText")?.GetValue(result));
                }
                catch
                {
                    // El detalle es opcional.
                }
                string message = !string.IsNullOrWhiteSpace(detail) ? detail.Trim() : error?.Message?.Trim() ?? L.T("apps.error.unknown");
                throw new InvalidOperationException(message);
            }
            if (s == "Canceled") throw new OperationCanceledException(L.T("apps.error.canceled"));
        }

        private static T Prop<T>(object o, Type t, string name)
        {
            object v = t.GetProperty(name)?.GetValue(o);
            return v is T x ? x : default(T);
        }
    }
}
