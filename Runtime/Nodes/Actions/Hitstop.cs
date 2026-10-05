using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Anhalten: friert das Spiel für ein paar Millisekunden ein (Hitstop), damit ein Treffer sitzt. Hält die Zeit
    // über KitTime an und gibt sie nach der Dauer in echter Zeit wieder frei. Mehrere Anhalten (oder ein offenes
    // Pause-Menü) gleichzeitig: das langsamste gilt, das Tempo von vorher kommt erst zurück, wenn keins mehr hält.
    [Serializable]
    [NodeInfo("Anhalten", "Aktion", "Spiel kurz einfrieren (Hitstop)")]
    public class Hitstop : KitNode
    {
        [Setting("Dauer (ms)")] public float milliseconds = 80f;
        [Setting("Tempo (0 = Stillstand)")] public float timeScale = 0f;

        [Output("Weiter")] [NonSerialized] public Output resumed;

        Coroutine holding;

        [Input("Anhalten")]
        public void Hold(Signal s)
        {
            if (milliseconds <= 0f) { Fail("Dauer muss größer als 0 sein"); return; }
            if (holding != null) StopWork(holding);              // erneut: die Zeit beginnt von vorn
            KitTime.Hold(this, timeScale);                       // dieser Knoten hält an; das langsamste Anhalten gilt
            holding = StartWork(Wait(s));
            Running($"hält an: {milliseconds:0} ms");
        }

        IEnumerator Wait(Signal s)
        {
            yield return new WaitForSecondsRealtime(milliseconds / 1000f);
            holding = null;
            KitTime.Release(this);
            Done("✓ weiter");
            resumed.Fire(s);
        }

        public override void OnStop()
        {
            holding = null;
            KitTime.Release(this);                               // nie im Stillstand hängen bleiben
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (milliseconds <= 0f) problems.Add("Dauer muss größer als 0 sein");
        }
    }
}
