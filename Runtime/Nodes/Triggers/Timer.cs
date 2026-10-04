using System;
using UnityEngine;

namespace Kit
{
    // Takt: feuert regelmäßig, solange er läuft. Streuung macht den Abstand etwas zufällig.
    [Serializable]
    [NodeInfo("Takt", "Auslöser", "Feuert alle paar Sekunden")]
    public class Timer : KitNode
    {
        [Setting("Abstand (s)")] public float interval = 1f;
        [Setting("Streuung (s)")] public float jitter = 0f;
        [Setting("Läuft sofort")] public bool autoStart = true;

        [Output("Tick")] [NonSerialized] public Output tick;
        [DataOut("Läuft")] [NonSerialized] public bool running;

        float next;

        public override void OnStart() { if (autoStart) Begin(); }

        [Input("Start")] public void StartTimer(Signal s) { Begin(); Running("läuft"); }
        [Input("Stopp")] public void StopTimer(Signal s) { running = false; Done("gestoppt"); }

        void Begin() { running = true; next = Time.time + Gap(); }
        float Gap() => Mathf.Max(0.01f, interval + UnityEngine.Random.Range(-jitter, jitter));

        public override void Tick()
        {
            if (!running) return;
            Running($"nächster Tick in {Mathf.Max(0, next - Time.time):0.0} s");
            if (Time.time < next) return;
            next = Time.time + Gap();
            tick.Fire(new Signal());
        }
    }
}
