using System;
using UnityEngine;

namespace Kit
{
    // Kamera wackeln: lässt die Kamera des Spielers kurz wackeln (Treffer, Explosion, Landung). Braucht kein
    // Cinemachine: die Spieler-Kamera rechnet den Versatz selbst dazu. Ohne ◆ Kamera nimmt der Knoten die
    // Spieler-Kamera der Szene.
    [Serializable]
    [NodeInfo("Kamera wackeln", "Spielsysteme", "Spieler-Kamera kurz wackeln lassen")]
    public class PlayerShake : KitNode
    {
        [Ref("Kamera", Optional = true)] public KitPlayerCamera playerCamera;
        [Setting("Stärke (m)")] public float strength = 0.15f;
        [Setting("Dauer (s)")] public float seconds = 0.25f;

        [Output("Ausgelöst")] [NonSerialized] public Output fired;

        [Input("Wackeln")]
        public void Shake(Signal s)
        {
            var cam = playerCamera ? playerCamera : UnityEngine.Object.FindAnyObjectByType<KitPlayerCamera>();
            if (!cam) { Fail("Keine Spieler-Kamera in der Szene (Prefab Spieler mit Kamera)"); return; }
            if (strength <= 0f || seconds <= 0f) { Fail("Stärke und Dauer müssen größer als 0 sein"); return; }
            cam.Shake(strength, seconds);
            Done($"✓ wackelt {seconds:0.##} s");
            fired.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (strength <= 0f || seconds <= 0f) problems.Add("Stärke und Dauer müssen größer als 0 sein");
        }
    }
}
