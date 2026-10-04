using System;

namespace Kit
{
    // Gemeinsamer Merker: wie ein Merker, aber er gehört einem Objekt mit „Gemeinsamer Zustand“ in der Szene
    // und mehrere Interaktionen können ihn benutzen. Ändert ihn irgendwer, feuern bei allen, die ihn
    // benutzen, "Wurde ja"/"Wurde nein". Wer schreiben darf, steht am Besitzer.
    [Serializable]
    [NodeInfo("Gemeinsamer Merker", "Zustand", "Ja/nein, das mehrere Interaktionen teilen")]
    public class SharedFlag : KitNode
    {
        [Ref("Besitzer", Permanent = true)] public KitSharedState owner;
        [Setting("Name")] public string label = "Strom an";

        [Output("Wurde ja")] [NonSerialized] public Output becameTrue;
        [Output("Wurde nein")] [NonSerialized] public Output becameFalse;
        [DataOut("Wert")] public bool Value => owner != null && (owner.Find(label)?.value ?? false);

        bool lost;

        public override void OnStart()
        {
            lost = false;
            owner.Changed += OnChanged;
        }

        public override void OnStop() { if (owner != null) owner.Changed -= OnChanged; }

        // Eine Änderung (von hier oder von einer anderen Interaktion) beginnt hier ein neues Ereignis.
        void OnChanged(string name, bool value, Interaction writer)
        {
            if (name != label) return;
            Done($"{label}: {JaNein(value)}{(writer != Runner && writer != null ? $" (von „{writer.name}“)" : "")}");
            (value ? becameTrue : becameFalse).Fire(new Signal());
        }

        [Input("Ja setzen")] public void SetTrue(Signal s) => Write(true);
        [Input("Nein setzen")] public void SetFalse(Signal s) => Write(false);
        [Input("Umschalten")] public void Toggle(Signal s) => Write(!Value);

        void Write(bool v)
        {
            var refused = owner.TrySet(label, v, Runner, out bool changed);
            if (refused != null) { Blocked(refused); return; }
            if (!changed) Done($"{label}: war schon {JaNein(v)}");
            // bei einer Änderung meldet sich OnChanged (auch hier) mit einem eigenen Ereignis
        }

        public override void Tick()
        {
            if (owner == null && !lost) { lost = true; Fail("◆ Besitzer wurde gelöscht: den gemeinsamen Merker gibt es nicht mehr"); }
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (owner != null && owner.Find(label) == null) problems.Add($"„{owner.name}“ hat keinen Merker „{label}“");
        }

        public override string Readout() => owner != null ? $"{label}: {JaNein(Value)}" : "Besitzer fehlt";
    }
}
