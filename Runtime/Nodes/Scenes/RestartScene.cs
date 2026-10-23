using System;

namespace Kit
{
    // Neustart: lädt die aktuelle Szene noch einmal, so wie der Spieler zuletzt hereinkam (am selben Eingang).
    // Alles in der Szene beginnt von vorn: Objekte, Logik, Variablen der Szene. Variablen mit dem Bereich Spiel
    // stehen danach so wie beim Betreten der Szene (die Münzen aus dem missglückten Versuch sind weg); mit
    // „Ganzes Spiel zurücksetzen“ beginnen sie ganz von vorn wie bei einem neuen Spiel.
    [Serializable]
    [NodeInfo("Neustart", KitGroups.Szene, "Aktuelle Szene von vorn beginnen")]
    [Hint("Lädt die aktuelle Szene noch einmal. Alles in ihr beginnt von vorn; der Spieler kommt am selben Eingang an wie zuletzt.")]
    public class RestartScene : KitNode
    {
        [Setting("Spiel-Werte wie beim Betreten")]
        [Hint("Variablen mit dem Bereich Spiel stehen nach dem Neustart wieder so wie beim Betreten der Szene: was im missglückten Versuch gesammelt wurde, ist weg.",
              edges: "Aus: Spiel-Variablen behalten ihren jetzigen Wert.")]
        public bool restoreEntry = true;
        [Setting("Ganzes Spiel zurücksetzen")]
        [Hint("Alle Spiel-Variablen beginnen wieder bei ihrem Startwert, wie bei einem neuen Spiel.",
              edges: "An: gilt statt „Spiel-Werte wie beim Betreten“.")]
        public bool resetGame = false;

        [Output("Wird neu geladen")]
        [Hint("Feuert sofort. Was daran hängt, läuft noch in der alten Szene und verschwindet mit ihr.")]
        [NonSerialized] public Output loading;

        [Input("Neu starten")]
        [Hint("Startet den Neustart: zudecken, Szene neu laden, aufdecken.")]
        public void Restart(Signal s)
        {
            var refused = KitScenes.Restart(resetGame, restoreEntry);
            if (refused != null) { Fail(refused); return; }
            Done(resetGame ? "startet neu, ganzes Spiel von vorn" : restoreEntry ? "startet neu, Spiel-Werte wie beim Betreten" : "startet neu, Spiel-Werte bleiben");
            loading.Fire(s);
        }
    }
}
