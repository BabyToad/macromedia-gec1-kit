using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Base of every Atmosphäre zone (Musik-Zone, Nebel-Zone, Bildstimmungs-Zone, Kamera-Zone): a trigger collider
    /// that notices when the player walks in and out. Several zones can sit on the same box and compose: one
    /// collider, music + fog + camera change together. Nested and overlapping zones work: the zone entered last
    /// wins, and leaving it gives back to the one still around the player.
    /// </summary>
    public abstract class AtmosphereZone : MonoBehaviour
    {
        [Hint("Nur Objekte mit diesem Tag lösen die Zone aus. „Player“ ist der Spieler des Kits.", edges: "Leer: alles, was hineinkommt.", label: "Nur Tag")]
        public string requiredTag = "Player";

        [Hint("Wie lange der Übergang beim Betreten und Verlassen dauert (echte Zeit, auch in Pause).", unit: "s", range: "0,5 bis 4", edges: "0: springt sofort um.", label: "Übergang")]
        public float seconds = 1.5f;

        [Hint("Wie der Übergang über die Dauer verläuft.", edges: "„Weich“ passt fast immer; „Sofort“ springt.", label: "Verlauf")]
        public Ease ease = Ease.Smooth;

        // Every collider that overlaps right now. A player made of several colliders leaves only when the last one has.
        readonly HashSet<Collider> m_Inside = new HashSet<Collider>();
        static readonly List<Collider> s_Prune = new List<Collider>();

        /// <summary>Is someone who counts inside right now?</summary>
        public bool IsOccupied => m_Inside.Count > 0;
        /// <summary>Who is inside right now (for the Inspector): one entry per object, not per collider.</summary>
        public IEnumerable<GameObject> Inside { get { foreach (var g in new HashSet<GameObject>(Alive())) yield return g; } }

        IEnumerable<GameObject> Alive() { foreach (var c in m_Inside) if (c) yield return Who(c); }

        /// <summary>The player (or whatever counts) walked in; the zone was empty before.</summary>
        protected abstract void Entered();
        /// <summary>The last one who counts left the zone (or the zone was switched off).</summary>
        protected abstract void Left();
        /// <summary>Colour of the zone's box in the Scene view.</summary>
        protected abstract Color GizmoColor { get; }
        /// <summary>Short label drawn in the Scene view, e.g. "Musik: Höhle".</summary>
        protected abstract string GizmoLabel { get; }

        protected virtual void Reset()
        {
            // Add Component on an empty object: a box that is a trigger, ready to scale. Existing collider: make it a trigger.
            var c = GetComponent<Collider>();
            if (!c) c = gameObject.AddComponent<BoxCollider>();
            c.isTrigger = true;
        }

        GameObject Who(Collider other) => other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        bool Counts(Collider other) => string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag) || Who(other).CompareTag(requiredTag);

        Role m_Role;   // the object's Rolle senses the trigger; the zone listens, so the object shows what it reacts to

        bool m_Started;

        protected virtual void OnEnable()
        {
            m_Role = Role.Of(gameObject);
            m_Role.TriggerEnter += Enter;
            m_Role.TriggerExit += Exit;
            if (Application.isPlaying && m_Started) Reconcile();   // switched on again later: look at once
        }

        // First time: after the scene has finished loading (sceneLoaded resets the shared fog/music state after OnEnable).
        protected virtual void Start()
        {
            m_Started = true;
            Reconcile();
        }

        // Switched on while someone already stands inside: no enter event will come, so look for them.
        // The same colliders count as for Enter, triggers included.
        void Reconcile()
        {
            var own = GetComponent<Collider>();
            if (!own || !own.enabled) return;
            var b = own.bounds;
            foreach (var other in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
            {
                if (other == own || !other.enabled || !Counts(other) || !Overlaps(own, other)) continue;
                bool wasEmpty = m_Inside.Count == 0;
                if (m_Inside.Add(other) && wasEmpty) Entered();
            }
        }

        // The real shapes, not their bounding boxes: a player next to a round or turned zone is not inside it.
        static bool Overlaps(Collider own, Collider other)
        {
            if (other is CharacterController cc)
            {
                // ComputePenetration does not know CharacterControllers: ask physics which colliders touch its capsule.
                var t = cc.transform;
                float r = cc.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z);
                float half = Mathf.Max(0f, cc.height * t.lossyScale.y * 0.5f - r);
                var c = t.TransformPoint(cc.center);
                // Selten gebraucht (nur beim Einschalten): die vollständige Abfrage, ohne Obergrenze an Treffern.
                foreach (var hit in Physics.OverlapCapsule(c - t.up * half, c + t.up * half, r, ~0, QueryTriggerInteraction.Collide))
                    if (hit == own) return true;
                return false;
            }
            return Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                                              other, other.transform.position, other.transform.rotation, out _, out _);
        }

        void Enter(Collider other)
        {
            if (!enabled || !Counts(other)) return;
            bool wasEmpty = m_Inside.Count == 0;
            if (m_Inside.Add(other) && wasEmpty) Entered();
        }

        void Exit(Collider other)
        {
            if (!m_Inside.Remove(other)) return;
            if (m_Inside.Count == 0 && enabled) Left();
        }

        static bool Gone(Collider c) => !c || !c.enabled || !c.gameObject.activeInHierarchy;

        void Update()
        {
            if (m_Inside.Count == 0) return;
            // A collider switched off, destroyed or deactivated inside the zone never sends OnTriggerExit.
            // The zone's own collider switched off: nothing is inside any more.
            var own = GetComponent<Collider>();
            if (Gone(own)) { m_Inside.Clear(); Left(); return; }
            s_Prune.Clear();
            foreach (var c in m_Inside) if (Gone(c)) s_Prune.Add(c);
            if (s_Prune.Count == 0) return;
            foreach (var c in s_Prune) m_Inside.Remove(c);
            if (m_Inside.Count == 0) Left();
        }

        protected virtual void OnDisable()
        {
            if (m_Role) { m_Role.TriggerEnter -= Enter; m_Role.TriggerExit -= Exit; }
            if (m_Inside.Count == 0) return;
            m_Inside.Clear();
            Left();
        }

#if UNITY_EDITOR
        void OnDrawGizmos() => DrawZone(0.06f);
        void OnDrawGizmosSelected() { DrawZone(0.18f); DrawSelected(); }

        /// <summary>Extra Scene-view drawing while the zone is selected (e.g. a line to its camera).</summary>
        protected virtual void DrawSelected() { }

        void DrawZone(float fill)
        {
            var c = GetComponent<Collider>();
            if (!c) return;
            var col = GizmoColor;
            if (Application.isPlaying && IsOccupied) col = Color.Lerp(col, Color.white, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (c is BoxCollider b)
            {
                Gizmos.color = new Color(col.r, col.g, col.b, fill); Gizmos.DrawCube(b.center, b.size);
                Gizmos.color = new Color(col.r, col.g, col.b, 0.9f); Gizmos.DrawWireCube(b.center, b.size);
            }
            else if (c is SphereCollider s)
            {
                Gizmos.color = new Color(col.r, col.g, col.b, fill); Gizmos.DrawSphere(s.center, s.radius);
                Gizmos.color = new Color(col.r, col.g, col.b, 0.9f); Gizmos.DrawWireSphere(s.center, s.radius);
            }
            Gizmos.matrix = Matrix4x4.identity;
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel) { normal = { textColor = col } };
            UnityEditor.Handles.Label(c.bounds.center + Vector3.up * (c.bounds.extents.y + 0.2f), GizmoLabel, style);
        }

        protected virtual void OnValidate()
        {
            seconds = Mathf.Max(0f, seconds);
        }
#endif
    }

    /// <summary>
    /// A value that several zones and nodes want at once. Each one adds a wish; the newest wish wins; taking a wish
    /// away falls back to the one before, and finally to the base value. Used for music layers, fog and post FX.
    /// </summary>
    public sealed class Wishes<T>
    {
        readonly List<(object owner, T value)> m_List = new List<(object, T)>();
        public T Base;
        public Wishes(T @base) { Base = @base; }

        public T Current => m_List.Count > 0 ? m_List[m_List.Count - 1].value : Base;
        public int Count => m_List.Count;

        /// <summary>Adds or renews this owner's wish (it becomes the newest).</summary>
        public void Set(object owner, T value) { Remove(owner); m_List.Add((owner, value)); }
        /// <summary>Takes this owner's wish away. True if it had one.</summary>
        public bool Remove(object owner) => m_List.RemoveAll(w => Equals(w.owner, owner)) > 0;
        public bool Has(object owner) => m_List.Exists(w => Equals(w.owner, owner));
        /// <summary>Drops wishes whose owner (a zone, a node's Logik) no longer exists. True if one was dropped.</summary>
        public bool PruneDead() => m_List.RemoveAll(w => w.owner is Object o && !o) > 0;

        /// <summary>Whose wish counts right now; null = nobody's (the base value).</summary>
        public object NewestOwner => m_List.Count > 0 ? m_List[m_List.Count - 1].owner : null;

        /// <summary>Is this owner's wish the one that counts?</summary>
        public bool IsNewest(object owner) => m_List.Count > 0 && Equals(m_List[m_List.Count - 1].owner, owner);
    }
}
