using System;
using UnityEngine;

namespace Kit
{
    // Zusammenstoß: zwei Körper prallen mit mindestens diesem Tempo aufeinander.
    // Braucht Rigidbody und Collider ohne "Is Trigger" auf beiden Seiten.
    [Serializable]
    [NodeInfo("Zusammenstoß", "Auslöser", "Physikalischer Aufprall")]
    public class Collide : KitNode
    {
        [Ref("Körper", Permanent = true)] public Collider body;
        [Setting("Nur Tag")] public string requiredTag = "";
        [Setting("Mindesttempo (m/s)")] public float minSpeed = 0f;

        [Output("Getroffen")] [NonSerialized] public Output hit;
        [DataOut("Aufprall (m/s)", EventLocal = true)] [NonSerialized] public float impact;
        [RefOut("Von wem")] [NonSerialized] public GameObject other;

        KitPhysicsRelay relay;

        public override void OnStart()
        {
            relay = KitPhysicsRelay.On(body.gameObject);
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
