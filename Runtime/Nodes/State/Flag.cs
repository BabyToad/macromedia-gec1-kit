using System;

namespace Kit
{
    // Merker: ein Ja/Nein mit Namen, z.B. "Schlüssel vorhanden".
    // Er feuert nur, wenn sich der Wert wirklich ändert. Den Wert lesen andere über den ● Draht.
    // Der Startwert steht im Graph; jede Interaktion in der Szene hat ihren eigenen Merker.
    [Serializable]
    [NodeInfo("Merker", "Zustand", "Merkt sich ja oder nein")]
    public class Flag : KitNode
    {
        [Setting("Name")] public string label = "Merker";
        [Setting("Startwert")] public bool startValue = false;

        [Output("Wurde ja")] [NonSerialized] public Output becameTrue;
        [Output("Wurde nein")] [NonSerialized] public Output becameFalse;
        [DataOut("Wert")] [NonSerialized] public bool value;

        public override void OnStart() => value = startValue;   // jedes Play beginnt beim Startwert

        [Input("Ja setzen")] public void SetTrue(Signal s) => Set(true, s);
        [Input("Nein setzen")] public void SetFalse(Signal s) => Set(false, s);
        [Input("Umschalten")] public void Toggle(Signal s) => Set(!value, s);

        void Set(bool v, Signal s)
        {
            if (v == value) { Done($"{label}: war schon {JaNein(v)}"); return; }
            value = v;
            (v ? becameTrue : becameFalse).Fire(s);
        }

        public override string Readout() => $"{label}: {JaNein(value)}";
    }
}
