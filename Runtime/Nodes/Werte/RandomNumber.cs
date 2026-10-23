using System;
using UnityEngine;

namespace Kit
{
    // Zufallszahl: würfelt, wenn ein Ereignis ankommt, und merkt sich das Ergebnis, bis wieder gewürfelt wird.
    // Die Zahl reist mit dem Ereignis: ein zweites Würfeln überschreibt sie nicht für ein Ereignis, das noch wartet.
    [Serializable]
    [NodeInfo("Zufallszahl", "Werte", "Eine zufällige Zahl würfeln")]
    public class RandomNumber : KitNode
    {
        [Hint("Kleinste mögliche Zahl.")]
        [DataIn("Von")] public float from = 1f;
        [Hint("Größte mögliche Zahl (bei ganzen Zahlen eingeschlossen: 1 bis 6 ist ein Würfel).")]
        [DataIn("Bis")] public float to = 6f;
        [Hint("An: nur ganze Zahlen (1, 2, 3 …). Aus: auch Kommazahlen (z.B. 2,73).")]
        [Setting("Ganze Zahlen")] public bool whole = true;

        [Hint("Feuert nach dem Würfeln. Das ● Ergebnis gehört zu diesem Ereignis.")]
        [Output("Gewürfelt")] [NonSerialized] public Output rolled;

        [Hint("Die zuletzt gewürfelte Zahl (vor dem ersten Würfeln: Von).")]
        [DataOut("Zahl", EventLocal = true)] [NonSerialized] public float number;

        [NonSerialized] bool m_Rolled;

        public override void OnStart() { m_Rolled = false; number = from; }

        [Hint("Eine neue Zahl würfeln.")]
        [Input("Würfeln")]
        public void Roll(Signal s)
        {
            float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
            if (whole && Mathf.CeilToInt(lo) > Mathf.FloorToInt(hi)) { Fail($"zwischen {KitValue.FormatNumber(lo)} und {KitValue.FormatNumber(hi)} liegt keine ganze Zahl"); return; }
            number = whole ? UnityEngine.Random.Range(Mathf.CeilToInt(lo), Mathf.FloorToInt(hi) + 1) : UnityEngine.Random.Range(lo, hi);
            m_Rolled = true;
            Done($"gewürfelt: {KitValue.FormatNumber(number)}");
            rolled.Fire(s);
        }

        public override string Readout() => m_Rolled ? $"{KitValue.FormatNumber(number)} (aus {KitValue.FormatNumber(from)} … {KitValue.FormatNumber(to)})" : $"{KitValue.FormatNumber(from)} … {KitValue.FormatNumber(to)}";
    }
}
