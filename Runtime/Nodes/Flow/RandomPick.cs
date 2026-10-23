using System;

namespace Kit
{
    // Zufällig: feuert genau EINEN der ersten "Anzahl" Ausgänge, alle gleich wahrscheinlich.
    [Serializable]
    [NodeInfo("Zufällig", KitGroups.Ablauf, "Feuert einen zufälligen Ausgang")]
    public class RandomPick : KitNode
    {
        [Hint("Unter wie vielen Ausgängen gewählt wird: 2 heißt A oder B, 3 heißt A, B oder C.", range: "2 bis 3", edges: "1 oder weniger: immer A. Mehr als 3: zählt als 3.")]
        [Setting("Anzahl")] public int count = 2;

        [Hint("Bekommt das Ereignis, wenn A gewählt wird.")]
        [Output("A")] [NonSerialized] public Output a;
        [Hint("Bekommt das Ereignis, wenn B gewählt wird.")]
        [Output("B")] [NonSerialized] public Output b;
        [Hint("Bekommt das Ereignis, wenn C gewählt wird. Nur bei Anzahl 3.")]
        [Output("C")] [NonSerialized] public Output c;

        [Hint("Wählt einen Ausgang zufällig, alle gleich wahrscheinlich, und gibt das Ereignis dorthin weiter. Ist der gewählte nicht angeschlossen, endet es hier.")]
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
