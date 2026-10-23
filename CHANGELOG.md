# Changelog

All notable changes to `de.macromedia.gec1`. Versions follow SemVer; tags are vX.Y.Z.

## [0.5.0] - 2026-10-23

Das Kurs-Paket heißt jetzt **Besteck**. Diese Version bringt alles für Tag 3 und Tag 4 auf einmal: Logik
direkt am Objekt, Werte, Szenen und Bildschirme, Figuren mit Verhalten, Atmosphäre und Kamera.

### Neu

#### Logik am Objekt
- **Logik**: Komponente auf dem Objekt (Add Component › Besteck › Logik); **Neu** legt einen **Logik-Graph** an, **Graph öffnen** zeigt ihn
- **Dieses Objekt**: das Objekt mit der Logik ist im Graph schon da und muss nicht gebunden werden; derselbe Graph funktioniert auf mehreren Objekten
- Jedes gebundene Objekt zeigt seine **Rolle** im Inspector, auch was es gerade spürt (Zone betreten, Zusammenstoß)
- Alle Einstellungen haben deutsche **Tooltips**: was sie bewirken, Einheit, sinnvoller Bereich und was an den Extremen passiert
- Eigener **Spieler** zum Anpassen: die Kursvorlage bringt `Assets/Spieler.prefab` als Prefab-Variante mit. Änderungen daran bleiben, Korrekturen am mitgelieferten Spieler kommen trotzdem an

#### Werte
- **Variable**: eine Zahl, ein Ja-Nein oder ein Text mit Namen. Bereich **Logik** (nur diese Logik), **Szene** (alle in der Szene) oder **Spiel** (bleibt beim Szenenwechsel)
- Zahl-Variablen können **Grenzen** haben und melden **Minimum erreicht**, **Maximum erreicht** und **Wert erreicht**; jede Variable meldet **Geändert**, auch wenn eine andere Logik sie ändert
- Komponente **Variable** (Add Component › Besteck › Variable): ein Wert an einem Objekt, im Play live im Inspector, mit Knöpfen zum Ausprobieren; Vorlagen Lebenspunkte, Zähler, Schalter
- **Rechnen** (+ − × ÷, kleinere/größere, begrenzen) und **Zufallszahl**
- **Wert anzeigen** hält eine Anzeige auf dem Stand einer Zahl; fertige Anzeigen unter GameObject › Besteck › Anzeige: **Zahl**, **Balken**, **Symbolreihe** (das Canvas legt Besteck selbst an)
- **Kurz aktivieren**: ein Objekt oder eine Komponente für ein paar Sekunden an (oder umgekehrt kurz aus)
- **Erzeugen mit Schwung**: eine Prefab-Instanz anlegen und gleich mit Tempo losschicken
- Fenster **Werte** (Tools › Besteck › Werte): alle Variablen mit ihrem aktuellen Wert, während das Spiel läuft

#### Szenen und Bildschirme
- **Eingang** (GameObject › Besteck › Eingang): hier kommt der Spieler aus einer anderen Szene an, mit Blick in Pfeilrichtung; ohne Namen passt der Eingang, der wie die alte Szene heißt
- Neue Knoten **Neustart**, **Nächste Szene**, **Zeit anhalten**, **Zeit fortsetzen**, **Spiel beenden** und Auslöser **Angekommen**; Fenster **Tools › Besteck › Szenen** für die Reihenfolge
- **Bildschirme** Start, Pause, Gewonnen, Verloren (GameObject › Besteck › Bildschirm): das Projekt bekommt eine eigene Variante zum Umgestalten; gezeigt wird ein Bildschirm durch Einschalten, dann steht das Spiel und der Mauszeiger ist frei
- Die Knöpfe der Bildschirme tun erst etwas, wenn eine Logik auf sie hört (Knopf gedrückt → z. B. Neustart); ein unverbundener Knopf sagt beim Klicken, was fehlt

#### Figuren
- **Verhalten** für Figuren: Add Component › Besteck › Verhalten › Patrouillieren, Umherstreifen, Wachen, Folgen, Fliehen, Zurückkehren oder Nachsehen. Jedes Verhalten ist eine eigene Komponente; das Häkchen zeigt, welches gerade läuft
- **Wahrnehmung**: Sichtkegel mit Reichweite und Wänden, Bemerken braucht einen Moment (nah dran schneller), Hören je nach Tempo des Spielers (Stehen ist lautlos, Rennen hört man weit), Vergessen und Berühren
- Neuer Auslöser **Figur**: Sieht Spieler, Hört Spieler, Hört Geräusch, Verliert Spieler, Ziel erreicht, Berührt; mit „Nur während“ gehört ein Knoten zu einem Verhalten
- Neue Knoten **Verhalten wechseln** und **Geräusch** (ein Laut, den Figuren mit Gehör hören, z. B. ein geworfener Stein)
- Im Scene View sieht man Sichtkegel, Hörweite, Route, Weg und Zustand jeder Figur; im Inspector während Play, was sie gerade tut

#### Atmosphäre und Kamera
- **Blende**: Bildschirm in eine Farbe ab- und aufblenden, auch über einen Szenenwechsel hinweg; **Szenenübergang** (Add Component › Besteck › Atmosphäre) stellt Farbe, Dauer und Verlauf pro Szene ein
- **Zeitlupe**: das Spiel weich verlangsamen, halten und wieder beschleunigen
- **Licht**, **Nebel**, **Farbe**: Lampen, Nebel der Szene und Objektfarben (auch Leuchten, Aufblitzen) über die Zeit blenden; „Zurück“ holt den Stand vom Spielstart
- **Lebendiges Licht**: Kerze, kaputte Neonröhre, Atmen, Blinken
- **Partikel**: abspielen, stoppen, Stoß an einem Ort; fertige Vorlagen unter GameObject › Besteck › Atmosphäre › Partikel (Funken, Glut, Rauch, Staub im Licht, Glühwürmchen, Regen, Schnee, Konfetti, Staubwolke)
- **Musik und Atmo**: Musik aus Ebenen, die im Takt bleiben; **Musik-Zone** und Knoten **Musik** blenden Ebenen oder ganze Stücke über
- **Nebel-Zone**, **Bildstimmungs-Zone** (Post-Processing mit sieben Vorlagen: Warm, Kalt, Traum, Bedrohlich, Unter Wasser, Erinnerung, Rausch) und Knoten **Bildstimmung**
- **Tageszeit**: ein Regler von 0 bis 24 Uhr stellt Sonne, Himmelslicht und Nebelfarbe, auch ohne Play; Knoten **Tageszeit** lässt die Zeit laufen
- **Kamera**: Add Component › Besteck › Kamera › Verfolgen, Fest, Blick auf, Schiene; **Kamera-Zone** wechselt beim Betreten mit Kamerafahrt; die Spieler-Kamera läuft dafür als „Spieler-Sicht“ über Cinemachine, ohne anders auszusehen

### Geändert
- Das Kurs-Paket heißt **Besteck**: alle Menüs liegen unter Tools › Besteck, GameObject › Besteck und Add Component › Besteck
- **Interaktion** heißt jetzt **Logik**, der Graph heißt **Logik-Graph**; GameObject › Besteck › Logik legt die Logik auf das gewählte Objekt
- **Szene laden** nimmt die Szene-Datei (hineinziehen statt Namen tippen) und kann einen **Eingang** nennen; Besteck trägt jede Szene selbst in die Build Settings ein
- Szenenwechsel (Szene laden, Neustart, Nächste Szene) blenden über Schwarz ab und auf; dahinter steht die Zeit still
- **Benutzen** meldet jetzt auch **Im Blick** und **Nicht mehr im Blick**; die Reichweite zählt ab dem Spieler, auch in der Ansicht von hinten
- **Inventar** gibt es auch ohne Objekt (Bereich Logik, Szene oder Spiel); am Spieler mit Bereich Spiel reist der Inhalt in die nächste Szene mit
- **Abklingzeit**, **Warten** und **Takt** zeigen ihren Zustand als Wert: Bereit, Wartet, Anzahl
- Das NavMesh backt sich selbst beim Start und folgt dem Level (eine Tür geht auf, eine Wand verschwindet); kein Backen-Knopf mehr nötig, auch nicht für „Gehen zu“
- Knoten **Kamera wechseln** hat eine einstellbare Kamerafahrt statt einer Priorität
- Behoben: eine **Zone** zählt ein Objekt aus mehreren Collidern nur einmal; **Bewegen** dreht auf dem kurzen Weg zurück; im **Dialog** tut die Taste einer leeren Antwort nichts; **Text setzen** meldet einen falschen Platzhalter verständlich

### So aktualisieren
- Im Package Manager die neue Version eintragen (Anleitung „Kurs-Paket aktualisieren“, Adresse mit `#v0.5.0`). Cinemachine 3 kommt dabei automatisch mit
- Danach fragt Unity einmal, ob das Projekt angepasst werden soll: **Jetzt anpassen**. Das geht auch später über **Tools › Besteck › Projekt auf den neuen Stand bringen**. Vorher committen
- Die Anpassung stellt um: Merker → Ja-Nein, Zähler → Zahl, Gemeinsamer Merker → Ja-Nein am Objekt, Lebenspunkte → Zahl „Lebenspunkte“ am Objekt, HUD-Anzeige → Wert anzeigen, getippte Szenennamen → Szene-Dateien (wenn es genau eine Szene mit dem Namen gibt); gebundene Objekte bekommen ihre Rolle. Verbindungen und Einstellungen bleiben erhalten
- Ein paar Dinge verhalten sich danach anders: „Ziel erreicht“ heißt „Wert erreicht“ und meldet sich auch, wenn die Zahl über das Ziel springt; „Getroffen“ und „Geheilt“ sind beide „Geändert“; Heilen bei 0 Lebenspunkten geht jetzt; beim Gemeinsamen Merker gibt es keine Schreibrechte mehr. Die Anpassung schreibt ihre Hinweise in die Console („[Besteck] Anpassen: …“). Danach das Spiel einmal durchspielen
- Eigene Skripte mit den alten Namen: Unity meldet „CS0246 … 'Interaction'“ bzw. „'KitGraph'“. Im Skript „Interaction“ durch „Logic“ und „KitGraph“ durch „LogicGraph“ ersetzen (oder den Agenten bitten)
- Wer noch keinen eigenen Spieler hat: **Tools › Besteck › Spieler-Variante anlegen** legt `Assets/Spieler.prefab` an
- Update, Anpassung und geänderte Dateien zusammen committen; ging etwas schief, alle geänderten Dateien zurücksetzen (Anleitung, Abschnitt „Wenn es nicht klappt“)

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
