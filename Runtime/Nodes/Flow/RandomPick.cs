using System;

namespace Kit
{
    // Zufällig: feuert genau EINEN der ersten "Anzahl" Ausgänge, alle gleich wahrscheinlich.
    [Serializable]
    [NodeInfo("Zufällig", "Ablauf", "Feuert einen zufälligen Ausgang")]
    public class RandomPick : KitNode
    {
        [Setting("Anzahl (2–3)")] public int count = 2;

        [Output("A")] [NonSerialized] public Output a;
        [Output("B")] [NonSerialized] public Output b;
        [Output("C")] [NonSerialized] public Output c;

        [Input("Start")]
        public void Go(Signal signal)
        {
            int n = Math.Max(1, Math.Min(3, count));
            int pick = UnityEngine.Random.Range(0, n);   // 0 … n-1
            var chosen = pick == 0 ? a : pick == 1 ? b : c;
            if (!chosen.IsConnected) { Blocked($"gewählt: {"ABC"[pick]}, aber dort ist nichts angeschlossen"); return; }
            chosen.Fire(signal);
        }
    }
}
