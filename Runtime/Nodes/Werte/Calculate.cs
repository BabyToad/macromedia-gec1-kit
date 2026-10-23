using System;
using UnityEngine;

namespace Kit
{
    // Rechnen: macht aus zwei Zahlen eine neue, z.B. Münzen × 10 oder Lebenspunkte begrenzt auf 0 … 100.
    // Kein Ereignis, nur ein Wert: wer das ● Ergebnis liest, bekommt es in diesem Moment ausgerechnet.
    // Größere Rechnungen gehören in eigenen C#-Code (Code ansehen zeigt, wie ein Knoten aussieht).
    [Serializable]
    [NodeInfo("Rechnen", "Werte", "Zwei Zahlen verrechnen")]
    public class Calculate : KitNode
    {
        public enum Op
        {
            [InspectorName("A + B")] Plus,
            [InspectorName("A − B")] Minus,
            [InspectorName("A × B")] Mal,
            [InspectorName("A ÷ B")] Geteilt,
            [InspectorName("kleinere von A und B")] Min,
            [InspectorName("größere von A und B")] Max,
            [InspectorName("A begrenzt auf B … C")] Begrenzen,
        }

        [Hint("Was mit A und B passiert. „Begrenzen“ hält A zwischen B (unten) und C (oben).")]
        [Setting("Rechnung")] public Op op = Op.Plus;
        [Hint("Erste Zahl. Fester Wert oder ● Draht, z.B. der Wert einer Zahl-Variable.")]
        [DataIn("A")] public float a;
        [Hint("Zweite Zahl. Bei „Begrenzen“: die untere Grenze.")]
        [DataIn("B")] public float b = 1f;
        [Hint("Nur bei „Begrenzen“: die obere Grenze.")]
        [DataIn("C")] public float c = 10f;

        [Hint("Das Ergebnis. Teilen durch 0 ergibt 0 (und der Knoten sagt es).")]
        [DataOut("Ergebnis")] public float Result => Compute(op, a, b, c);

        public static float Compute(Op op, float a, float b, float c)
        {
            switch (op)
            {
                case Op.Plus: return a + b;
                case Op.Minus: return a - b;
                case Op.Mal: return a * b;
                case Op.Geteilt: return b == 0f ? 0f : a / b;
                case Op.Min: return Mathf.Min(a, b);
                case Op.Max: return Mathf.Max(a, b);
                default: return Mathf.Clamp(a, Mathf.Min(b, c), Mathf.Max(b, c));
            }
        }

        static readonly string[] k_Forms = { "{0} + {1}", "{0} − {1}", "{0} × {1}", "{0} ÷ {1}", "kleinere von {0}, {1}", "größere von {0}, {1}", "{0} begrenzt auf {1} … {2}" };

        public override string Readout()
        {
            string f(float v) => KitValue.FormatNumber(v);
            string text = string.Format(k_Forms[(int)op], f(a), f(b), f(c)) + " = " + f(Result);
            return op == Op.Geteilt && b == 0f ? text + " (durch 0 geteilt)" : text;
        }
    }
}
