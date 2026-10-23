using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Rolle: sits on every object that plays a role in a Logik-Graph, so the object itself shows what it does in
    /// which graph (its Inspector lists "Rolle: Tür in Tür öffnen", opens the graph at the node, and shows the live
    /// state in Play). The editor adds and removes it; it owns no settings.
    ///
    /// It is also the object's sensor: Unity sends physics messages (OnTriggerEnter …) only to components on the
    /// object itself, so nodes that react to a zone or a collision listen here. Everything an object reacts to is
    /// then visible on it, instead of in a hidden helper component.
    /// </summary>
    [AddComponentMenu("")]                 // not in Add Component: the editor manages it
    [DisallowMultipleComponent]
    public class Role : MonoBehaviour
    {
        /// <summary>One subscription to this object: who listens (usually a KitNode), to what, with which method.</summary>
        public struct Listener
        {
            public object node;      // the delegate's target, usually a KitNode
            public string what;      // "Betreten", "Zusammenstoß" …
            internal Delegate handler;
        }

        /// <summary>The last thing this object sensed, per kind (for the Inspector).</summary>
        public struct Sensed
        {
            public string who;
            public float time;
            public int count;
        }

        Action<Collider> m_TriggerEnter, m_TriggerExit;
        Action<Collision> m_CollisionEnter, m_CollisionExit;
        readonly List<Listener> m_Listeners = new List<Listener>();
        readonly Dictionary<string, Sensed> m_Sensed = new Dictionary<string, Sensed>();
        // Colliders inside this object's trigger right now, kept even while no node listens (a Logik switched off and
        // on again still knows who is inside). Unity sends no exit for colliders that are disabled or destroyed inside,
        // so those are dropped whenever someone asks.
        readonly HashSet<Collider> m_Contacts = new HashSet<Collider>();

        public const string EnterText = "Betreten", ExitText = "Verlassen", HitText = "Zusammenstoß", HitEndText = "Berührung endet";

        /// <summary>Something entered this object's trigger collider ("Is Trigger" on).</summary>
        public event Action<Collider> TriggerEnter { add { m_TriggerEnter += value; Rows(); } remove { m_TriggerEnter -= value; Rows(); } }
        /// <summary>Something left this object's trigger collider.</summary>
        public event Action<Collider> TriggerExit { add { m_TriggerExit += value; Rows(); } remove { m_TriggerExit -= value; Rows(); } }
        /// <summary>A solid collision began (both colliders without "Is Trigger", at least one Rigidbody).</summary>
        public event Action<Collision> CollisionEnter { add { m_CollisionEnter += value; Rows(); } remove { m_CollisionEnter -= value; Rows(); } }
        /// <summary>A solid collision ended.</summary>
        public event Action<Collision> CollisionExit { add { m_CollisionExit += value; Rows(); } remove { m_CollisionExit -= value; Rows(); } }

        /// <summary>The nodes listening right now (Play only).</summary>
        public IReadOnlyList<Listener> Listeners => m_Listeners;

        /// <summary>The last event of one kind (<see cref="EnterText"/> …), if any.</summary>
        public bool TryGetSensed(string what, out Sensed sensed) => m_Sensed.TryGetValue(what, out sensed);

        /// <summary>The object's Rolle. Adds one if the editor has not (e.g. an object that arrived with an event).</summary>
        public static Role Of(GameObject go) => go.TryGetComponent(out Role r) ? r : go.AddComponent<Role>();

        // The rows are read back from the events themselves after every change, so they always show exactly who C#
        // will call (duplicates, multicast delegates and removals that matched nothing included).
        void Rows()
        {
            m_Listeners.Clear();
            Add(m_TriggerEnter, EnterText); Add(m_TriggerExit, ExitText);
            Add(m_CollisionEnter, HitText); Add(m_CollisionExit, HitEndText);
        }

        void Add(Delegate d, string what)
        {
            if (d == null) return;
            foreach (var single in d.GetInvocationList())
                m_Listeners.Add(new Listener { node = single.Target, what = what, handler = single });
        }

        void Note(string what, Component other)
        {
            m_Sensed.TryGetValue(what, out var s);
            var who = other ? (other is Collider c && c.attachedRigidbody ? c.attachedRigidbody.gameObject : other.gameObject) : null;
            m_Sensed[what] = new Sensed { who = who ? who.name : "?", time = Time.time, count = s.count + 1 };
        }

        void OnTriggerEnter(Collider c) { m_Contacts.Add(c); Note(EnterText, c); m_TriggerEnter?.Invoke(c); }
        void OnTriggerExit(Collider c) { m_Contacts.Remove(c); Note(ExitText, c); m_TriggerExit?.Invoke(c); }

        /// <summary>Who counts as one thing entering: the object with the Rigidbody (the player as a whole), else the collider's object.</summary>
        public static GameObject OwnerOf(Collider c) => c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;

        /// <summary>
        /// How many colliders of <paramref name="owner"/> are inside this trigger right now that pass <paramref name="counts"/>
        /// (a node's own filter, e.g. its tag); disabled or destroyed ones never count.
        /// </summary>
        public int ContactsOf(GameObject owner, Predicate<Collider> counts = null)
        {
            Prune();
            int n = 0;
            foreach (var c in m_Contacts) if (OwnerOf(c) == owner && (counts == null || counts(c))) n++;
            return n;
        }

        void Prune() => m_Contacts.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);

        // Every physics step while something is inside: a collider switched off inside (and maybe moved away before it
        // comes back) is forgotten while it is off, so coming back counts as entering again.
        // If this object's own trigger collider was switched off or removed, Unity sends no exits either: nobody is inside.
        void FixedUpdate()
        {
            if (m_Contacts.Count == 0) return;
            if (!HasLiveTrigger()) { m_Contacts.Clear(); return; }
            Prune();
        }

        bool HasLiveTrigger()
        {
            foreach (var c in GetComponents<Collider>()) if (c.enabled && c.isTrigger) return true;
            return false;
        }

        // The zone itself left the simulation (switched off or its object deactivated): nobody is inside any more.
        // Unity sends fresh enter messages when it comes back.
        void OnDisable() => m_Contacts.Clear();
        void OnCollisionEnter(Collision c) { Note(HitText, c.collider); m_CollisionEnter?.Invoke(c); }
        void OnCollisionExit(Collision c) { Note(HitEndText, c.collider); m_CollisionExit?.Invoke(c); }
    }
}
