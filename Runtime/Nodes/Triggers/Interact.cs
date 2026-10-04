using System;
using UnityEngine;

namespace Kit
{
    // Benutzen: der Spieler schaut DIESES Objekt an und drückt die Taste.
    // (Im alten BlockoutKit reichte irgendein Treffer. Das ist hier behoben.)
    [Serializable]
    [NodeInfo("Benutzen", "Auslöser", "Objekt anschauen und Taste drücken")]
    public class Interact : KitNode
    {
        [Ref("Objekt", Permanent = true)] public GameObject target;                       // ◆ was man benutzen kann
        [Ref("Kamera", Optional = true, Permanent = true)] public Camera viewCamera;    // leer = Camera.main
        [Setting("Taste")] public KeyCode key = KeyCode.E;
        [Setting("Reichweite (m)")] public float range = 3f;

        [Output("Benutzt")] [NonSerialized] public Output used;
        [RefOut("Wer")] [NonSerialized] public GameObject who;   // das Objekt mit der Kamera

        bool lost;

        public override void Tick()
        {
            if (lost) return;
            if (target == null) { lost = true; Fail("◆ Objekt wurde gelöscht: hier kann nichts mehr benutzt werden"); return; }   // einmal melden
            if (!KitInput.Down(key)) return;
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) { Fail("keine Kamera (Tag MainCamera fehlt)"); return; }

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            // Trigger-Collider zählen nicht: sonst würde eine Zone davor den Blick abfangen.
            if (!Physics.Raycast(ray, out var hit, range, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.transform != target.transform && !hit.transform.IsChildOf(target.transform)) return;

            who = cam.transform.root.gameObject;
            Done("✓ Benutzt");
            used.Fire(new Signal());
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (target != null && target.GetComponentInChildren<Collider>() == null)
                problems.Add($"„{target.name}“ hat keinen Collider: der Blick kann es nicht treffen");
        }
    }
}
