using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Folgen: stays close to a target (the player, unless set). „Ziel erreicht“ fires each time the figure
    /// catches up. A dog at the heel, a chaser, a duckling behind another figure.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Folgen")]
    public class Follow : NpcBehaviour
    {
        [Hint("Wem die Figur folgt.", edges: "Leer: dem, was die Wahrnehmung beobachtet, normalerweise dem Spieler.", label: "Ziel")]
        public Transform target;

        [Hint("Abstand, den die Figur hält; hier bleibt sie stehen.", unit: "m", range: "0,5 bis 4", edges: "Klein: dicht dran. Groß: mit Respekt.", label: "Abstand halten")]
        [Min(0f)] public float keepDistance = 1.5f;

        [Hint("Erst wenn das Ziel um so viel weiter weg ist, läuft die Figur wieder los. Verhindert Zappeln.", unit: "m", range: "0,5 bis 3", edges: "0: läuft bei jedem Schritt mit. Groß: gemächlicher Begleiter.", label: "Losgehen ab")]
        [Min(0f)] public float slack = 1f;

        [Hint("Steht die Figur, schaut sie das Ziel an.", edges: "Aus: schaut, wohin sie zuletzt ging.", label: "Ziel anschauen")]
        public bool faceTarget = true;

        public Follow() { speed = 3.5f; }

        public override BehaviourKind Kind => BehaviourKind.Folgen;

        bool m_Chasing;
        float m_NextRepath;

        public Transform Target => OrWatched(target);

        protected override void Begin() { m_Chasing = false; m_NextRepath = 0f; }

        protected override void Tick()
        {
            var t = Target;
            if (t == null) { Say("kein Ziel zum Folgen"); Halt(); return; }
            float d = Vector3.Distance(Flat(transform.position), Flat(t.position));
            if (!m_Chasing && d > keepDistance + slack) m_Chasing = true;
            if (m_Chasing)
            {
                // Close enough, or at the spot beside the target and within the slack (the agent stops a bit early).
                if (d <= keepDistance + 0.1f || (AtDestination && d <= keepDistance + slack))
                {
                    m_Chasing = false;
                    Halt();
                    Say("bei {2} ({0:0.0} m)", d, 0f, t);
                    ReachedGoal();
                    return;
                }
                if (Time.time >= m_NextRepath)
                {
                    m_NextRepath = Time.time + 0.2f;
                    // Aim at the point keepDistance short of the target, so the figure stops beside it, not inside.
                    var to = Flat(t.position - transform.position);
                    GoTo(t.position - to.normalized * keepDistance);
                }
                Say("folgt {2} ({0:0.0} m)", d, 0f, t);
                return;
            }
            if (faceTarget) TurnTo(t.position - transform.position);
            Say("bei {2} ({0:0.0} m)", d, 0f, t);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
