using System;
using UnityEngine;

namespace Kit
{
    // Abklingzeit: lässt ein Ereignis durch und sperrt danach für eine Weile.
    [Serializable]
    [NodeInfo("Abklingzeit", "Ablauf", "Nach einem Durchlass eine Weile gesperrt")]
    public class Cooldown : KitNode
    {
        [Setting("Sperre (s)")] public float seconds = 1f;

        [Output("Durch")] [NonSerialized] public Output passed;
        [Output("Gesperrt")] [NonSerialized] public Output rejected;
        [DataOut("Restzeit (s)")] public float Remaining => Mathf.Max(0f, until - Time.time);

        float until = float.MinValue;

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
