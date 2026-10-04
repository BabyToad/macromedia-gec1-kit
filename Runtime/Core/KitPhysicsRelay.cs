using System;
using UnityEngine;

namespace Kit
{
    /// <summary>Added by the kit to a collider's GameObject at runtime; passes Unity's physics messages on.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class KitPhysicsRelay : MonoBehaviour
    {
        public event Action<Collider> TriggerEnter, TriggerExit;
        public event Action<Collision> CollisionEnter;

        public static KitPhysicsRelay On(GameObject go) =>
            go.TryGetComponent(out KitPhysicsRelay r) ? r : go.AddComponent<KitPhysicsRelay>();

        void OnTriggerEnter(Collider c) => TriggerEnter?.Invoke(c);
        void OnTriggerExit(Collider c) => TriggerExit?.Invoke(c);
        void OnCollisionEnter(Collision c) => CollisionEnter?.Invoke(c);
    }
}
