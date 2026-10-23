using System;

namespace Kit
{
    // Start: feuert einmal, sobald die Logik läuft (im ersten Frame, wenn alles bereit ist).
    [Serializable]
    [NodeInfo("Start", KitGroups.Ausloeser, "Feuert einmal, sobald die Logik läuft")]
    public class Begin : KitNode
    {
        [Hint("Feuert einmal, sobald die Logik läuft: zu Beginn von Play und jedes Mal, wenn ihr Objekt wieder eingeschaltet wird.")]
        [Output("Los")] [NonSerialized] public Output go;

        bool pending;

        public override void OnStart() => pending = true;   // noch nicht feuern: andere Knoten starten gerade erst

        public override void Tick()
        {
            if (!pending) return;
            pending = false;
            Done("✓ Los");
            go.Fire(new Signal());
        }
    }
}
