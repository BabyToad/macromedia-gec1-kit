using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Wachen: stands at a post and looks around. When the senses notice something, it turns toward it.
    /// Pushed away, it walks back to its post („Ziel erreicht“ on arrival). What it sees is set in Wahrnehmung.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Wachen")]
    public class Guard : NpcBehaviour
    {
        [Hint("Der Posten: wo die Wache steht und wohin sie schaut (blauer Pfeil).", edges: "Leer: Platz und Blickrichtung beim Start.", label: "Posten")]
        public Transform post;

        [Hint("Wie weit die Wache nach links und rechts schaut, insgesamt.", unit: "°", range: "0 bis 180", edges: "0: starr geradeaus. 360: dreht sich ganz herum.", label: "Umsehen")]
        [Range(0f, 360f)] public float lookAngle = 90f;

        [Hint("Dauer für einmal links, rechts und zurück schauen.", unit: "s", range: "2 bis 10", edges: "Klein: nervös. Groß: gelassen.", label: "Umsehen dauert")]
        [Min(0.5f)] public float lookPeriod = 6f;

        [Hint("Dreht sich zu dem, was die Wahrnehmung bemerkt hat (Spieler oder Geräusch), solange sie sich daran erinnert.", edges: "Aus: schaut stur weiter umher.", label: "Zum Bemerkten drehen")]
        public bool turnToClue = true;

        public Guard() { speed = 2f; }

        public override BehaviourKind Kind => BehaviourKind.Wachen;

        bool m_Returning;
        float m_Phase;

        public Vector3 PostPosition => post != null ? post.position : (FigureSenses != null ? FigureSenses.StartPosition : transform.position);
        public Quaternion PostRotation => post != null ? post.rotation : (FigureSenses != null ? FigureSenses.StartRotation : transform.rotation);

        protected override void Begin()
        {
            m_Phase = 0f;
            m_Returning = Flat(PostPosition - transform.position).magnitude > 0.5f && GoTo(PostPosition);
        }

        protected override void Tick()
        {
            if (m_Returning)
            {
                if (!AtDestination) { Say("geht zum Posten ({0:0.0} m)", Remaining); return; }
                m_Returning = false;
                if (!ReachedGoal()) return;   // the graph switched to something else
            }
            else if (Flat(PostPosition - transform.position).magnitude > 1f)   // pushed off the post
            {
                m_Returning = GoTo(PostPosition);
                return;
            }

            bool fresh = Senses.HasClue && Time.time - Senses.LastClueTime < Mathf.Max(1f, Senses.forgetAfter);
            if (turnToClue && (Senses.Noticed || fresh))
            {
                TurnTo(Senses.LastClue - transform.position, 360f);
                Say("schaut zu dem, was sie bemerkt hat");
                m_Phase = 0f;
                return;
            }
            m_Phase += Time.deltaTime / lookPeriod;
            float yaw = Yaw(PostRotation) + Mathf.Sin(m_Phase * Mathf.PI * 2f) * lookAngle * 0.5f;
            TurnToYaw(yaw, 120f);
            Say(lookAngle > 0f ? "hält Wache, schaut sich um" : "hält Wache");
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
