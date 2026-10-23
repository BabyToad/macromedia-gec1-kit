using System;
using UnityEngine;

namespace Kit
{
    // Zusammenstoß: zwei Körper prallen mit mindestens diesem Tempo aufeinander.
    // Braucht Rigidbody und Collider ohne "Is Trigger" auf beiden Seiten.
    [Serializable]
    [NodeInfo("Zusammenstoß", KitGroups.Ausloeser, "Wenn etwas hart genug aufprallt")]
    public class Collide : KitNode
    {
        [Hint("Der Körper, dessen Aufprall zählt. Braucht einen Collider ohne „Is Trigger“; mindestens einer der beiden Beteiligten braucht einen Rigidbody.")]
        [Ref("Körper", Permanent = true)] public Collider body;
        [Hint("Nur Aufpralle mit Objekten, die diesen Tag tragen, zählen. Der Tag muss am Objekt mit dem Collider sitzen.", edges: "Leer: jeder Aufprall zählt.")]
        [Setting("Nur Tag")] public string requiredTag = "";
        [Hint("Wie schnell die beiden aufeinanderprallen müssen, damit es zählt. Gemessen wird das Tempo der beiden zueinander.", unit: "m/s", range: "0 bis 10", edges: "0: jede Berührung zählt.")]
        [Setting("Mindesttempo")] public float minSpeed = 0f;

        [Hint("Feuert beim Aufprall. ◆ Von wem und ● Aufprall gehören zu diesem Ereignis.")]
        [Output("Getroffen")] [NonSerialized] public Output hit;
        [Hint("Wie schnell die beiden beim Aufprall aufeinander zukamen.", unit: "m/s")]
        [DataOut("Aufprall", EventLocal = true)] [NonSerialized] public float impact;
        [Hint("Was den Körper getroffen hat: das Objekt mit dem Rigidbody, sonst das Objekt mit dem Collider.")]
        [RefOut("Von wem")] [NonSerialized] public GameObject other;

        Role relay;   // die Rolle auf dem Körper meldet den Aufprall

        public override void OnStart()
        {
            relay = Role.Of(body.gameObject);
            relay.CollisionEnter += OnHit;
        }

        public override void OnStop() { if (relay != null) relay.CollisionEnter -= OnHit; }

        void OnHit(Collision c)
        {
            if (!string.IsNullOrEmpty(requiredTag) && !c.collider.CompareTag(requiredTag)) return;
            if (c.relativeVelocity.magnitude < minSpeed) return;
            impact = c.relativeVelocity.magnitude;
            other = c.collider.attachedRigidbody ? c.collider.attachedRigidbody.gameObject : c.collider.gameObject;
            Done($"✓ Getroffen von {other.name} ({impact:0.0} m/s)");
            hit.Fire(new Signal());
        }
    }
}
