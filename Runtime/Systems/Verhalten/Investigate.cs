using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Nachsehen: walks to the last thing the senses noticed (where the player was last seen or heard, or a
    /// Geräusch) and looks around there. Then „Ziel erreicht“. A newer clue on the way redirects it.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Nachsehen")]
    public class Investigate : NpcBehaviour
    {
        [Hint("Wie lange sich die Figur am Ort umsieht, bevor „Ziel erreicht“ feuert.", unit: "s", range: "1 bis 6", edges: "0: kehrt sofort um.", label: "Umsehen dauert")]
        [Min(0f)] public float lookTime = 3f;

        [Hint("Wie weit sie sich beim Umsehen dreht, insgesamt.", unit: "°", range: "60 bis 180", label: "Umsehen")]
        [Range(0f, 360f)] public float lookAngle = 140f;

        public Investigate() { speed = 3f; }

        public override BehaviourKind Kind => BehaviourKind.Nachsehen;

        enum Phase { Gehen, Umsehen, Fertig }
        Phase m_Phase;
        float m_ClueTime, m_LookStart, m_BaseYaw;

        protected override void Begin()
        {
            m_Phase = Phase.Gehen;
            Head();
        }

        void Head()
        {
            m_ClueTime = Senses.LastClueTime;
            if (!Senses.HasClue || !GoTo(Senses.LastClue)) StartLooking();
        }

        void StartLooking()
        {
            m_Phase = Phase.Umsehen;
            m_LookStart = Time.time;
            m_BaseYaw = Yaw(transform.rotation);
            Halt();
        }

        protected override void Tick()
        {
            // Something newer was noticed: go there instead.
            if (m_Phase != Phase.Fertig && Senses.HasClue && Senses.LastClueTime > m_ClueTime + 0.01f && !Senses.CanSee)
            {
                m_Phase = Phase.Gehen;
                Head();
            }
            switch (m_Phase)
            {
                case Phase.Gehen:
                    if (!AtDestination)
                    {
                        if (PathBlocked) Say("kommt nicht zur Spur: sieht sich hier um");
                        else Say("sieht nach ({0:0.0} m)", Remaining);
                        if (PathBlocked) StartLooking();
                        return;
                    }
                    StartLooking();
                    return;
                case Phase.Umsehen:
                    float t = (Time.time - m_LookStart) / Mathf.Max(0.01f, lookTime);
                    TurnToYaw(m_BaseYaw + Mathf.Sin(t * Mathf.PI * 2f) * lookAngle * 0.5f, 200f);
                    if (Senses.HasClue) Say("sieht sich um ({0:0.0} s)", Mathf.Max(0f, lookTime - (Time.time - m_LookStart)));
                    else Say("keine Spur: sieht sich um");
                    if (t < 1f) return;
                    m_Phase = Phase.Fertig;
                    Say("hat nachgesehen");
                    ReachedGoal();
                    return;
                default:
                    Say("hat nachgesehen");
                    return;
            }
        }
    }
}
