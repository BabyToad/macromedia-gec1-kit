using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Kraft: stößt einen Rigidbody an. Die Physik rechnet es im nächsten Physik-Schritt aus.
    // Stoß und Tempo wirken einmal, wenn das Event ankommt. Dauerkraft schiebt in jedem Physik-Schritt weiter,
    // bis "Stopp" kommt (oder die Logik aufhört).
    [Serializable]
    [NodeInfo("Kraft", KitGroups.Aktion, "Rigidbody anstoßen oder dauerhaft schieben")]
    public class Push : KitNode
    {
        public enum Kind { Stoß, Dauerkraft, Tempo }

        [Hint("Der Rigidbody, der angestoßen wird. Mit „Is Kinematic“ wirken keine Kräfte.")]
        [Ref("Körper")] public Rigidbody body;
        [Hint("Wohin gestoßen wird, als X, Y, Z. Nur die Richtung zählt, nicht die Länge.", edges: "0, 0, 0: keine Wirkung.")]
        [Setting("Richtung")] public Vector3 direction = Vector3.up;
        [Hint("Wie stark. Bei Stoß und Dauerkraft bewegen sich schwere Körper weniger, bei Tempo bekommt jeder Körper gleich viel Tempo dazu.", range: "Stoß 2 bis 20, Tempo 2 bis 15, Dauerkraft 10 bis 100", edges: "0: keine Wirkung. Negativ: in die Gegenrichtung.")]
        [Setting("Stärke")] public float strength = 5f;
        [Hint("Stoß: ein Schlag, je schwerer der Körper, desto weniger Wirkung. Tempo: ein Schlag, der jedem Körper gleich viel Tempo gibt. Dauerkraft: schiebt ständig weiter, bis „Stopp“ kommt.")]
        [Setting("Art")] public Kind kind = Kind.Stoß;
        [Hint("An: die Richtung dreht sich mit dem Körper (Z ist seine Vorderseite). Aus: Achsen der Welt (Y ist oben).")]
        [Setting("Richtung lokal")] public bool local = true;   // lokal: "nach vorn" ist die Blickrichtung des Körpers

        [Hint("Feuert, sobald die Kraft wirkt; bei Dauerkraft gleich zu Beginn, nicht am Ende.")]
        [Output("Angewendet")] [NonSerialized] public Output applied;

        Coroutine pushing;

        [Hint("Stößt den Körper an. Bei Dauerkraft ersetzt ein neues Ereignis das laufende Schieben.")]
        [Input("Anstoßen")]
        public void Apply(Signal s)
        {
            if (body.isKinematic) { Fail($"„{body.name}“ ist kinematisch: Kräfte wirken nicht"); return; }
            if (kind == Kind.Dauerkraft)
            {
                EndPush();                                       // ein neuer Auftrag ersetzt den laufenden
                pushing = StartWork(Keep(body));
                Running($"schiebt „{body.name}“, bis Stopp kommt");
                applied.Fire(s);
                return;
            }
            var mode = kind == Kind.Stoß ? ForceMode.Impulse : ForceMode.VelocityChange;
            body.AddForce(Dir(body) * strength, mode);
            applied.Fire(s);
        }

        [Hint("Beendet die Dauerkraft. Darf auch von einem ganz anderen Ereignis kommen.")]
        [Input("Stopp", UsesRefs = false)]   // braucht den Körper nicht: auch ein anderes Ereignis darf stoppen
        public void Stop(Signal s)
        {
            if (pushing == null) { Done("schob gerade nicht"); return; }
            EndPush();
            Done("✓ gestoppt");
        }

        Vector3 Dir(Rigidbody b) => (local ? b.transform.TransformDirection(direction) : direction).normalized;

        IEnumerator Keep(Rigidbody b)
        {
            var step = new WaitForFixedUpdate();
            while (true)
            {
                if (b == null) { pushing = null; Fail("◆ Körper wurde gelöscht, während die Kraft wirkte"); yield break; }
                if (!b.isKinematic) b.AddForce(Dir(b) * strength, ForceMode.Force);
                yield return step;
            }
        }

        void EndPush()
        {
            if (pushing != null) StopWork(pushing);
            pushing = null;
        }

        public override void OnStop() { pushing = null; }
    }
}
