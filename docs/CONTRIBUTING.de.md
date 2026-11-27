[English](../CONTRIBUTING.md) · [Español](CONTRIBUTING.es.md) · [Português (Brasil)](CONTRIBUTING.pt-BR.md) · [Français](CONTRIBUTING.fr.md) · **Deutsch** · [Русский](CONTRIBUTING.ru.md)

# Bei TURBiX mitmachen

Danke, dass Sie helfen möchten. TURBiX ist ein öffentliches, kostenloses Projekt, das in der Freizeit gepflegt wird, und jeder Beitrag zählt. Programmieren müssen Sie dafür nicht können.

## So können Sie helfen

- **Einen Fehler melden.** Öffnen Sie ein [Issue](https://github.com/JulianR87/TURBiX/issues/new/choose) und nennen Sie Ihre Windows- und TURBiX-Version, was Sie getan haben und was Sie erwartet hätten. Das Aktivitätsprotokoll (Seite **Aktivitätsprotokoll**, Schaltfläche *Kopieren*) hilft sehr.
- **Eine Verbesserung vorschlagen** unter [Issues](https://github.com/JulianR87/TURBiX/issues) oder Fragen in den [Discussions](https://github.com/JulianR87/TURBiX/discussions) stellen.
- **Eine Übersetzung verbessern.** Die Texte sind einfache Textdateien in `src/Turbix/Strings`. Die Anleitung ist [`tools/TRANSLATING.de.md`](../tools/TRANSLATING.de.md).
- **Code beisteuern** per Pull Request. Issues mit dem Label `good first issue` sind ein guter Einstieg.
- **Einen Fehlalarm eines Virenscanners melden.** Nennen Sie den Virenscanner und den Namen der Erkennung, damit wir ihn dem Hersteller melden können.

## Kompilieren

Sie brauchen das [.NET SDK](https://dotnet.microsoft.com/download) (6 oder neuer). Führen Sie `build.cmd` aus; das Ergebnis sind `dist\TURBiX.exe` und `dist\TURBiX-Setup.exe`.

## Regeln für Code

1. **Es muss auf jedem PC mit frisch installiertem Windows 10 (1607+) oder 11 laufen.** Keine Downloads, keine Zusatzpakete, kein PowerShell im Programm, kein Internetzugriff.
2. **Alles lässt sich rückgängig machen.** Eine Einstellung sichert zuerst den ursprünglichen Wert und lässt sich unter **Wiederherstellen** zurücknehmen.
3. **Die Sicherheit bleibt unangetastet.** Nichts, was Windows Update, Microsoft Defender, SmartScreen oder die Firewall abschaltet, und keine „Tricks“, die etwas kaputt machen.
4. **Kein fest eingebauter Text im Code.** Jeder sichtbare Text steht in `Strings`, in allen sechs Sprachen. `python tools/check_strings.py` muss `OK` ausgeben.
5. **Jede Option hat ihre „?“-Schaltfläche** mit einer kurzen, ehrlichen Erklärung. Keine Versprechen, die das Programm nicht halten kann.
6. **Testen Sie vor dem Einsenden.** Nutzen Sie für alles, was Windows verändert, eine virtuelle Maschine und schreiben Sie in den Pull Request, was Sie getestet haben.

Bitte eine Änderung pro Pull Request. Issues und Pull Requests können auf Englisch oder Spanisch verfasst sein.

## Sicherheitsprobleme

Bitte öffnen Sie für eine Sicherheitslücke kein öffentliches Issue. Nutzen Sie **Security › Report a vulnerability** auf der Seite des Repositorys, damit sie behoben werden kann, bevor sie bekannt wird.
