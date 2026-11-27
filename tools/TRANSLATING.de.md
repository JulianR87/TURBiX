[English](TRANSLATING.md) · [Español](TRADUCIR.md) · [Português (Brasil)](TRANSLATING.pt-BR.md) · [Français](TRANSLATING.fr.md) · **Deutsch** · [Русский](TRANSLATING.ru.md)

# So wird TURBiX übersetzt

TURBiX erscheint in der Sprache von Windows: Spanisch (`es`), Englisch (`en`), brasilianisches Portugiesisch (`pt`), Französisch (`fr`),
Deutsch (`de`) und Russisch (`ru`). Bei jeder anderen Sprache wird Englisch verwendet. Eine andere Sprache ausprobieren: `TURBiX.exe --lang de`
(oder der Wert `Language` unter `HKCU\Software\TURBiX`).

## Wo die Texte liegen

- `src/Turbix/Strings/<Bereich>.<Sprache>.txt`, in die exe eingebettet (keine Satelliten-DLLs: Es bleibt bei einer einzigen Datei).
- Eine Zeile pro Text: `Schlüssel = Text`. `\n` = Zeilenumbruch, ` ` = geschütztes Leerzeichen, `#` = Kommentar.
- Jeder Bereich hat seine 6 Dateien mit **genau denselben Schlüsseln**. Ein Schlüssel kommt nicht in zwei Bereichen vor.
- `common.*.txt` enthält das Gemeinsame (Namen der Bereiche, Schaltflächen, Zustände, Einheiten): **wiederverwenden**, nicht duplizieren.
- Schlüssel: `bereich.sache.detail` in camelCase. Beispiel: `cleanup.item.tempUser.name`, `cleanup.item.tempUser.help`.

## Im Code

```csharp
using Turbix.Localization;

L.T("apps.title")                         // Text
L.F("apps.removed", name)                 // mit Werten {0}, {1}…
L.P("apps.count", n)                      // Plural: {0} ist n, bereits formatiert, {1}… sind Extras: L.P("k", n, extra)
```

```xml
xmlns:l="clr-namespace:Turbix.Localization"
<TextBlock Text="{l:T apps.title}"/>       <!-- auch Content, Tag, ToolTip, HelpTip.Text… -->
```

- Plurale: `schlüssel.one` und `schlüssel.other`, im Russischen `schlüssel.one`, `schlüssel.few`, `schlüssel.many` (1 файл, 2 файла, 5 файлов,
  21 → one, 22 → few, 11–14 → many). Im Französischen und Portugiesischen gilt `one` für 0 und 1. Niemals „Datei(en)“.
- Keine Sätze aus Bruchstücken zusammensetzen („Es wurden “ + n + „ Apps entfernt“): ein Schlüssel mit Platzhaltern. Die
  Wortstellung ist von Sprache zu Sprache verschieden.
- Datumsangaben und Zahlen mit `L.Culture`, Größen mit `FileCleaner.FormatSize` (übersetzt die Einheiten bereits).
- `Id`, `Tag` für die Logik, `CommandParameter`, Registrierungspfade, Dienst- und Dateinamen **werden nicht übersetzt**.
- Fehlermeldungen von Windows (`ex.Message`) kommen bereits in der Systemsprache: Sie werden mit `{0}` eingefügt.
- Der Selbsttest (`Testing/SelfTest.cs`) wird nicht übersetzt: Kein Benutzer bekommt ihn zu sehen.

## Wie man schreibt

- **Natürlich, so wie ein Mensch es schreiben würde**, der Windows kennt und für ganz normale Leute schreibt. Kurze Sätze und
  einfache Wörter. Kein Werbeton, keine Ausrufezeichen, kein „Maximale Optimierung!“ und keine KI-typischen Füllfloskeln
  („Entdecken Sie“, „leistungsstark“, „mühelos“, „mit nur einem Klick“, „seamless“, „unleash“…). Nicht Wort für Wort übersetzen:
  so neu formulieren, wie es ein Muttersprachler sagen würde.
- Anrede: `es` tú · `en` you · `pt` você · `fr` vous · `de` Sie · `ru` вы (kleingeschrieben).
- Anführungszeichen: `es` «…» · `en` “…” · `pt` “…” · `fr` « … » (mit ` ` innen) · `de` „…“ · `ru` «…».
- Die **offiziellen Windows-Namen in der jeweiligen Sprache** verwenden (die von Windows selbst): Administrador de tareas / Task
  Manager / Gerenciador de Tarefas / Gestionnaire des tâches / Task-Manager / Диспетчер задач · Configuración / Settings /
  Configurações / Paramètres / Einstellungen / Параметры · Hibernar / Hibernate / Hibernar / Mettre en veille prolongée /
  Ruhezustand / Гибернация · Explorador de archivos / File Explorer / Explorador de Arquivos / Explorateur de fichiers /
  Datei-Explorer / Проводник. Dasselbe gilt für die Namen der Apps aus dem Microsoft Store (Noticias / News / Notícias /
  Actualités / Nachrichten / Новости…). Ebenso für Marken und Eigennamen (TURBiX, Microsoft Store, Edge, OneDrive, Xbox).
- Die Bereiche von TURBiX heißen so wie in `common` (`nav.*`): Steht im spanischen Text «desde Restaurar», heißt es auf Englisch
  “from Restore”, auf Deutsch „unter Wiederherstellen“ usw.
- Inhaltliche Regeln, die in allen Sprachen gelten: „Nutzt kein Internet“, nie etwas, das nach „keine Verbindung/offline“ klingt,
  RAM freizugeben oder die Registrierung zu bereinigen **beschleunigt** einen PC, der genug Reserven hat, **nicht**, „Gut“ unter Zustand
  bedeutet „keine Anzeichen für Fehler“, keine Garantie.
- Die Hilfetexte hinter dem „?“ bleiben kurz.
- Deutsch und Russisch sind etwa 30 % länger: in XAML keine festen Breiten für Text (`Width` bei Schaltflächen, Registerkarten oder
  Spalten mit Text): `MinWidth` und `Padding` verwenden oder `TextWrapping="Wrap"`, wo zwei Zeilen möglich sind.

## Prüfen

```
python tools/check_strings.py                      # alles
python tools/check_strings.py Views/AppsView.xaml  # nur diese Dateien
```

Das Skript meldet Schlüssel, die in einer Sprache fehlen, abweichende `{0}`-Platzhalter, unvollständige Plurale, doppelte Schlüssel und
spanischen Text, der im Code stehen geblieben ist.
