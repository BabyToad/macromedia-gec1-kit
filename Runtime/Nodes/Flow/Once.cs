using System;
using UnityEngine;

namespace Kit
{
    // Einmal: nur das erste Ereignis kommt durch. Danach geht jedes an "Schon passiert" –
    // oder wird blockiert (mit Grund), wenn dort nichts angeschlossen ist.
    [Serializable]
    [NodeInfo("Einmal", KitGroups.Ablauf, "Lässt nur das erste Ereignis durch")]
    public class Once : KitNode
    {
        [Hint("Bekommt das erste Ereignis, nach „Zurücksetzen“ wieder das nächste.")]
        [Output("Erstes Mal")] [NonSerialized] public Output first;
        [Hint("Bekommt jedes weitere Ereignis. Nicht angeschlossen: es endet hier.")]
        [Output("Schon passiert")] [NonSerialized] public Output again;
        [Hint("Ja, sobald das erste Ereignis durch ist; nein nach „Zurücksetzen“.")]
        [DataOut("Schon passiert")] [NonSerialized] public bool happened;

        float when;

        [Hint("Das erste Ereignis geht an „Erstes Mal“, jedes weitere an „Schon passiert“.")]
        [Input("Prüfen")]
        public void Check(Signal signal)
        {
            if (!happened)
            {
                happened = true; when = Time.time;
                first.Fire(signal);
                return;
            }
            if (again.IsConnected) again.Fire(signal);
            else Blocked($"schon passiert (bei {when:0.0} s)");
        }

        [Hint("Macht den Knoten wieder frisch: das nächste Ereignis gilt wieder als erstes.")]
        [Input("Zurücksetzen")]
        public void ResetOnce(Signal signal) { happened = false; Done("zurückgesetzt"); }

        public override string Readout() => happened ? "schon passiert" : "noch nicht passiert";
    }
}
