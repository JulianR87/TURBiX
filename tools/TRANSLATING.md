**English** · [Español](TRADUCIR.md) · [Português (Brasil)](TRANSLATING.pt-BR.md) · [Français](TRANSLATING.fr.md) · [Deutsch](TRANSLATING.de.md) · [Русский](TRANSLATING.ru.md)

# How TURBiX is translated

TURBiX is shown in the Windows language: Spanish (`es`), English (`en`), Brazilian Portuguese (`pt`), French (`fr`),
German (`de`) and Russian (`ru`). Any other language falls back to English. To try another language: `TURBiX.exe --lang de`
(or the `Language` value in `HKCU\Software\TURBiX`).

## Where the strings live

- `src/Turbix/Strings/<area>.<language>.txt`, embedded in the exe (no satellite DLLs: it is still a single file).
- One line per string: `key = text`. `\n` = line break, ` ` = non-breaking space, `#` = comment.
- Each area has its 6 files with **exactly the same keys**. A key is never repeated in two areas.
- `common.*.txt` holds what is shared (section names, buttons, states, units): **reuse it**, don't duplicate it.
- Keys: `area.thing.detail` in camelCase. E.g. `cleanup.item.tempUser.name`, `cleanup.item.tempUser.help`.

## In the code

```csharp
using Turbix.Localization;

L.T("apps.title")                         // text
L.F("apps.removed", name)                 // with values {0}, {1}…
L.P("apps.count", n)                      // plural: {0} is n already formatted, {1}… are extras: L.P("k", n, extra)
```

```xml
xmlns:l="clr-namespace:Turbix.Localization"
<TextBlock Text="{l:T apps.title}"/>       <!-- also Content, Tag, ToolTip, HelpTip.Text… -->
```

- Plurals: `key.one` and `key.other`, in Russian `key.one`, `key.few`, `key.many` (1 файл, 2 файла, 5 файлов,
  21 → one, 22 → few, 11–14 → many). In French and Portuguese `one` covers 0 and 1. Never “file(s)”.
- Don't build sentences by gluing pieces together (“Removed ” + n + “ apps”): use one key with placeholders. Word
  order changes from one language to another.
- Dates and numbers with `L.Culture`, sizes with `FileCleaner.FormatSize` (it already translates the units).
- `Id`s, `Tag`s used by logic, `CommandParameter`s, registry paths, service names and file names **are not translated**.
- Error messages that come from Windows (`ex.Message`) are already in the system language: insert them with `{0}`.
- The self-test (`Testing/SelfTest.cs`) is not translated: no user ever sees it.

## How to write

- **Natural, the way a person would write it**: someone who knows Windows and writes for ordinary people. Short sentences,
  plain words. No advertising tone, no exclamation marks, no “Optimize to the max!”, none of the filler typical of AI
  (“Discover”, “powerful”, “effortless”, “in just one click”, “seamless”, “unleash”…). Don't translate word for word:
  rewrite it the way a native speaker would say it.
- Form of address: `es` tú · `en` you · `pt` você · `fr` vous · `de` Sie · `ru` вы (lowercase).
- Quotation marks: `es` «…» · `en` “…” · `pt` “…” · `fr` « … » (with ` ` inside) · `de` „…“ · `ru` «…».
- Use the **official Windows names in that language** (the ones Windows itself uses): Administrador de tareas / Task
  Manager / Gerenciador de Tarefas / Gestionnaire des tâches / Task-Manager / Диспетчер задач · Configuración / Settings /
  Configurações / Paramètres / Einstellungen / Параметры · Hibernar / Hibernate / Hibernar / Mettre en veille prolongée /
  Ruhezustand / Гибернация · Explorador de archivos / File Explorer / Explorador de Arquivos / Explorateur de fichiers /
  Datei-Explorer / Проводник. The same goes for the names of Microsoft Store apps (Noticias / News / Notícias /
  Actualités / Nachrichten / Новости…). Likewise brands and proper names (TURBiX, Microsoft Store, Edge, OneDrive, Xbox).
- TURBiX sections are named as in `common` (`nav.*`): if the Spanish text says «desde Restaurar», in English it is
  “from Restore”, in German „unter Wiederherstellen“, and so on.
- Content rules that apply in every language: “Doesn't use the internet”, never anything that sounds like “offline” or
  “no connection”, freeing up RAM or cleaning the registry **does not speed up** a PC that has plenty to spare, “Good” in
  Health means “no signs of failure”, not a guarantee.
- The “?” help texts stay short.
- German and Russian run about 30% longer: in XAML, no fixed widths for text (`Width` on buttons, tabs or
  columns with text): use `MinWidth` and `Padding`, or `TextWrapping="Wrap"` where the text may take two lines.

## Checking

```
python tools/check_strings.py                      # everything
python tools/check_strings.py Views/AppsView.xaml  # only those files
```

It reports keys missing in any language, mismatched `{0}` placeholders, incomplete plurals, repeated keys and
Spanish text left hard-coded in the code.
