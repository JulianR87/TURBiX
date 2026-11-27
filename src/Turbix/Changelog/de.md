# Änderungen

Diese Datei wird in TURBiX auf der Seite „Neuigkeiten“ angezeigt. Jede Version hat einen eigenen Abschnitt
mit den wichtigen Änderungen in Kurzform. Jede Version beginnt mit
`## X.Y.Z` (ohne Datum) und jede Änderung ist ein Punkt `- **Titel.** Kurze Beschreibung.`

## 1.2.0

- **Neue Projektwebsite.** TURBiX hat jetzt eine eigene Seite, in sechs Sprachen: https://julianr87.github.io/TURBiX/
- **Sauberere Texte.** Die Zeichensetzung in Hilfetexten, Warnungen und Meldungen wurde in allen sechs Sprachen überarbeitet.

## 1.1.10

- **Stille Installation.** `TURBiX-Setup.exe --silent` installiert, ohne ein Fenster anzuzeigen, und `--uninstall --silent` deinstalliert ohne Rückfrage. Praktisch für mehrere PCs und für Paketmanager.
- **Korrekturen nach Tests unter Windows 10 und 11.** Beim Öffnen des Fensters, während TURBiX bereits neben der Uhr lief, erschien die Meldung „Unerwarteter Fehler“, das passiert nicht mehr. Auf kleinen Bildschirmen ragt das Fenster nicht mehr oben hinaus. Windows-Benachrichtigungen zeigen jetzt den Namen TURBiX.

## 1.1.9

- **Neuer Name: TURBiX.** Programm, Installationsprogramm, Verknüpfungen und Dokumentation schreiben sich jetzt TURBiX. Die Dateien heißen nun `TURBiX.exe` und `TURBiX-Setup.exe`, Ihre Einstellungen und Sicherungen bleiben, wo sie waren.

## 1.1.8

- **Dokumentation in sechs Sprachen.** Die Projektseite gibt es jetzt in denselben Sprachen wie TURBiX, mit mehr Details zu jeder Funktion. Das Programm arbeitet wie in Version 1.1.7.

## 1.1.7

- **Installationsprogramm.** Neben der gewohnten `TURBiX.exe` gibt es jetzt eine `TURBiX-Setup.exe`, die TURBiX unter „Programme“ installiert, die Verknüpfung im Startmenü (und auf Wunsch auf dem Desktop) anlegt und es in die App-Liste von Windows einträgt.
- **Deinstallation.** TURBiX wird über Einstellungen › Apps entfernt, wie jedes andere Programm. Das Deinstallieren macht die Änderungen an Windows nicht rückgängig (dafür gibt es „Wiederherstellen“) und behält die Sicherungen.
- **Korrekturen.** In den Einstellungen zeigt „Wie Windows“ jetzt wirklich die Sprache von Windows. Das Aktivitätsprotokoll wechselt jeden Tag in eine neue Datei, auch wenn TURBiX geöffnet bleibt. Über das Menü des Symbols lässt sich keine Aufgabe mehr starten und TURBiX nicht beenden, solange eine Frage unbeantwortet ist. Unter „Windows-Registrierung“ erscheint derselbe Eintrag nicht mehr doppelt.
## 1.1.6

- **Neue Seite „Einstellungen“.** In der Seitenleiste heißt „Darstellung“ jetzt „Einstellungen“. Dort finden Sie Design, Sprache (jetzt auch selbst wählbar), den Start mit Windows, die automatische Freigabe von Arbeitsspeicher, den Datenordner und eine Schaltfläche, die alles auf den Auslieferungszustand zurücksetzt.
- **Symbol neben der Uhr.** Solange TURBiX geöffnet ist, erscheint sein Symbol im Infobereich. Doppelklick öffnet das Fenster, per Rechtsklick können Sie Arbeitsspeicher freigeben, temporäre Dateien bereinigen oder TURBiX beenden. Das Symbol lässt sich in den Einstellungen ausblenden.
- **Das X beendet TURBiX nicht mehr.** Beim Schließen wird das Fenster nur ausgeblendet, und TURBiX läuft neben der Uhr weiter (in den Einstellungen änderbar). Zum vollständigen Schließen gibt es „TURBiX beenden“ im Menü des Symbols, mit Rückfrage. Beim Windows-Start bleibt TURBiX jetzt direkt neben der Uhr, ohne das Fenster zu öffnen.

## 1.1.5

- **Windows-Benachrichtigungen.** Der Hinweis zum freigegebenen Arbeitsspeicher kommt jetzt als Windows-Benachrichtigung mit Namen und Symbol von TURBiX, statt im Programm zu erscheinen.
- **TURBiX beim Windows-Start öffnen.** Neue Option unter Leistung › Autostart (und unter Arbeitsspeicher). TURBiX öffnet sich bei der Anmeldung minimiert, damit die automatische Freigabe immer läuft. Weil Administratorrechte nötig sind, wird eine Aufgabe „TURBiX“ in der Aufgabenplanung verwendet, die beim Ausschalten wieder gelöscht wird.

## 1.1.4

- **Hinweis nach dem Freigeben.** Wenn Sie Arbeitsspeicher freigeben, zeigt ein Hinweis, wie viel frei wurde und wie viel jetzt verfügbar ist. Läuft die automatische Freigabe, während TURBiX minimiert ist, kommt er als Windows-Benachrichtigung, abschalten lässt er sich unter „Automatische Freigabe“.

## 1.1.3

- **Unter Windows 11 geprüft.** Alles wurde erneut auf einem frisch installierten Windows 11 getestet: Einstellungen anwenden, neu starten und unter „Wiederherstellen“ rückgängig machen. Ein Sicherungsname, der unübersetzt erscheinen konnte, wurde korrigiert.
- **Kleine Bildschirme.** Unter Prozesse werden lange Namen jetzt mit „…“ gekürzt statt abgeschnitten, und die Links auf der Startseite werden in schmalen Fenstern nicht mehr abgeschnitten.

## 1.1.2

- **Neuigkeiten in Ihrer Sprache.** Bei Windows auf Spanisch erschien diese Seite auf Englisch. Jetzt richtet sie sich nach derselben Sprache wie der Rest von TURBiX.

## 1.1.1

- **TURBiX in Ihrer Sprache.** Die Oberfläche erscheint automatisch in der Sprache von Windows: Spanisch, Englisch, Portugiesisch, Französisch, Deutsch oder Russisch. Bei allen anderen Sprachen wird Englisch verwendet.
- **Gruppierte Prozesse.** Ein neuer Tab unter Leistung zeigt alle laufenden Prozesse wie im Task-Manager, aber die Prozesse, die ein Programm gestartet hat, lassen sich darunter aufklappen. Angezeigt werden Prozessor, Arbeitsspeicher und Threads, welcher Dienst in jedem „Diensthost“ läuft, und Sie können einen Task beenden. Prozesse, die Windows braucht, sind gesperrt.
- **Das Protokoll merkt sich frühere Sitzungen.** Das Aktivitätsprotokoll zeigt jetzt auch, was TURBiX in früheren Sitzungen getan hat, nach Tagen getrennt.
- **Lesbare Namen im dunklen Modus.** Unter „Installierte Programme“ erschienen die Namen schwarz auf dunklem Hintergrund.

## 1.1.0

- **Neuer Bereich Apps.** Entfernt mit einem Klick die Apps, die Windows von selbst installiert (Candy Crush, Solitaire mit Werbung, Clipchamp, Nachrichten, TikTok und andere Werbe-Apps). Angezeigt werden nur bekannte Apps, die sich gefahrlos entfernen lassen, nie der Store, Xbox oder Teile von Windows. Wenn Ihnen eine fehlt, können Sie sie kostenlos aus dem Microsoft Store neu installieren.
- **Programme deinstallieren.** Die Liste der installierten Programme mit Größe, Suchfeld und einer Schaltfläche, die das offizielle Deinstallationsprogramm des jeweiligen Programms öffnet.
- **Mehr Windows-Einstellungen.** Taskleiste und Startmenü (Schaltflächen „Jetzt besprechen“, Chat, Cortana und „Aktive Anwendungen“, Suche als Symbol, Taskleiste links, zuletzt verwendete Dateien, die Edge-Verknüpfung, die nach jedem Update wiederkommt), optionale Dienste, die kaum jemand nutzt (Fax, Einzelhandelsdemo, Offlinekarten, Druckwarteschlange, Indizierung…) und das Abschalten des Ruhezustands, was mehrere GB frei macht. Alles lässt sich unter „Wiederherstellen“ rückgängig machen.
- **Sicherungen überschreiben sich nicht mehr.** Wurden zwei Gruppen von Einstellungen in derselben Sekunde angewendet (zum Beispiel Leistung und Windows-Einstellungen), ging die Sicherung der ersten verloren und ließ sich unter „Wiederherstellen“ nicht mehr rückgängig machen. Jetzt hat jede Sitzung ihre eigene Datei.
- **Auch unter Windows 11 getestet.** Richtige Namen beim Windows-Autostart (Cortana erschien als Code) und Texte, die unter Wiederherstellen, Reparieren und auf dem Startbildschirm nicht mehr abgeschnitten werden.
- **Windows.old bereinigen.** Löscht die vorherige Windows-Installation und die Reste großer Updates, die oft 10 bis 30 GB belegen, mit der Datenträgerbereinigung von Windows selbst.

## 1.0.10

- **Gründlich auf einem frisch installierten Windows getestet.** Jede Einstellung, die Bereinigung, der Arbeitsspeicher und der Windows-Autostart wurden angewendet, nach einem Neustart geprüft und wieder rückgängig gemacht. Alles blieb erhalten, und danach war der PC genau wieder so wie vorher.

## 1.0.9

- **Vorsichtigere Registry-Reparatur.** Auf einem frisch installierten Windows meldete sie einige .NET-Einträge als defekt, die Windows schon ab Werk so mitbringt. Jetzt rührt sie nichts an, was zum Windows-Ordner gehört, und zeigt keine doppelten Einträge mehr.
- **Spielmodus richtig erkannt.** Windows hat ihn ab Werk eingeschaltet, TURBiX zeigte ihn aber als „Nicht angewendet“ an.
- **Prozessor unter Zustand.** Zählt die Kerne auf PCs mit mehreren Prozessoren richtig und zeigt den vollständigen Modellnamen.
- **Klarere Hinweise.** Wenn sich der Wiederherstellungspunkt nicht erstellen lässt, nennt das Aktivitätsprotokoll den Grund, statt leer zu bleiben.

## 1.0.8

- **Richtige Symbole unter Windows 10.** Das Symbol für den Arbeitsspeicher gab es unter Windows 10 nicht, es erschien als leeres Kästchen. Jetzt wird es in allen Versionen korrekt angezeigt.
- **Neuigkeiten ohne Datum.** Der Verlauf zeigt nur noch die Versionsnummer.

## 1.0.7

- **Neuer Bereich Zustand.** Zeigt den Zustand jeder Komponente: Laufwerke (Zustand, Abnutzung, Temperatur und Betriebsstunden), Akku, Arbeitsspeicher, Prozessor, Grafikkarte und ihr Treiber, Geräte mit Problemen sowie Bluescreens und unerwartete Abschaltungen im letzten Monat. Jeder Hinweis erklärt, was zu tun ist.
- **Windows-Registry reparieren.** Unter Problemlösung › Windows-Registry: sucht Einträge, die auf nicht mehr vorhandene Programme verweisen (defekte Autostart-Einträge, „Geisterprogramme“ in der Deinstallationsliste, Reste gelöschter Programme), und entfernt sie. Vorher wird eine Sicherung angelegt, die Sie wiederherstellen können.

## 1.0.6

- **PC-Diagnose.** Der Startbildschirm prüft die häufigsten Ursachen für einen langsamen PC (mechanische oder fehlerhafte Festplatte, wenig Speicherplatz, wenig Arbeitsspeicher, zu viele Autostart-Programme, tagelang kein Neustart) und sagt Ihnen, was Sie jeweils tun können.
- **Neuer Bereich Problemlösung › Reparieren.** Die Reparaturen, die der Microsoft-Support empfiehlt, jeweils per Klick: Windows-Dateien (SFC), Windows-Abbild (DISM), Datenträger prüfen, Netzwerk zurücksetzen, hängenden Drucker freigeben und Explorer neu starten.
- **Problemlösung › Windows-Einstellungen.** Beseitigt lästige Dinge in Windows 11: klassisches Kontextmenü, sichtbare Dateierweiterungen, Suchhighlights, Kontohinweise im Startmenü, „Task beenden“ in der Taskleiste, Schnellstart und mehr.
- **Sicherere Bereinigung.** Aus Outlook geöffnete Anhänge und im Schnellzugriff angeheftete Ordner bleiben unberührt, und die Bereinigung folgt nie Verknüpfungen in andere Ordner.
- **Zuverlässigere Sicherung und Wiederherstellung.** Der ursprüngliche Wert wird vor jeder Änderung gespeichert. Schlägt das Rückgängigmachen teilweise fehl, meldet TURBiX das und lässt Sie es erneut versuchen.
- **Klarere Hinweise.** TURBiX meldet, wenn der Wiederherstellungspunkt nicht erstellt werden konnte und wenn es mit einem anderen Administratorkonto als Ihrem geöffnet wurde.
- **Bedienung per Tastatur.** Beim Wechseln mit Tab sieht man, wo der Fokus liegt, und die „?“-Hilfen öffnen sich mit der Eingabetaste.

## 1.0.5

- **Neuer Bereich Leistung.** Drei Werkzeuge, damit der PC schneller startet, Arbeitsspeicher frei hat und in Spielen besser läuft.
- **Windows-Autostart.** Listet die Programme auf, die beim Einschalten des PCs von selbst starten, und lässt Sie sie per Schalter deaktivieren, ohne etwas zu deinstallieren.
- **Arbeitsspeicher.** Zeigt die Auslastung live und welche Programme am meisten belegen. Eine Schaltfläche gibt sofort Speicher frei, und die automatische Bereinigung wiederholt das alle 5, 10, 15, 30 oder 60 Minuten.
- **Spiele.** 13 Einstellungen gegen das, was Windows beim Spielen bremst: Hintergrundaufnahme, Energiesparen, Mausbeschleunigung, Hintergrund-Apps und mehr. Alle lassen sich unter „Wiederherstellen“ rückgängig machen.

## 1.0.4

- **Vollständiger Verlauf.** „Neuigkeiten“ zeigt wieder alle Versionen, ohne eine auszulassen.

## 1.0.3

- **Installierte Version auf einen Blick.** Oben auf der Seite „Neuigkeiten“ steht, welche TURBiX-Version Sie verwenden.

## 1.0.2

- **Klarerer Status in der Seitenleiste.** Wo früher „offline“ stand, steht jetzt „Nutzt kein Internet“, damit es nicht nach einem Problem mit Ihrer Verbindung aussieht. Die Karte hat ein eigenes „?“, das jede Angabe erklärt.
- **Aufgeräumteres Aussehen.** Das „?“ an der Designauswahl ist weg, weil sie sich von selbst erklärt.

## 1.0.1

- **Heller, dunkler oder System-Modus.** Neue Auswahl „Darstellung“ in der Seitenleiste. Mit „System“ übernimmt TURBiX den Modus von Windows und wechselt von selbst, wenn Sie ihn in den Einstellungen ändern.
- **Bereich Neuigkeiten.** Eine neue Seite vor „Über TURBiX“ mit den wichtigen Änderungen jeder Version.

## 1.0.0

- **Erste Version.** Eine einzige ausführbare Datei für Windows 10 (ab 1607) und Windows 11, ohne Installation und ohne Abhängigkeiten.
- **Datenschutz und Telemetrie.** 23 Einstellungen in 5 Gruppen, mit den Profilen „Empfohlen“ und „Maximum“ und dem aktuellen Status jeder Einstellung.
- **Bereinigung.** 20 Kategorien temporärer Dateien und Caches, mit einer vorherigen Analyse, die nur liest, bevor etwas gelöscht wird.
- **Alles umkehrbar.** Automatische Sicherung jedes geänderten Werts, Rückgängigmachen pro Sitzung und optionaler Wiederherstellungspunkt.
- **Hilfe bei jeder Option.** Eine „?“-Schaltfläche erklärt kurz, was jede Einstellung bewirkt.
- **Aktivitätsprotokoll.** Alles, was TURBiX tut, wird auf dem Bildschirm und in C:\ProgramData\TURBiX\Logs festgehalten.
