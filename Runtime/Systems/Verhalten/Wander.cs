using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    /// <summary>Umherstreifen: wanders to random reachable points around a centre, pausing in between.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Umherstreifen")]
    public class Wander : NpcBehaviour
    {
        [Hint("Um diesen Punkt herum streift die Figur.", edges: "Leer: um den Platz, an dem sie beim Start stand.", label: "Mitte")]
        public Transform centre;

        [Hint("Wie weit sich die Figur von der Mitte entfernt.", unit: "m", range: "2 bis 20", edges: "Klein: ein Huhn im Gehege. Groß: ein Wanderer.", label: "Umkreis")]
        [Min(0.5f)] public float radius = 6f;

        [Hint("Kürzeste Pause zwischen zwei Wegen.", unit: "s", range: "0 bis 5", label: "Pause mindestens")]
        [Min(0f)] public float pauseMin = 1f;

        [Hint("Längste Pause zwischen zwei Wegen.", unit: "s", range: "0 bis 10", edges: "Gleich wie die kürzeste: immer gleich lange Pausen.", label: "Pause höchstens")]
        [Min(0f)] public float pauseMax = 3f;

        public Wander() { speed = 1.5f; }

        public override BehaviourKind Kind => BehaviourKind.Umherstreifen;

        bool m_Walking;
        float m_PauseUntil;
        NavMeshPath m_PathBuf;   // made on first use: Unity forbids native objects in component constructors
        NavMeshPath m_Path => m_PathBuf ??= new NavMeshPath();

        /// <summary>The centre in world space.</summary>
        public Vector3 Centre => centre != null ? centre.position : (FigureSenses != null ? FigureSenses.StartPosition : transform.position);

        protected override void Begin() { m_Walking = false; m_PauseUntil = 0f; }

        protected override void Tick()
        {
            if (m_Walking)
            {
                if (!AtDestination) { Say("streift umher ({0:0.0} m)", Remaining); return; }
                m_Walking = false;
                if (!ReachedGoal()) return;   // the graph switched to something else
                m_PauseUntil = Time.time + Random.Range(pauseMin, Mathf.Max(pauseMin, pauseMax));
            }
            float left = m_PauseUntil - Time.time;
            if (left > 0f) { Say("hält inne ({0:0.0} s)", left); return; }
            if (PickPoint(out var p)) { m_Walking = GoTo(p); Say("streift umher"); }
            else { Say("findet keinen Weg im Umkreis"); m_PauseUntil = Time.time + 1f; }
        }

        bool PickPoint(out Vector3 point)
        {
            for (int i = 0; i < 12; i++)
            {
                var r = Random.insideUnitCircle * radius;
                var p = Centre + new Vector3(r.x, 0f, r.y);
                if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) continue;
                if ((hit.position - transform.position).sqrMagnitude < 1f) continue;   // a real walk, not a shuffle
                if (!Agent.CalculatePath(hit.position, m_Path) || m_Path.status != NavMeshPathStatus.PathComplete) continue;
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }
    }
}
