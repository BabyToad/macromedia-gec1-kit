using System;

namespace Kit
{
    // Zähler: eine ganze Zahl. "Ziel erreicht" feuert in dem Moment, in dem der Wert das Ziel trifft
    // (nicht noch einmal bei jedem Schritt darüber hinaus). Zurücksetzen setzt auf den Startwert, ohne zu feuern.
    [Serializable]
    [NodeInfo("Zähler", "Zustand", "Zählt hoch und runter")]
    public class Counter : KitNode
    {
        [Setting("Name")] public string label = "Zähler";
        [Setting("Startwert")] public int start = 0;
        [Setting("Ziel")] public int target = 3;

        [Output("Ziel erreicht")] [NonSerialized] public Output reached;
        [NonSerialized] public int value;
        // Als ● Kommazahl ausgegeben, damit der Draht an jede Zahl passt (Vergleich, Text setzen …).
        [DataOut("Wert")] public float Value => value;

        public override void OnStart() => value = start;

        [Input("+1")] public void Add(Signal s) => Change(+1, s);
        [Input("−1")] public void Subtract(Signal s) => Change(-1, s);
        [Input("Zurücksetzen")] public void ResetCounter(Signal s) { value = start; Done($"{label}: {value}"); }

        void Change(int step, Signal s)
        {
            value += step;
            if (value == target) { Done($"{label}: {value} = Ziel"); reached.Fire(s); }
            else Done($"{label}: {value}");
        }

        public override string Readout() => $"{label}: {value} / {target}";
    }
}
