using System;
using UnityEngine;

namespace Kit
{
    // In der Nähe: prüft jeden Frame den Abstand zwischen zwei Objekten. Ohne Collider.
    [Serializable]
    [NodeInfo("In der Nähe", "Auslöser", "Abstand wird kleiner oder größer als der Radius")]
    public class Proximity : KitNode
    {
        [Ref("Mitte", Permanent = true)] public Transform center;
        [Ref("Wer", Permanent = true)] public Transform other;
        [Setting("Radius (m)")] public float radius = 3f;

        [Output("Kommt näher")] [NonSerialized] public Output approached;
        [Output("Entfernt sich")] [NonSerialized] public Output left;
        [DataOut("Abstand (m)")] public float Distance => center && other ? Vector3.Distance(center.position, other.position) : -1f;

        bool inside, lost;

        public override void OnStart() => inside = Distance >= 0 && Distance <= radius;

        public override void Tick()
        {
            if (!center || !other)
            {
                if (!lost) Fail(!center ? "◆ Mitte wurde gelöscht" : "◆ Wer wurde gelöscht");   // einmal melden
                lost = true;
                return;
            }
            bool now = Distance <= radius;
            if (now == inside) return;
            inside = now;
            Done(now ? "✓ Kommt näher" : "✓ Entfernt sich");
            (now ? approached : left).Fire(new Signal());
        }

        public override string Readout() => Distance >= 0 ? $"Abstand {Distance:0.0} m" : null;
    }
}
