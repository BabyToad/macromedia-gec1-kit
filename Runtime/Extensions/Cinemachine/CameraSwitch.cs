using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    // Kamera wechseln: lässt eine andere Cinemachine-Kamera das Spiel zeigen (Aktivieren), mit einer Kamerafahrt
    // dorthin, und gibt das Bild wieder ab (Deaktivieren): dann zeigt wieder die Kamera davor, am Ende die
    // Spieler-Sicht. Für Zwischensequenzen, einen Blick auf die Tür, die gerade aufging, eine Übersicht.
    [Serializable]
    [NodeInfo("Kamera wechseln", KitGroups.Kamera, "Andere Kamera zeigen lassen, mit Kamerafahrt")]
    public class CameraSwitch : KitNode
    {
        [Ref("Kamera")]
        [Hint("Die Kamera, die übernehmen soll: ein Objekt mit CinemachineCamera, z. B. mit „Kamera: Fest“.")]
        public CinemachineCamera cam;

        [Setting("Übergang")]
        [Hint("So lange dauert die Kamerafahrt (echte Zeit).", unit: "s", range: "0,5 bis 3", edges: "0: harter Schnitt. 1: ruhige Fahrt. 3: langsamer Schwenk.")]
        public float seconds = 1f;

        [Setting("Verlauf")]
        [Hint("Wie die Kamerafahrt verläuft.", edges: "„Weich“ wirkt wie eine Kranfahrt.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt sofort beim Wechsel; die Kamerafahrt läuft dann noch.")] [Output("Gewechselt")] [NonSerialized] public Output switched;

        [Hint("Diese Kamera zeigt ab jetzt das Spiel, mit einer Kamerafahrt dorthin.")] [Input("Aktivieren")]
        public void On(Signal s)
        {
            CameraDirector.Switch(cam, this, seconds, ease);
            Done("✓ zeigt: " + cam.name); switched.Fire(s);
        }

        [Hint("Gibt das Bild an die Kamera davor zurück (am Ende an die Spieler-Sicht).")] [Input("Deaktivieren")]
        public void Off(Signal s)
        {
            if (!CameraDirector.IsSwitchedTo(cam)) { Done($"„{cam.name}“ war nicht aktiviert"); return; }
            CameraDirector.Release(this, seconds, ease, cam);
            Done("✓ zurück"); switched.Fire(s);
        }

        public override void OnStart() => PlayerCameraBridge.Ensure();   // bereit, bevor gewechselt wird: Kamerafahrt statt Schnitt

        public override void OnStop() => CameraDirector.Release(this, 0.5f, Ease.Smooth);   // Interaktion aus: Bild zurückgeben

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (Camera.main == null) problems.Add("Die Szene hat keine Hauptkamera (Tag MainCamera)");
            if (seconds < 0f) problems.Add("Übergang darf nicht negativ sein");
        }
    }
}
