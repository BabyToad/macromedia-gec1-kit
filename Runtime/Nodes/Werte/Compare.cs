using System;
using UnityEngine;

namespace Kit
{
    // Vergleich: macht aus einer Zahl ein Ja oder Nein, z.B. „Münzen ≥ 3“. Kein Ereignis, nur ein Wert,
    // den „Wenn“ (oder eine Ja-Nein-Variable über „Setzen“) lesen kann.
    // B ist eine Einstellung (wie bis v0.3.2, damit alte Graphen ihren Wert behalten). Zwei Variablen vergleicht
    // man über Rechnen: A − B, dann Vergleich mit 0.
    [Serializable]
    [NodeInfo("Vergleich", "Werte", "Eine Zahl prüfen: ja oder nein")]
    public class Compare : KitNode
    {
        public enum Op
        {
            [InspectorName("A ≥ B")] GrößerGleich,
            [InspectorName("A > B")] Größer,
            [InspectorName("A = B")] Gleich,
            [InspectorName("A ≠ B")] Ungleich,
            [InspectorName("A < B")] Kleiner,
            [InspectorName("A ≤ B")] KleinerGleich,
        }

        [Hint("Die Zahl, die geprüft wird, z.B. der ● Wert einer Zahl-Variable.")]
        [DataIn("A")] public float number;
        [Hint("Wie A und B verglichen werden.")]
        [Setting("Vergleich")] public Op op = Op.GrößerGleich;
        [Hint("Womit A verglichen wird, z.B. 3. Zwei Variablen vergleichen: erst Rechnen (A − B), dann mit 0 vergleichen.")]
        [Setting("B")] public float threshold = 1f;

        [Hint("Ja, wenn der Vergleich stimmt. „Gleich“ verzeiht winzige Rundungsfehler (0,0001).")]
        [DataOut("Ergebnis")] public bool Result => Check(op, number, threshold);

        public static bool Check(Op op, float a, float b)
        {
            switch (op)
            {
                case Op.GrößerGleich: return a >= b;
                case Op.Größer: return a > b;
                case Op.Gleich: return Math.Abs(a - b) < 0.0001f;
                case Op.Ungleich: return Math.Abs(a - b) >= 0.0001f;
                case Op.Kleiner: return a < b;
                default: return a <= b;
            }
        }

        static readonly string[] k_Symbols = { "≥", ">", "=", "≠", "<", "≤" };
        public override string Readout() => $"{KitValue.FormatNumber(number)} {k_Symbols[(int)op]} {KitValue.FormatNumber(threshold)}: {JaNein(Result)}";
    }
}
