# Changelog

All notable changes to `de.macromedia.gec1`. Versions follow SemVer; tags are vX.Y.Z.

## [0.3.2] - 2026-10-05

- Beispiel „Werkstatt (kaputt)“ für Tag 3: Tools › Kit › Beispiel „Werkstatt (kaputt)“ bauen
- Kraft › Dauerkraft schiebt jetzt so lange, bis „Stopp“ kommt
- Warnungen vor Play bleiben in der Console stehen
- Vorlage für eigene Knoten: Tools › Kit › Eigener Knoten (C#) legt die Dateien in Assets/Eigene Knoten an
- „Kamera wackeln“ wirkt auf die Kamera des Spielers (auch ohne Cinemachine)
- Neuer Knoten „Anhalten“: hält das Spiel für einen Moment an
- Für Menüs: neue Knoten „Knopf gedrückt“, „Pause“ und „Weiter“
- Rohling „Truhe“: Tools › Kit › Rohling „Truhe“ bauen
- Beispiele gibt es nur noch über das Tools-Menü (Tools › Kit › Beispiel …), nicht mehr im Package Manager unter Samples

## [0.3.1] - 2026-10-04

- Rosa Materialien behoben: Spieler und Beispiel nutzen jetzt URP-Materialien
- Spieler-Kamera steht im Edit-Modus hinter dem Spieler statt im Kopf
- „Beispiel bauen“ überschreibt keine eigenen Materialien mehr
- Abhängigkeit: URP (com.unity.render-pipelines.universal 17.3.0) ist jetzt angegeben

## [0.3.0] - 2026-10-04

- Kit mit Graph-Editor: Interaktionen als Graph bauen und beim Spielen beobachten (Fenster öffnet sich per Doppelklick auf eine .kit-Datei)
- Kit-Runtime mit Spielsystemen und Erweiterungen; Cinemachine-Knoten nur, wenn com.unity.cinemachine 3.x installiert ist
- Beispiel „Schlüssel und Tür“ (Package Manager > GEC1 Kurs-Paket > Samples); die Kit-Menüs bauen in Assets/Kit Beispiel und überschreiben nichts ungefragt
- Abhängigkeit: Graph Toolkit 0.4.0-exp.2 (experimentell)

## [0.2.0] - 2026-10-03

- Spieler-Controller: Prefab Spieler unter Player/ (laufen, springen, umsehen; Ich- und Dritte-Person-Ansicht; Gamepad)
- Setup-Check unverändert

## [0.1.0] - 2026-10-03

- Setup-Check (Kurs > Setup prüfen) als Paket; prüft u. a. Unity-Version, URP, Git (für dieses Paket), .gitignore, KI-Verzeichnis und Dokumentation.
