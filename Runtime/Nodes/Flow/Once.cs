using System;
using UnityEngine;

namespace Kit
{
    // Einmal: nur das erste Ereignis kommt durch. Danach geht jedes an "Schon passiert" –
    // oder wird blockiert (mit Grund), wenn dort nichts angeschlossen ist.
    [Serializable]
    [NodeInfo("Einmal", "Ablauf", "Lässt nur das erste Ereignis durch")]
    public class Once : KitNode
    {
        [Output("Erstes Mal")] [NonSerialized] public Output first;
        [Output("Schon passiert")] [NonSerialized] public Output again;
        [DataOut("Schon passiert")] [NonSerialized] public bool happened;

        float when;

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

        [Input("Zurücksetzen")]
        public void ResetOnce(Signal signal) { happened = false; Done("zurückgesetzt"); }

        public override string Readout() => happened ? "schon passiert" : "noch nicht passiert";
    }
}
