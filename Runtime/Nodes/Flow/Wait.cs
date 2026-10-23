using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Warten: hält ein Ereignis eine Zeit lang fest und gibt es dann weiter.
    // Es gibt immer nur EIN Warten pro Knoten. Kommt während des Wartens ein neues Ereignis:
    //   Neu starten (Standard): das alte wird verworfen ("Abgebrochen" feuert), die Zeit beginnt von vorn.
    //   Ignorieren: das neue wird blockiert, das alte läuft weiter.
    [Serializable]
    [Hint("Pro Knoten wartet immer nur ein Ereignis. Kommt ein neues, während eins wartet, entscheidet „Wenn schon wartet“: von vorn beginnen oder das neue ignorieren.")]
    [NodeInfo("Warten", KitGroups.Ablauf, "Gibt das Ereignis nach einer Zeit weiter")]
    public class Wait : KitNode
    {
        public enum WhenBusy { NeuStarten, Ignorieren }

        [Hint("Wie lange gewartet wird, bevor „Fertig“ feuert.", unit: "s", range: "0,1 bis 10", edges: "0: sofort weiter. Negativ: zählt als 0.")]
        [Setting("Dauer")] public float seconds = 1f;
        [Hint("Was passiert, wenn ein Ereignis ankommt, während noch eins wartet. Neu starten: das alte wird verworfen („Abgebrochen“ feuert), die Zeit beginnt von vorn. Ignorieren: das neue endet hier, das alte wartet weiter.")]
        [Setting("Wenn schon wartet")] public WhenBusy whenBusy = WhenBusy.NeuStarten;
        [Hint("An: wartet in echter Zeit, auch während das Spiel angehalten ist (Pause, Anhalten). Aus: steht die Spielzeit still, wartet auch der Knoten.")]
        [Setting("Echtzeit")] public bool realtime = false;

        [Hint("Feuert nach der Dauer, mit dem Ereignis, das gewartet hat (z.B. mit seinem ◆ Wer).")]
        [Output("Fertig")] [NonSerialized] public Output done;
        [Hint("Feuert, wenn ein Warten vorzeitig endet: durch „Abbrechen“ oder weil ein neues Ereignis es neu startet. Mit dem alten Ereignis.")]
        [Output("Abgebrochen")] [NonSerialized] public Output cancelled;
        [Hint("Wie lange das laufende Warten noch dauert.", unit: "s", edges: "0: wartet gerade nicht.")]
        [DataOut("Restzeit")] [NonSerialized] public float remaining;
        [Hint("Ja, solange ein Ereignis wartet.")]
        [DataOut("Wartet")] public bool Waiting => work != null;

        Coroutine work;
        Signal waiting;   // das Ereignis, das gerade wartet

        [Hint("Beginnt das Warten mit diesem Ereignis.")]
        [Input("Start")]
        public void Begin(Signal signal)
        {
            if (work != null)
            {
                if (whenBusy == WhenBusy.Ignorieren) { Blocked($"wartet schon (noch {remaining:0.0} s)"); return; }
                StopWork(work);                    // das alte Warten endet hier, sein "Fertig" kommt nie
                cancelled.Fire(waiting);           // mit dem alten Ereignis (z.B. dessen "Wer")
            }
            if (seconds <= 0f) { work = null; waiting = null; remaining = 0; done.Fire(signal); return; }   // 0 s: sofort
            waiting = signal;
            work = StartWork(Run(signal));
        }

        [Hint("Beendet das laufende Warten: „Fertig“ kommt nicht, dafür „Abgebrochen“. Wartet nichts, passiert nichts.")]
        [Input("Abbrechen")]
        public void Cancel(Signal signal)
        {
            if (work == null) { Done("war nicht am Warten"); return; }
            StopWork(work); work = null; remaining = 0;
            var old = waiting; waiting = null;
            Done("abgebrochen");
            cancelled.Fire(old);                   // das abgebrochene Ereignis, nicht das "Abbrechen"-Ereignis
        }

        IEnumerator Run(Signal signal)
        {
            for (remaining = seconds; remaining > 0f; remaining -= realtime ? Time.unscaledDeltaTime : Time.deltaTime)
            {
                Running($"wartet: {remaining:0.0} s", 1f - remaining / seconds);
                yield return null;                 // weiter im nächsten Frame
            }
            remaining = 0; work = null; waiting = null;
            Done("✓ Fertig");
            done.Fire(signal);                     // dasselbe Ereignis, mit seinen Werten (z.B. "Wer")
        }

        public override void OnStop() { work = null; waiting = null; remaining = 0; }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (seconds < 0) problems.Add("Dauer ist negativ; sie zählt als 0 s");
        }
    }
}
