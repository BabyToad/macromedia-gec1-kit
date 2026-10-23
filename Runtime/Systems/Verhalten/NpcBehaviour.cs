using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    /// <summary>The behaviours a figure can switch between. <see cref="Stehen"/> = none: the figure stands still.</summary>
    public enum BehaviourKind { Stehen, Patrouillieren, Umherstreifen, Wachen, Folgen, Fliehen, Zurückkehren, Nachsehen }

    /// <summary>
    /// Base of every Verhalten component (Add Component › Verhalten › …). Each one only moves the figure; the
    /// <see cref="Senses"/> on the same object notice things, and the graph decides what follows what.
    /// Exactly one behaviour per figure is switched on: the Inspector's checkbox *is* the current state.
    /// Switching one on (graph node „Verhalten wechseln“, or the checkbox during Play) switches the others off.
    /// At the start, the first switched-on behaviour from the top wins.
    /// Behaviours never fire graph events while starting; reports reach the graph a frame later, from Update.
    /// </summary>
    [RequireComponent(typeof(Senses))]
    public abstract class NpcBehaviour : MonoBehaviour
    {
        [Hint("Optionaler Name, wenn eine Figur dasselbe Verhalten mehrmals hat, z. B. zwei Patrouillen „Tag“ und „Nacht“. „Verhalten wechseln“ wählt dann mit diesem Namen.", edges: "Leer: „Verhalten wechseln“ nimmt das erste dieser Art.", label: "Name")]
        public string label = "";

        [Hint("Gehtempo in diesem Verhalten. Zum Vergleich: Spieler gehen 4 m/s und rennen 7 m/s.", unit: "m/s", range: "1 bis 8", edges: "Langsamer als der Spieler: man entkommt. Schneller: man muss sich verstecken.", label: "Tempo")]
        [Min(0f)] public float speed = 2f;

        /// <summary>How students find the presets, for messages: "Add Component › Kit › Verhalten › ".</summary>
        public const string MenuPath = "Add Component › " + KitBrand.Name + " › Verhalten › ";

        /// <summary>Which preset this is.</summary>
        public abstract BehaviourKind Kind { get; }

        /// <summary>What the behaviour is doing right now, in German (Inspector, graph, gizmo label). Formatted on demand.</summary>
        public string Status
        {
            get
            {
                if (m_StatusFormat == null) return "";
                var name = m_StatusAbout != null ? m_StatusAbout.name : "";
                return string.Format(m_StatusFormat, m_StatusA, m_StatusB, name);
            }
        }

        string m_StatusFormat = "";
        float m_StatusA, m_StatusB;
        UnityEngine.Object m_StatusAbout;

        /// <summary>
        /// Sets the live status without building a string every frame: <paramref name="format"/> is a constant with
        /// {0} = <paramref name="a"/>, {1} = <paramref name="b"/>, {2} = the name of <paramref name="about"/>.
        /// </summary>
        protected void Say(string format, float a = 0f, float b = 0f, UnityEngine.Object about = null)
        {
            m_StatusFormat = format; m_StatusA = a; m_StatusB = b; m_StatusAbout = about;
        }

        protected NavMeshAgent Agent { get; private set; }
        protected Senses Senses { get; private set; }

        /// <summary>The figure's agent (added automatically at Play if missing).</summary>
        public NavMeshAgent NavAgent => Agent;
        /// <summary>The figure's senses on the same object.</summary>
        public Senses FigureSenses => Senses;
        /// <summary>True from the first activation on (lets behaviours resume instead of restarting).</summary>
        protected bool WasActiveBefore { get; private set; }
        /// <summary>Added at runtime by „Verhalten wechseln“ because the figure did not have it.</summary>
        public bool AddedAtRuntime { get; internal set; }

        bool m_Prepared;
        bool m_RotationOverridden;
        Vector3 m_Dest; bool m_HasDest; float m_NextRetry;
        int m_Activation;   // changes on every switch on/off: work started before a switch knows it is stale

        // ---------------------------------------------------------------------------------- lifecycle
        protected virtual void Awake()
        {
            Prepare();
            // Starting state: the first switched-on behaviour from the top. Later ones step back.
            if (enabled)
                foreach (var other in GetComponents<NpcBehaviour>())
                {
                    if (other == this) break;
                    if (other.enabled) { enabled = false; break; }
                }
        }

        void Prepare()
        {
            if (m_Prepared) return;
            m_Prepared = true;
            Senses = GetComponent<Senses>();
            Senses.Init();
            KitNavigation.Ensure();   // bake first, then add the agent: it lands on the NavMesh without a warning
            Agent = GetComponent<NavMeshAgent>();
            if (Agent == null) Agent = AddAgent(gameObject);
            if (TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.isKinematic = true;   // the agent moves the body, not physics
            SnapToNavMesh();
        }

        /// <summary>Adds a NavMeshAgent sized to the object's collider.</summary>
        public static NavMeshAgent AddAgent(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            var a = go.AddComponent<NavMeshAgent>();
            if (c != null)
            {
                var b = c.bounds;
                a.radius = Mathf.Max(0.1f, Mathf.Min(b.extents.x, b.extents.z));
                a.height = Mathf.Max(0.2f, b.size.y);
                a.baseOffset = go.transform.position.y - b.min.y;
            }
            a.angularSpeed = 540f;
            a.acceleration = 16f;
            a.autoBraking = true;
            return a;
        }

        protected virtual void OnEnable()
        {
            Prepare();
            m_Activation++;
            Senses.Current = this;   // first, so the ones switched off below do not report "Stehen" in between
            foreach (var other in GetComponents<NpcBehaviour>())
                if (other != this && other.enabled) other.enabled = false;
            ApplyAgentSettings();
            Begin();
            WasActiveBefore = true;
            Senses.RaiseSwitched(this);
        }

        protected virtual void OnDisable()
        {
            m_Activation++;
            End();
            ReleaseRotation();
            m_HasDest = false;
            if (Agent != null && Agent.isOnNavMesh) Agent.ResetPath();
            if (Senses != null && Senses.Current == this)
            {
                Senses.Current = null;
                if (gameObject.activeInHierarchy) Senses.RaiseSwitched(null);
            }
        }

        void Update()
        {
            if (Agent == null) return;
            if (!Agent.isOnNavMesh) { SnapToNavMesh(); if (!Agent.isOnNavMesh) { Say("steht nicht auf begehbarem Boden"); return; } }
            // A goal that was out of reach (a closed door): ask again now and then, the NavMesh follows the level.
            // Also when the path was lost (the NavMesh was swapped under the figure) before it arrived.
            if (m_HasDest && !Agent.pathPending && Time.time >= m_NextRetry &&
                (Agent.pathStatus != NavMeshPathStatus.PathComplete || !Agent.hasPath && !AtDestination))
            {
                m_NextRetry = Time.time + 0.5f;
                Agent.SetDestination(m_Dest);
            }
            Tick();
        }

        /// <summary>Puts the figure onto the NavMesh if it stands beside it (placed before the bake, dropped from a height).</summary>
        internal void SnapToNavMesh()
        {
            if (Agent == null || Agent.isOnNavMesh || !Agent.enabled) return;
            var feet = new Vector3(transform.position.x, Senses.Feet(transform), transform.position.z);
            if (NavMesh.SamplePosition(feet, out var hit, 3f, NavMesh.AllAreas)) Agent.Warp(hit.position);
        }

        protected virtual void ApplyAgentSettings()
        {
            Agent.speed = speed;
            Agent.stoppingDistance = 0.2f;
            if (Agent.isOnNavMesh) Agent.isStopped = false;
        }

        /// <summary>The behaviour was switched on: pick the first goal. Do not fire events here.</summary>
        protected abstract void Begin();
        /// <summary>Every frame while switched on and on the NavMesh.</summary>
        protected abstract void Tick();
        /// <summary>The behaviour was switched off: forget running work.</summary>
        protected virtual void End() { }

        // ---------------------------------------------------------------------------------- helpers for presets
        /// <summary>Walks to <paramref name="point"/>. False if no NavMesh point is near it.</summary>
        protected bool GoTo(Vector3 point)
        {
            ReleaseRotation();
            if (!Agent.isOnNavMesh || !NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas)) return false;
            Agent.isStopped = false;
            m_Dest = hit.position; m_HasDest = true; m_NextRetry = Time.time + 0.5f;
            return Agent.SetDestination(m_Dest);
        }

        /// <summary>
        /// The figure stands at its goal (or has none). Measured to the goal itself, so the end of a path that
        /// stops short (a closed door) does not count as arrived.
        /// </summary>
        protected bool AtDestination
        {
            get
            {
                if (!m_HasDest) return true;
                if (Agent.pathPending) return false;
                if (Agent.pathStatus == NavMeshPathStatus.PathInvalid) return false;
                var here = transform.position;
                float d = Vector2.Distance(new Vector2(here.x, here.z), new Vector2(m_Dest.x, m_Dest.z));
                // Height counts too: a goal on an unreachable floor right above is not reached. Feet = pivot minus
                // the agent's base offset; the goal lies on the NavMesh surface.
                float feet = here.y - Agent.baseOffset;
                return d <= Agent.stoppingDistance + 0.25f && Mathf.Abs(feet - m_Dest.y) <= Mathf.Max(1.25f, Agent.height * 0.6f);
            }
        }

        /// <summary>The goal cannot be reached right now: the path ends short of it and the figure is there.</summary>
        protected bool PathBlocked =>
            m_HasDest && !Agent.pathPending && Agent.pathStatus != NavMeshPathStatus.PathComplete &&
            (!Agent.hasPath || Agent.remainingDistance <= Agent.stoppingDistance + 0.05f);

        protected void Halt()
        {
            m_HasDest = false;
            if (Agent.isOnNavMesh) Agent.ResetPath();
        }

        /// <summary>Turns toward a direction at <paramref name="degreesPerSecond"/>. Takes rotation over from the agent.</summary>
        protected void TurnTo(Vector3 direction, float degreesPerSecond = 240f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            if (!m_RotationOverridden) { Agent.updateRotation = false; m_RotationOverridden = true; }
            var want = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        }

        protected void TurnToYaw(float yaw, float degreesPerSecond = 240f) =>
            TurnTo(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, degreesPerSecond);

        protected void ReleaseRotation()
        {
            if (!m_RotationOverridden) return;
            m_RotationOverridden = false;
            if (Agent != null) Agent.updateRotation = true;
        }

        /// <summary>
        /// Tells the graph „Ziel erreicht“. <paramref name="index"/> is e.g. the waypoint number (1-based). The graph may
        /// switch behaviours right inside this call; returns false then, and the caller must stop touching the agent.
        /// </summary>
        protected bool ReachedGoal(int index = 0)
        {
            int before = m_Activation;
            Senses.RaiseArrived(this, index);
            return before == m_Activation && enabled;
        }

        /// <summary>The object this behaviour is about: its own field, else what the senses watch (the player).</summary>
        protected Transform OrWatched(Transform own) => own != null ? own : Senses.Target;

        /// <summary>Length of the agent's current path (for the Inspector).</summary>
        protected float Remaining => Agent.hasPath && !Agent.pathPending ? Agent.remainingDistance : 0f;

        protected static float Yaw(Quaternion q) => q.eulerAngles.y;

        // ---------------------------------------------------------------------------------- switching
        /// <summary>The component type for each kind.</summary>
        public static Type TypeOf(BehaviourKind kind)
        {
            switch (kind)
            {
                case BehaviourKind.Patrouillieren: return typeof(Patrol);
                case BehaviourKind.Umherstreifen: return typeof(Wander);
                case BehaviourKind.Wachen: return typeof(Guard);
                case BehaviourKind.Folgen: return typeof(Follow);
                case BehaviourKind.Fliehen: return typeof(Flee);
                case BehaviourKind.Zurückkehren: return typeof(ReturnHome);
                case BehaviourKind.Nachsehen: return typeof(Investigate);
                default: return null;
            }
        }

        /// <summary>German name of a kind as shown in menus and messages.</summary>
        public static string NameOf(BehaviourKind kind) => kind == BehaviourKind.Zurückkehren ? "Zurückkehren" : kind.ToString();

        /// <summary>
        /// Switches <paramref name="figure"/> to <paramref name="kind"/>. If the figure lacks that behaviour, it is
        /// added with default values (and <paramref name="note"/> says so). Stehen switches every behaviour off.
        /// Returns the behaviour now running (null for Stehen).
        /// </summary>
        public static NpcBehaviour Switch(GameObject figure, BehaviourKind kind, string label, out string note)
        {
            note = null;
            var all = figure.GetComponents<NpcBehaviour>();
            if (kind == BehaviourKind.Stehen)
            {
                foreach (var b in all) b.enabled = false;
                var senses = figure.GetComponent<Senses>();
                if (senses != null) senses.Current = null;
                return null;
            }
            NpcBehaviour pick = null;
            foreach (var b in all)
                if (b.Kind == kind && (string.IsNullOrEmpty(label) || string.Equals(b.label, label, StringComparison.OrdinalIgnoreCase))) { pick = b; break; }
            if (pick == null)
            {
                pick = (NpcBehaviour)figure.AddComponent(TypeOf(kind));   // steps back in Awake: the current one is still on
                pick.label = label ?? "";
                pick.AddedAtRuntime = true;
                note = $"„{figure.name}“ hatte kein {NameOf(kind)}: mit Standardwerten ergänzt ({MenuPath}{NameOf(kind)} zum Einstellen)";
            }
            pick.enabled = true;
            return pick;
        }

        /// <summary>Points used by the gizmo for the current path.</summary>
        public void GetPath(List<Vector3> into)
        {
            into.Clear();
            if (Agent == null || !Agent.hasPath) return;
            into.Add(transform.position);
            into.AddRange(Agent.path.corners);
        }
    }
}
