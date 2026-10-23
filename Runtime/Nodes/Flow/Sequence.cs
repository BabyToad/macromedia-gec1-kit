using System;

namespace Kit
{
    // Nacheinander: feuert 1., dann 2., dann 3., dann 4. – alles im selben Frame.
    // Wartet NICHT, bis etwas fertig ist. Für "erst wenn die Tür oben ist": Ausgang "Angekommen" verwenden.
    // (Mehrere Drähte an einem Ausgang gehen auch; ihre Nummer am Draht ist die Reihenfolge.)
    [Serializable]
    [Hint("Wartet nicht: hängt an „1.“ ein Bewegen, feuert „2.“ sofort und nicht erst nach der Fahrt. Für „danach“ den Ausgang „Angekommen“ oder „Fertig“ des ersten Knotens verwenden.")]
    [NodeInfo("Nacheinander", KitGroups.Ablauf, "Feuert die Ausgänge der Reihe nach, ohne zu warten")]
    public class Sequence : KitNode
    {
        [Hint("Feuert als Erstes. Was daran hängt, ist erledigt, bevor „2.“ feuert, soweit es nicht über Zeit läuft.")]
        [Output("1.")] [NonSerialized] public Output first;
        [Hint("Feuert direkt nach „1.“, im selben Frame.")]
        [Output("2.")] [NonSerialized] public Output second;
        [Hint("Feuert direkt nach „2.“, im selben Frame.")]
        [Output("3.")] [NonSerialized] public Output third;
        [Hint("Feuert direkt nach „3.“, als Letztes.")]
        [Output("4.")] [NonSerialized] public Output fourth;

        [Hint("Feuert die Ausgänge 1. bis 4. der Reihe nach, alle im selben Moment.")]
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
