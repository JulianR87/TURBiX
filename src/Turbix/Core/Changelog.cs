using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Turbix.Core
{
    public sealed class ChangelogItem
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public bool HasTitle => !string.IsNullOrEmpty(Title);
        public bool HasText => !string.IsNullOrEmpty(Text);
    }

    public sealed class ChangelogRelease
    {
        public string Version { get; set; }
        public string DateText { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsFirst { get; set; }
        public bool IsLast { get; set; }
        public List<ChangelogItem> Items { get; } = new List<ChangelogItem>();
    }

    /// <summary>
    /// Lee el CHANGELOG.md del repositorio, que va incrustado en el ejecutable.
    /// Formato esperado:
    ///   ## 1.0.1
    ///   - **Título del cambio.** Descripción breve.
    /// </summary>
    public static class Changelog
    {
        private const string SpanishResource = "Turbix.CHANGELOG.md";

        /// <summary>El historial en el idioma de la interfaz; el español es el CHANGELOG.md del repositorio.</summary>
        private static IEnumerable<string> Candidates()
        {
            string lang = Localization.L.Lang;
            if (lang == Localization.L.Source)
            {
                // Español: es el CHANGELOG.md de la raíz, no un archivo de Changelog\.
                yield return SpanishResource;
                yield return $"Turbix.Changelog.{Localization.L.Fallback}.md";
                yield break;
            }
            yield return $"Turbix.Changelog.{lang}.md";
            if (lang != Localization.L.Fallback) yield return $"Turbix.Changelog.{Localization.L.Fallback}.md";
            yield return SpanishResource;
        }

        public static List<ChangelogRelease> Load()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (string name in Candidates())
                {
                    using (var stream = asm.GetManifestResourceStream(name))
                    {
                        if (stream == null) continue;
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                            return Parse(reader.ReadToEnd(), AppInfo.Version);
                    }
                }
                return new List<ChangelogRelease>();
            }
            catch (Exception ex)
            {
                Logger.Warn(Localization.L.F("changelog.readError", ex.Message));
                return new List<ChangelogRelease>();
            }
        }

        public static List<ChangelogRelease> Parse(string markdown, string currentVersion)
        {
            var list = new List<ChangelogRelease>();
            ChangelogRelease release = null;
            ChangelogItem item = null;

            foreach (string raw in markdown.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("## "))
                {
                    release = ParseHeader(line.Substring(3).Trim(), currentVersion);
                    list.Add(release);
                    item = null;
                }
                else if (release != null && (line.StartsWith("- ") || line.StartsWith("* ")))
                {
                    item = ParseItem(line.Substring(2).Trim());
                    release.Items.Add(item);
                }
                else if (item != null && line.StartsWith("  ") && line.Trim().Length > 0)
                {
                    // Continuación de la viñeta anterior.
                    item.Text = (item.Text + " " + Clean(line.Trim())).Trim();
                }
                else if (line.Trim().Length == 0)
                {
                    item = null;
                }
            }

            // Una versión sin viñetas no aporta nada en «Novedades».
            list.RemoveAll(r => r.Items.Count == 0);

            if (list.Count > 0)
            {
                list[0].IsFirst = true;
                list[list.Count - 1].IsLast = true;
            }
            return list;
        }

        private static ChangelogRelease ParseHeader(string header, string currentVersion)
        {
            // «1.0.1 — 2026-11-26», «1.0.1 - 2026-11-26» o solo «1.0.1».
            string version = header, date = null;
            int sep = header.IndexOfAny(new[] { ' ', '\t' });
            if (sep > 0)
            {
                version = header.Substring(0, sep);
                date = header.Substring(sep).Trim().TrimStart('—', '–', '-', '·').Trim();
            }
            version = version.TrimStart('v', 'V').Trim('[', ']');

            string dateText = date;
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
                dateText = d.ToString("D", Localization.L.Culture);

            return new ChangelogRelease
            {
                Version = version,
                DateText = dateText ?? "",
                IsCurrent = string.Equals(version, currentVersion, StringComparison.OrdinalIgnoreCase),
            };
        }

        private static ChangelogItem ParseItem(string text)
        {
            if (text.StartsWith("**"))
            {
                int end = text.IndexOf("**", 2, StringComparison.Ordinal);
                if (end > 2)
                {
                    string title = Clean(text.Substring(2, end - 2)).TrimEnd('.', ':').Trim();
                    string rest = Clean(text.Substring(end + 2)).TrimStart('.', ':', ' ', '—', '-').Trim();
                    return new ChangelogItem { Title = title, Text = rest };
                }
            }
            return new ChangelogItem { Text = Clean(text) };
        }

        /// <summary>Quita el marcado Markdown más común para mostrar texto plano.</summary>
        private static string Clean(string s) => s.Replace("**", "").Replace("`", "");
    }
}
