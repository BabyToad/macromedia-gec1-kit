# de.macromedia.gec1 – Kurs-Paket für Game Engines & Coding 1

Unity-Paket für den Kurs **Game Engines & Coding 1** (Hochschule Macromedia, Leipzig, WiSe 2026/27).
Die Kursvorlage [`macromedia-gec1-ws26`](https://github.com/BabyToad/macromedia-gec1-ws26) bindet
es über eine Git-URL mit Versions-Tag ein.

| Teil | Seit | Inhalt |
|---|---|---|
| Setup-Check | 0.1.0 | Menü **Kurs > Setup prüfen** |
| Spieler-Controller | 0.2.0 | Prefab `Player/Spieler.prefab` |
| Kit (Graph) | 0.3.0 | Interaktionen als Graph |

Anleitungen: <https://www.allknivesnobagel.com/teaching/gec1/guides/>

**Voraussetzung:** Git muss installiert sein (Windows: Git für Windows; Mac: Command Line Tools),
sonst kann Unity das Paket nicht laden.

**Render-Pipeline:** Spieler-Prefab und Beispiel nutzen URP-Materialien; im Projekt muss die Universal Render Pipeline (URP) aktiv sein. In der Kursvorlage ist sie bereits eingestellt.

Dieses Repository wird aus dem Studio-Repository erzeugt (`setup/tools/release_kit.py`). Änderungen
bitte dort, nicht hier.
