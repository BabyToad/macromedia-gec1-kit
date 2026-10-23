using System;

namespace Kit
{
    // Wenn: prüft die Bedingung im Moment, in dem das Ereignis ankommt, und schickt es zu Ja oder Nein.
    // Der andere Ausgang bleibt einfach still – das ist kein Fehler.
    // Ist der gewählte Ausgang nicht angeschlossen, endet das Ereignis hier: das zeigt der Knoten als blockiert.
    [Serializable]
    [NodeInfo("Wenn", KitGroups.Ablauf, "Ja oder Nein, je nach Bedingung")]
    public class Branch : KitNode
    {
        [Hint("Ja oder nein. Gelesen wird im Moment, in dem ein Ereignis bei „Prüfen“ ankommt.")]
        [DataIn("Bedingung")] public bool condition;

        [Hint("Bekommt das Ereignis, wenn die Bedingung ja ist.")]
        [Output("Ja")] [NonSerialized] public Output yes;
        [Hint("Bekommt das Ereignis, wenn die Bedingung nein ist.")]
        [Output("Nein")] [NonSerialized] public Output no;

        [Hint("Liest die Bedingung und schickt das Ereignis zu „Ja“ oder „Nein“. Ist dieser Ausgang nicht angeschlossen, endet es hier.")]
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
