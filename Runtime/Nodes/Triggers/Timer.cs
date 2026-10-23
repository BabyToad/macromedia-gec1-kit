using System;
using UnityEngine;

namespace Kit
{
    // Takt: feuert regelmäßig, solange er läuft. Streuung macht den Abstand etwas zufällig.
    [Serializable]
    [NodeInfo("Takt", KitGroups.Ausloeser, "Feuert alle paar Sekunden")]
    public class Timer : KitNode
    {
        [Hint("Wie viel Zeit zwischen zwei Ticks vergeht. Steht die Spielzeit still, steht auch der Takt.", unit: "s", range: "0,1 bis 10", edges: "Sehr klein, 0 oder negativ: mindestens 0,01 s zwischen zwei Ticks, höchstens ein Tick pro Frame.")]
        [Setting("Abstand")] public float interval = 1f;
        [Hint("Um wie viel jeder Abstand zufällig kürzer oder länger ausfällt.", unit: "s", range: "0 bis zum halben Abstand", edges: "0: ganz gleichmäßig. Größer als der Abstand: manche Ticks kommen fast sofort.")]
        [Setting("Streuung")] public float jitter = 0f;
        [Hint("An: der Takt läuft ab Start von selbst. Aus: erst, wenn bei „Start“ ein Ereignis ankommt.")]
        [Setting("Läuft sofort")] public bool autoStart = true;

        [Hint("Feuert nach jedem Abstand, solange der Takt läuft. Der erste Tick kommt nach einem Abstand, nicht sofort.")]
        [Output("Tick")] [NonSerialized] public Output tick;
        [Hint("Ja, solange der Takt läuft.")]
        [DataOut("Läuft")] [NonSerialized] public bool running;
        [Hint("Wie oft der Takt seit dem Start schon getickt hat (Start setzt auf 0). Z.B. als Rundenzähler oder für eine Uhr.", unit: "Ticks")]
        [DataOut("Anzahl")] [NonSerialized] public float ticks;

        float next;

        public override void OnStart() { if (autoStart) Begin(); }

        [Hint("Startet den Takt. Läuft er schon, beginnt der Abstand von vorn.")]
        [Input("Start")] public void StartTimer(Signal s) { Begin(); Running("läuft"); }
        [Hint("Hält den Takt an. Es kommen keine Ticks mehr, bis wieder ein Ereignis bei „Start“ ankommt.")]
        [Input("Stopp")] public void StopTimer(Signal s) { running = false; Done("gestoppt"); }

        void Begin() { running = true; ticks = 0; next = Time.time + Gap(); }
        float Gap() => Mathf.Max(0.01f, interval + UnityEngine.Random.Range(-jitter, jitter));

        public override void Tick()
        {
            if (!running) return;
            Running($"nächster Tick in {Mathf.Max(0, next - Time.time):0.0} s");
            if (Time.time < next) return;
            next = Time.time + Gap();
            ticks++;
            tick.Fire(new Signal());
        }
    }
}
