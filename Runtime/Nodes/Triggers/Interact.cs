using System;
using UnityEngine;

namespace Kit
{
    // Benutzen: der Spieler schaut DIESES Objekt an und drückt die Taste.
    // (Im alten BlockoutKit reichte irgendein Treffer. Das ist hier behoben.)
    [Serializable]
    [NodeInfo("Benutzen", KitGroups.Ausloeser, "Objekt anschauen und Taste drücken")]
    public class Interact : KitNode
    {
        [Hint("Was benutzt werden kann. Braucht einen Collider ohne „Is Trigger“, auch an einem Kindobjekt, damit der Blick es trifft.")]
        [Ref("Objekt", Permanent = true)] public GameObject target;                       // ◆ was man benutzen kann
        [Hint("Von welcher Kamera aus geschaut wird.", edges: "Leer: die Hauptkamera (Tag „MainCamera“).")]
        [Ref("Kamera", Optional = true, Permanent = true)] public Camera viewCamera;    // leer = Camera.main
        [Hint("Welche Taste das Benutzen auslöst.")]
        [Setting("Taste")] public KeyCode key = KeyCode.E;
        [Hint("Wie weit das Objekt höchstens vom Spieler entfernt sein darf, in Blickrichtung gemessen. Steht etwas Festes dazwischen, zählt es nicht.", unit: "m", range: "1,5 bis 5", edges: "Gilt in der Ich-Perspektive und in der Ansicht von hinten gleich.")]
        [Setting("Reichweite")] public float range = 3f;

        [Hint("Feuert, wenn die Taste gedrückt wird, während der Blick genau auf das Objekt fällt und es in Reichweite ist.")]
        [Output("Benutzt")] [NonSerialized] public Output used;
        [Hint("Feuert, wenn der Blick das Objekt in Reichweite trifft. Nur ein Signal: was der Spieler dann sieht oder hört (Umriss, Hinweis, Ton), wird selbst gebaut.")]
        [Output("Im Blick")] [NonSerialized] public Output focused;
        [Hint("Feuert, wenn der Blick das Objekt wieder verlässt (weggeschaut, zu weit weg, etwas davor).")]
        [Output("Nicht mehr im Blick")] [NonSerialized] public Output unfocused;
        [Hint("Wer benutzt hat: der Spieler, für den die Kamera schaut (ohne Spieler: die Kamera selbst).")]
        [RefOut("Wer")] [NonSerialized] public GameObject who;   // das Objekt mit der Kamera
        [Hint("Ja, solange der Blick das Objekt in Reichweite trifft.")]
        [DataOut("Im Blick")] [NonSerialized] public bool inView;

        bool lost;

        public override void Tick()
        {
            if (lost) return;
            if (target == null) { lost = true; Fail("◆ Objekt wurde gelöscht: hier kann nichts mehr benutzt werden"); return; }   // einmal melden
            // Der Blick wird nur geprüft, wenn ihn jemand braucht: die Taste oder ein angeschlossenes Im Blick (▶ oder ●).
            bool pressed = KitInput.Down(key);
            if (!pressed && !watch && !inView) return;
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null)
            {
                if (inView) { inView = false; Done("✓ nicht mehr im Blick (keine Kamera)"); unfocused.Fire(new Signal()); }
                if (pressed) Fail("keine Kamera: eine Kamera mit dem Tag MainCamera in die Szene legen oder ◆ Kamera verbinden");
                return;
            }

            var body = BodyOf(cam);
            bool looking = Looks(cam, body);
            if (looking != inView)
            {
                inView = looking;
                who = body ? body.gameObject : cam.gameObject;
                Done(looking ? "✓ im Blick" : "✓ nicht mehr im Blick");
                (looking ? focused : unfocused).Fire(new Signal());
            }
            if (!pressed || !looking) return;

            who = body ? body.gameObject : cam.gameObject;
            Done("✓ Benutzt");
            used.Fire(new Signal());
        }

        bool Looks(Camera cam, Transform body)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            // Die Reichweite zählt ab dem Spieler, nicht ab der Kamera: in der Ansicht von hinten sitzt die Kamera
            // ein paar Meter hinter ihm. "behind" ist dieser Abstand entlang des Blicks (Ich-Perspektive: etwa 0).
            float behind = body ? Mathf.Max(0f, Vector3.Dot(body.position - ray.origin, ray.direction)) : 0f;
            // Trigger-Collider zählen nicht (sonst finge eine Zone davor den Blick ab), der eigene Körper auch nicht.
            var hits = Physics.RaycastAll(ray, behind + range, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (body && h.transform.IsChildOf(body)) continue;
                if (h.distance - behind > range) return false;
                return h.transform == target.transform || h.transform.IsChildOf(target.transform);
            }
            return false;
        }

        // Wer schaut: der Körper, für den die Kamera schaut (die Spieler-Kamera sagt es selbst), sonst ein Spieler
        // (CharacterController) über der Kamera. Ohne beides zählt die Kamera allein.
        static Transform BodyOf(Camera cam)
        {
            var viewer = cam.GetComponentInParent<IKitViewer>();
            if (viewer != null && viewer.Body) return viewer.Body;
            var cc = cam.GetComponentInParent<CharacterController>();
            return cc ? cc.transform : null;
        }

        bool watch;   // someone uses Im Blick: worked out once per start, so the ray costs nothing otherwise

        public override void OnStart()
        {
            inView = false;
            watch = focused.IsConnected || unfocused.IsConnected;
            var graph = Runner.Graph;
            foreach (var l in graph.links)
                if (l.kind == LogicGraph.LinkKind.Data && l.from == index && l.fromPort == nameof(inView)) { watch = true; break; }
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (target != null && target.GetComponentInChildren<Collider>() == null)
                problems.Add($"„{target.name}“ hat keinen Collider: der Blick kann es nicht treffen");
        }
    }
}
