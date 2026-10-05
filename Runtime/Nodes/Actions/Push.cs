using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Kraft: stößt einen Rigidbody an. Die Physik rechnet es im nächsten Physik-Schritt aus.
    // Stoß und Tempo wirken einmal, wenn das Event ankommt. Dauerkraft schiebt in jedem Physik-Schritt weiter,
    // bis "Stopp" kommt (oder die Interaktion aufhört).
    [Serializable]
    [NodeInfo("Kraft", "Aktion", "Rigidbody anstoßen oder dauerhaft schieben")]
    public class Push : KitNode
    {
        public enum Kind { Stoß, Dauerkraft, Tempo }

        [Ref("Körper")] public Rigidbody body;
        [Setting("Richtung")] public Vector3 direction = Vector3.up;
        [Setting("Stärke")] public float strength = 5f;
        [Setting("Art")] public Kind kind = Kind.Stoß;
        [Setting("Richtung lokal")] public bool local = true;   // lokal: "nach vorn" ist die Blickrichtung des Körpers

        [Output("Angewendet")] [NonSerialized] public Output applied;

        Coroutine pushing;

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
