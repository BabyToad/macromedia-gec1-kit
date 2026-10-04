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
    [NodeInfo("Warten", "Ablauf", "Gibt das Ereignis nach einer Zeit weiter")]
    public class Wait : KitNode
    {
        public enum WhenBusy { NeuStarten, Ignorieren }

        [Setting("Dauer (s)")] public float seconds = 1f;
        [Setting("Wenn schon wartet")] public WhenBusy whenBusy = WhenBusy.NeuStarten;
        [Setting("Echtzeit (ignoriert Pause)")] public bool realtime = false;

        [Output("Fertig")] [NonSerialized] public Output done;
        [Output("Abgebrochen")] [NonSerialized] public Output cancelled;
        [DataOut("Restzeit (s)")] [NonSerialized] public float remaining;

        Coroutine work;
        Signal waiting;   // das Ereignis, das gerade wartet

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
