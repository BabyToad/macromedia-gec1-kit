using System;

namespace Kit
{
    // Vergleich: macht aus einer Zahl ein Ja/Nein, z.B. "Münzen ≥ 3". Kein Ereignis, nur ein Wert,
    // den "Wenn" lesen kann. Mehr Rechnen gehört in eigenen C#-Code.
    [Serializable]
    [NodeInfo("Vergleich", "Zustand", "Zahl mit einem Wert vergleichen")]
    public class Compare : KitNode
    {
        public enum Op { GrößerGleich, Größer, Gleich, Ungleich, Kleiner, KleinerGleich }

        [DataIn("Zahl")] public float number;
        [Setting("Vergleich")] public Op op = Op.GrößerGleich;
        [Setting("Wert")] public float threshold = 1f;

        [DataOut("Ergebnis")]
        public bool Result
        {
            get
            {
                switch (op)
                {
                    case Op.GrößerGleich: return number >= threshold;
                    case Op.Größer: return number > threshold;
                    case Op.Gleich: return Math.Abs(number - threshold) < 0.0001f;
                    case Op.Ungleich: return Math.Abs(number - threshold) >= 0.0001f;
                    case Op.Kleiner: return number < threshold;
                    default: return number <= threshold;
                }
            }
        }

        static readonly string[] k_Symbols = { "≥", ">", "=", "≠", "<", "≤" };
        public override string Readout() => $"{number:0.##} {k_Symbols[(int)op]} {threshold:0.##}: {JaNein(Result)}";
    }
}
