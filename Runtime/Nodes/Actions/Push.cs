using System;
using UnityEngine;

namespace Kit
{
    // Kraft: stößt einen Rigidbody an. Die Physik rechnet es im nächsten Physik-Schritt aus.
    [Serializable]
    [NodeInfo("Kraft", "Aktion", "Rigidbody anstoßen")]
    public class Push : KitNode
    {
        public enum Kind { Stoß, Dauerkraft, Tempo }

        [Ref("Körper")] public Rigidbody body;
        [Setting("Richtung")] public Vector3 direction = Vector3.up;
        [Setting("Stärke")] public float strength = 5f;
        [Setting("Art")] public Kind kind = Kind.Stoß;
        [Setting("Richtung lokal")] public bool local = true;   // lokal: "nach vorn" ist die Blickrichtung des Körpers

        [Output("Angewendet")] [NonSerialized] public Output applied;

        [Input("Anstoßen")]
        public void Apply(Signal s)
        {
            if (body.isKinematic) { Fail($"„{body.name}“ ist kinematisch: Kräfte wirken nicht"); return; }
            var dir = local ? body.transform.TransformDirection(direction) : direction;
            var mode = kind == Kind.Stoß ? ForceMode.Impulse : kind == Kind.Dauerkraft ? ForceMode.Force : ForceMode.VelocityChange;
            body.AddForce(dir.normalized * strength, mode);
            applied.Fire(s);
        }
    }
}
