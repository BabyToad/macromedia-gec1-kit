using System;

namespace Kit
{
    // Wenn: prüft die Bedingung im Moment, in dem das Ereignis ankommt, und schickt es zu Ja oder Nein.
    // Der andere Ausgang bleibt einfach still – das ist kein Fehler.
    // Ist der gewählte Ausgang nicht angeschlossen, endet das Ereignis hier: das zeigt der Knoten als blockiert.
    [Serializable]
    [NodeInfo("Wenn", "Ablauf", "Ja oder Nein, je nach Bedingung")]
    public class Branch : KitNode
    {
        [DataIn("Bedingung")] public bool condition;

        [Output("Ja")] [NonSerialized] public Output yes;
        [Output("Nein")] [NonSerialized] public Output no;

        [Input("Prüfen")]
        public void Check(Signal signal)
        {
            var chosen = condition ? yes : no;
            string why = $"weil {SourceOf(nameof(condition))}";
            if (!chosen.IsConnected)
            {
                Blocked($"{(condition ? "Ja" : "Nein")} ist nicht angeschlossen – {why}");
                return;
            }
            Done($"→ {(condition ? "Ja" : "Nein")}, {why}");
            chosen.Fire(signal);
        }
    }
}
