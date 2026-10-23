using System;
using UnityEngine;

namespace Kit
{
    // Abklingzeit: lässt ein Ereignis durch und sperrt danach für eine Weile.
    [Serializable]
    [NodeInfo("Abklingzeit", KitGroups.Ablauf, "Nach einem Durchlass eine Weile gesperrt")]
    public class Cooldown : KitNode
    {
        [Hint("Wie lange nach einem Durchlass alles gesperrt bleibt. Steht die Spielzeit still, läuft auch die Sperre nicht ab.", unit: "s", range: "0,2 bis 10", edges: "0 oder negativ: nie gesperrt.")]
        [Setting("Sperre")] public float seconds = 1f;

        [Hint("Bekommt das Ereignis, wenn gerade nicht gesperrt ist. Die Sperre beginnt in diesem Moment.")]
        [Output("Durch")] [NonSerialized] public Output passed;
        [Hint("Bekommt jedes Ereignis, das in die Sperre fällt. Die Sperre wird dadurch nicht länger. Nicht angeschlossen: es endet hier.")]
        [Output("Gesperrt")] [NonSerialized] public Output rejected;
        [Hint("Wie lange die Sperre noch dauert.", unit: "s", edges: "0: offen.")]
        [DataOut("Restzeit")] public float Remaining => Mathf.Max(0f, until - Time.time);
        [Hint("Ja, wenn das nächste Ereignis durchkäme (die Sperre ist vorbei), z.B. für eine Anzeige „bereit“.")]
        [DataOut("Bereit")] public bool Ready => Time.time >= until;

        float until = float.MinValue;

        [Hint("Lässt das Ereignis durch oder weist es ab, je nachdem, ob gerade die Sperre läuft.")]
        [Input("Prüfen")]
        public void Check(Signal signal)
        {
            if (Time.time >= until)
            {
                until = Time.time + seconds;
                passed.Fire(signal);
                return;
            }
            if (rejected.IsConnected) rejected.Fire(signal);
            else Blocked($"gesperrt, noch {Remaining:0.0} s");
        }

        public override string Readout() => Remaining > 0 ? $"gesperrt: {Remaining:0.0} s" : "offen";
    }
}
