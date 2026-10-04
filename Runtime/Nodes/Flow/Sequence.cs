using System;

namespace Kit
{
    // Nacheinander: feuert 1., dann 2., dann 3., dann 4. – alles im selben Frame.
    // Wartet NICHT, bis etwas fertig ist. Für "erst wenn die Tür oben ist": Ausgang "Angekommen" verwenden.
    // (Mehrere Drähte an einem Ausgang gehen auch; ihre Nummer am Draht ist die Reihenfolge.)
    [Serializable]
    [NodeInfo("Nacheinander", "Ablauf", "Feuert die Ausgänge der Reihe nach, ohne zu warten")]
    public class Sequence : KitNode
    {
        [Output("1.")] [NonSerialized] public Output first;
        [Output("2.")] [NonSerialized] public Output second;
        [Output("3.")] [NonSerialized] public Output third;
        [Output("4.")] [NonSerialized] public Output fourth;

        [Input("Start")]
        public void Go(Signal signal)
        {
            // Zweig 1 läuft komplett durch (soweit er nicht wartet), dann Zweig 2 usw.
            first.Fire(signal);
            second.Fire(signal);
            third.Fire(signal);
            fourth.Fire(signal);
        }
    }
}
