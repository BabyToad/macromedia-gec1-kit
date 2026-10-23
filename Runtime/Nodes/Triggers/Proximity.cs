using System;
using UnityEngine;

namespace Kit
{
    // In der Nähe: prüft jeden Frame den Abstand zwischen zwei Objekten. Ohne Collider.
    [Serializable]
    [NodeInfo("In der Nähe", KitGroups.Ausloeser, "Abstand wird kleiner oder größer als der Radius")]
    public class Proximity : KitNode
    {
        [Hint("Der Punkt, um den herum gemessen wird. Braucht keinen Collider.")]
        [Ref("Mitte", Permanent = true)] public Transform center;
        [Hint("Das Objekt, dessen Abstand zur Mitte zählt, z.B. der Spieler.")]
        [Ref("Wer", Permanent = true)] public Transform other;
        [Hint("Ab welchem Abstand „Kommt näher“ feuert. Gemessen wird in alle Richtungen, auch nach oben.", unit: "m", range: "1 bis 20", edges: "0 oder negativ: feuert praktisch nie.")]
        [Setting("Radius")] public float radius = 3f;

        [Hint("Feuert, wenn „Wer“ den Radius von außen nach innen überschreitet. Steht er beim Start schon drinnen, feuert es nicht.")]
        [Output("Kommt näher")] [NonSerialized] public Output approached;
        [Hint("Feuert, wenn „Wer“ den Radius wieder nach außen verlässt.")]
        [Output("Entfernt sich")] [NonSerialized] public Output left;
        [Hint("Der aktuelle Abstand zwischen Mitte und Wer.", unit: "m", edges: "-1: eins der beiden Objekte fehlt.")]
        [DataOut("Abstand")] public float Distance => center && other ? Vector3.Distance(center.position, other.position) : -1f;

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
