using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    /// <summary>
    /// Fliehen: runs away from a threat (the player, unless set) until it is at a safe distance; then
    /// „Ziel erreicht“. It picks reachable spots away from the threat, so it does not run into the next wall.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Fliehen")]
    public class Flee : NpcBehaviour
    {
        [Hint("Wovor die Figur flieht.", edges: "Leer: vor dem, was die Wahrnehmung beobachtet, normalerweise dem Spieler.", label: "Wovor")]
        public Transform from;

        [Hint("Ab diesem Abstand fühlt sich die Figur sicher: sie bleibt stehen, „Ziel erreicht“ feuert. Kommt die Bedrohung näher, flieht sie erneut.", unit: "m", range: "5 bis 20", label: "Sicher ab")]
        [Min(1f)] public float safeDistance = 10f;

        [Hint("In Sicherheit schaut die Figur zur Bedrohung zurück, wie ein Reh, das sich umdreht.", edges: "Aus: dreht ihr den Rücken zu.", label: "Zurückschauen")]
        public bool lookBack = true;

        public Flee() { speed = 5f; }

        public override BehaviourKind Kind => BehaviourKind.Fliehen;

        const float PickInterval = 0.4f;
        bool m_Fleeing;      // running toward a refuge right now
        bool m_Threatened;   // the threat was closer than safe since the last „in Sicherheit“
        float m_NextPick;
        NavMeshPath m_PathBuf;   // made on first use: Unity forbids native objects in component constructors
        NavMeshPath m_Path => m_PathBuf ??= new NavMeshPath();
        readonly Vector3[] m_Corners = new Vector3[32];

        public Transform Threat => OrWatched(from);

        protected override void Begin() { m_Fleeing = false; m_Threatened = false; m_NextPick = 0f; }

        protected override void Tick()
        {
            var t = Threat;
            if (t == null) { Say("nichts, wovor sie fliehen könnte"); Halt(); return; }
            float d = Vector3.Distance(Flat(transform.position), Flat(t.position));
            if (d >= safeDistance)
            {
                if (m_Fleeing) { m_Fleeing = false; Halt(); }
                if (m_Threatened)
                {
                    m_Threatened = false;
                    Say("in Sicherheit ({0:0.0} m)", d);
                    if (!ReachedGoal()) return;   // the graph switched to something else
                }
                if (lookBack) TurnTo(t.position - transform.position);
                Say("in Sicherheit ({0:0.0} m)", d);
                return;
            }
            m_Threatened = true;
            // Pick a refuge at most every PickInterval, also after a failed search (a cornered figure must not
            // search every frame); sooner only if it arrived at the last refuge.
            if (Time.time >= m_NextPick || (m_Fleeing && AtDestination))
            {
                m_NextPick = Time.time + PickInterval;
                // A failed new search keeps an escape that is still under way; only a figure without a path is cornered.
                if (PickRefuge(t.position, d, out var p)) m_Fleeing = GoTo(p) || m_Fleeing;
                else if (!Agent.hasPath || AtDestination) m_Fleeing = false;
            }
            if (m_Fleeing) Say("flieht vor {2} ({0:0.0} m)", d, 0f, t);
            else Say("sitzt in der Ecke ({0:0.0} m)", d);
        }

        /// <summary>
        /// Looks in 16 directions and picks the reachable spot that ends farthest from the threat, preferring
        /// paths that start away from it and short detours. Corners score low because their exits lead back.
        /// </summary>
        bool PickRefuge(Vector3 threat, float d, out Vector3 best)
        {
            best = default;
            float bestScore = float.NegativeInfinity;
            var away = Flat(transform.position - threat);
            away = away.sqrMagnitude < 0.01f ? Flat(transform.forward).normalized : away.normalized;
            float step = Mathf.Max(3f, safeDistance - d + 2f);
            for (int i = 0; i < 16; i++)
            {
                var dir = Quaternion.Euler(0f, i * 22.5f, 0f) * away;
                var p = transform.position + dir * step;
                if (!NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                if (!Agent.CalculatePath(hit.position, m_Path) || m_Path.status != NavMeshPathStatus.PathComplete) continue;
                int n = m_Path.GetCornersNonAlloc(m_Corners);
                if (n < 2) continue;
                float len = 0f;
                for (int k = 1; k < n; k++) len += Vector3.Distance(m_Corners[k - 1], m_Corners[k]);
                // The first corner shows which way the figure actually starts running.
                var first = Flat(m_Corners[1] - transform.position).normalized;
                float score = Vector3.Distance(Flat(hit.position), Flat(threat))
                            + 2f * Vector3.Dot(first, away)
                            - 0.3f * Mathf.Max(0f, len - step);
                if (score > bestScore) { bestScore = score; best = hit.position; }
            }
            return bestScore > float.NegativeInfinity;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
