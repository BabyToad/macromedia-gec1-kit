using System;

namespace Kit
{
    // Start: feuert einmal, sobald die Interaktion läuft (im ersten Frame, wenn alles bereit ist).
    [Serializable]
    [NodeInfo("Start", "Auslöser", "Einmal am Anfang")]
    public class Begin : KitNode
    {
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
