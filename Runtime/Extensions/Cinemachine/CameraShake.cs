using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    // Kamera wackeln: löst einen Cinemachine-Impuls aus (Bildschirmwackeln). Form und Stärke stellt die
    // Impulsquelle ein; sichtbar wird es nur an Kameras mit CinemachineImpulseListener.
    [Serializable]
    [NodeInfo("Kamera wackeln (Cinemachine)", KitGroups.Kamera, "Cinemachine-Impuls auslösen")]
    public class CameraShake : KitNode
    {
        [Ref("Impulsquelle")]
        [Hint("Ein Objekt mit CinemachineImpulseSource: bestimmt Form und Richtung des Wackelns.")]
        public CinemachineImpulseSource source;
        [Setting("Stärke")]
        [Hint("Wie stark die Kamera wackelt, als Faktor auf die Impulsquelle.", range: "0,3 bis 3", edges: "0: gar nicht. Groß: heftig.")]
        public float strength = 1f;

        [Hint("Kommt sofort nach dem Auslösen.")] [Output("Ausgelöst")] [NonSerialized] public Output fired;

        [Hint("Lässt die Kamera einmal wackeln.")] [Input("Auslösen")]
        public void Shake(Signal s)
        {
            source.GenerateImpulse(strength);
            Done("✓ Impuls");
            fired.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (UnityEngine.Object.FindAnyObjectByType<CinemachineImpulseListener>() == null)
                problems.Add("Keine Kamera hat einen CinemachineImpulseListener: das Wackeln wäre unsichtbar");
        }
    }
}
