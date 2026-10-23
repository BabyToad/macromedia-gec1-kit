using UnityEngine;

namespace Kit
{
    /// <summary>Zurückkehren: walks back home and turns the way it faced there. „Ziel erreicht“ on arrival.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Zurückkehren")]
    public class ReturnHome : NpcBehaviour
    {
        [Hint("Wohin die Figur zurückkehrt, mit Platz und Blickrichtung.", edges: "Leer: wo sie beim Start stand.", label: "Zuhause")]
        public Transform home;

        public ReturnHome() { speed = 2.5f; }

        public override BehaviourKind Kind => BehaviourKind.Zurückkehren;

        bool m_Walking, m_Reported;

        public Vector3 HomePosition => home != null ? home.position : (FigureSenses != null ? FigureSenses.StartPosition : transform.position);
        public Quaternion HomeRotation => home != null ? home.rotation : (FigureSenses != null ? FigureSenses.StartRotation : transform.rotation);

        /// <summary>True once the figure stands at home.</summary>
        public bool IsHome { get; private set; }

        protected override void Begin()
        {
            IsHome = m_Reported = false;
            m_Walking = GoTo(HomePosition);
            if (!m_Walking) Say("Zuhause ist nicht erreichbar");
        }

        protected override void Tick()
        {
            if (!IsHome)
            {
                // Not reachable at the start (a closed door)? Keep trying: the NavMesh follows the level.
                if (!m_Walking && !(m_Walking = GoTo(HomePosition))) { Say("Zuhause ist nicht erreichbar"); return; }
                if (!AtDestination)
                {
                    if (PathBlocked) Say("Weg nach Hause versperrt: wartet");
                    else Say("kehrt zurück ({0:0.0} m)", Remaining);
                    return;
                }
                m_Walking = false;
                IsHome = true;
            }
            Say("ist zurück");
            if (!m_Reported)
            {
                m_Reported = true;
                if (!ReachedGoal()) return;   // the graph switched to something else
            }
            TurnTo(HomeRotation * Vector3.forward);
        }
    }
}
