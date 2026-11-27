using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Markup;
using Microsoft.Win32;

namespace Turbix.Localization
{
    /// <summary>
    /// Textos de la interfaz en el idioma de Windows. Los textos viven en Strings\*.{idioma}.txt, incrustados en el exe
    /// (no hay ensamblados satélite: Turbix sigue siendo un único archivo).
    ///
    /// Formato de cada archivo: una línea «clave = texto». «\n» es un salto de línea y « » un espacio duro. Las líneas que empiezan con # son
    /// comentarios. Los plurales usan sufijos: clave.one / clave.few / clave.many / clave.other (few y many solo en ruso).
    ///
    /// Si falta una clave se usa el inglés; si tampoco está, el español (el idioma en que se escribe Turbix).
    /// </summary>
    public static class L
    {
        public static readonly string[] Supported = { "en", "es", "pt", "fr", "de", "ru" };
        public const string Fallback = "en";
        public const string Source = "es";

        private static readonly object Sync = new object();
        private static Dictionary<string, string> _current, _fallback, _source;
        private static string _lang, _windowsLang;
        private static CultureInfo _culture;

        /// <summary>Código de dos letras del idioma en uso («es», «en», …).</summary>
        public static string Lang { get { EnsureLoaded(); return _lang; } }

        /// <summary>Cultura para fechas y números, coherente con el idioma de los textos.</summary>
        public static CultureInfo Culture { get { EnsureLoaded(); return _culture; } }

        /// <summary>
        /// Elige el idioma: el indicado (argumento --lang), el guardado en HKCU\Software\Turbix\Language
        /// o el idioma de la interfaz de Windows. Si no es uno de los admitidos, inglés.
        /// </summary>
        public static void Init(string requested = null)
        {
            lock (Sync)
            {
                // App.OnStartup cambia después la cultura del programa a la del idioma elegido, así que el idioma
                // de Windows se anota aquí, la primera vez, antes de ese cambio.
                if (_windowsLang == null)
                    _windowsLang = Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? Fallback;
                string lang = Normalize(requested) ?? Normalize(ReadOverride()) ?? _windowsLang;
                _lang = lang;
                _culture = PickCulture(lang);
                var all = LoadAll();
                _current = all.TryGetValue(lang, out var c) ? c : new Dictionary<string, string>();
                _fallback = all.TryGetValue(Fallback, out var f) ? f : new Dictionary<string, string>();
                _source = all.TryGetValue(Source, out var s) ? s : new Dictionary<string, string>();
            }
        }

        private static void EnsureLoaded()
        {
            if (_current == null) Init();
        }

        private static string Normalize(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return null;
            lang = lang.Trim().ToLowerInvariant();
            if (lang.Length > 2) lang = lang.Substring(0, 2);
            return Supported.Contains(lang) ? lang : null;
        }

        /// <summary>Idioma elegido en Ajustes, o null si sigue el de Windows.</summary>
        public static string SavedChoice => Normalize(ReadOverride());

        /// <summary>Idioma que tocaría según Windows (inglés si no es uno de los admitidos).</summary>
        public static string WindowsLanguage { get { EnsureLoaded(); return _windowsLang; } }

        /// <summary>Nombre de cada idioma escrito en ese mismo idioma (así lo encuentra quien lo habla).</summary>
        public static string NativeName(string lang)
        {
            switch (lang)
            {
                case "es": return "Español";
                case "pt": return "Português (Brasil)";
                case "fr": return "Français";
                case "de": return "Deutsch";
                case "ru": return "Русский";
                default: return "English";
            }
        }

        /// <summary>Guarda el idioma elegido (null = el de Windows). Se aplica al volver a abrir Turbix.</summary>
        public static void SaveChoice(string lang)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\TURBiX"))
                {
                    if (key == null) return;
                    if (Normalize(lang) == null) key.DeleteValue("Language", false);
                    else key.SetValue("Language", Normalize(lang), RegistryValueKind.String);
                }
            }
            catch
            {
                // sin permiso para escribir: se queda con el idioma actual
            }
        }

        private static string ReadOverride()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\TURBiX"))
                    return key?.GetValue("Language") as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Si la configuración regional de Windows es del mismo idioma, se respeta (p. ej. es-AR, pt-PT, fr-CA).
        /// Si no, se usa la variante más común del idioma de los textos, para no mezclar «lunes» con «Monday».
        /// </summary>
        private static CultureInfo PickCulture(string lang)
        {
            var regional = CultureInfo.CurrentCulture;
            if (regional.TwoLetterISOLanguageName == lang) return regional;
            string name;
            switch (lang)
            {
                case "es": name = "es-ES"; break;
                case "pt": name = "pt-BR"; break;
                case "fr": name = "fr-FR"; break;
                case "de": name = "de-DE"; break;
                case "ru": name = "ru-RU"; break;
                default: name = "en-US"; break;
            }
            try { return CultureInfo.GetCultureInfo(name); }
            catch { return CultureInfo.InvariantCulture; }
        }

        // ------------------------------------------------------------------ carga

        private static Dictionary<string, Dictionary<string, string>> LoadAll()
        {
            var result = Supported.ToDictionary(l => l, l => new Dictionary<string, string>(StringComparer.Ordinal));
            var asm = Assembly.GetExecutingAssembly();
            foreach (string res in asm.GetManifestResourceNames())
            {
                // Turbix.Strings.<área>.<idioma>.txt
                if (!res.StartsWith("Turbix.Strings.", StringComparison.Ordinal) || !res.EndsWith(".txt", StringComparison.Ordinal)) continue;
                string[] parts = res.Split('.');
                if (parts.Length < 5) continue;
                string lang = parts[parts.Length - 2];
                if (!result.TryGetValue(lang, out var dict)) continue;
                using (var stream = asm.GetManifestResourceStream(res))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    Parse(reader, dict);
            }
            return result;
        }

        private static void Parse(TextReader reader, Dictionary<string, string> dict)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string t = line.TrimStart();
                if (t.Length == 0 || t[0] == '#') continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                string value = t.Substring(eq + 1);
                if (value.StartsWith(" ")) value = value.Substring(1);
                dict[key] = Unescape(value.TrimEnd());
            }
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 's': sb.Append(' '); break;        // espacio visible al final de una línea
                        case 'u' when i + 4 < s.Length &&
                                      int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code):
                            sb.Append((char)code);              //   = espacio duro
                            i += 4;
                            break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append('\\').Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ consulta

        public static bool Has(string key)
        {
            EnsureLoaded();
            return _current.ContainsKey(key) || _fallback.ContainsKey(key) || _source.ContainsKey(key);
        }

        /// <summary>Texto de la clave en el idioma actual.</summary>
        public static string T(string key)
        {
            EnsureLoaded();
            if (key == null) return string.Empty;
            if (_current.TryGetValue(key, out var v)) return v;
            if (_fallback.TryGetValue(key, out v)) return v;
            if (_source.TryGetValue(key, out v)) return v;
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Falta el texto: " + key);
#endif
            return "[" + key + "]";
        }

        /// <summary>Texto con valores: {0}, {1}… con el formato de números y fechas del idioma.</summary>
        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try { return string.Format(Culture, format, args); }
            catch (FormatException) { return format; }
        }

        /// <summary>
        /// Texto en singular o plural según <paramref name="n"/>. {0} es el número (ya con separador de miles) y
        /// {1}, {2}… los valores extra.
        /// </summary>
        public static string P(string key, long n, params object[] extra)
        {
            EnsureLoaded();
            string form = PluralForm(_lang, n);
            string k = key + "." + form;
            if (!Has(k)) k = key + ".other";
            var args = new object[1 + (extra?.Length ?? 0)];
            args[0] = n.ToString("N0", Culture);
            if (extra != null) Array.Copy(extra, 0, args, 1, extra.Length);
            return F(k, args);
        }

        /// <summary>Reglas de plural de CLDR, simplificadas a números enteros.</summary>
        private static string PluralForm(string lang, long n)
        {
            n = Math.Abs(n);
            switch (lang)
            {
                case "ru":
                    long d = n % 10, h = n % 100;
                    if (d == 1 && h != 11) return "one";
                    if (d >= 2 && d <= 4 && (h < 12 || h > 14)) return "few";
                    return "many";
                case "fr":
                case "pt":
                    return n <= 1 ? "one" : "other";
                default:
                    return n == 1 ? "one" : "other";
            }
        }
    }

    /// <summary>Texto traducido en XAML: Text="{l:T nav.home}".</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class T : MarkupExtension
    {
        public T() { }
        public T(string key) { Key = key; }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
    }
}
