using System;
using UnityEngine;

namespace Kit
{
    // Kamera wackeln: lässt die Kamera des Spielers kurz wackeln (Treffer, Explosion, Landung). Braucht kein
    // Cinemachine: die Spieler-Kamera rechnet den Versatz selbst dazu. Ohne ◆ Kamera nimmt der Knoten die
    // Spieler-Kamera der Szene.
    [Serializable]
    [NodeInfo("Kamera wackeln", KitGroups.Spielsysteme, "Spieler-Kamera kurz wackeln lassen")]
    public class PlayerShake : KitNode
    {
        [Hint("Die Kamera des Spielers (Komponente „Spieler-Kamera“).", edges: "Leer: die erste Spieler-Kamera in der Szene.")]
        [Ref("Kamera", Optional = true)] public KitPlayerCamera playerCamera;
        [Hint("Wie weit die Kamera höchstens ausschlägt.", unit: "m", range: "0,05 bis 0,5", edges: "0 oder negativ: Fehler, nichts wackelt. Über 1: das Bild springt.")]
        [Setting("Stärke")] public float strength = 0.15f;
        [Hint("Wie lange das Wackeln ausklingt. Läuft in echter Zeit, also auch während das Spiel angehalten ist.", unit: "s", range: "0,1 bis 1", edges: "0 oder negativ: Fehler, nichts wackelt.")]
        [Setting("Dauer")] public float seconds = 0.25f;

        [Hint("Feuert, sobald das Wackeln beginnt, nicht erst am Ende.")]
        [Output("Ausgelöst")] [NonSerialized] public Output fired;

        [Hint("Lässt die Kamera wackeln. Ein laufendes Wackeln wird dadurch nicht schwächer, es beginnt neu.")]
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
