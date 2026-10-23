using UnityEngine;

namespace Kit
{
    // Kamera zum Spieler: in der Ich-Perspektive sitzt sie im Kopf, von hinten schwebt sie hinter
    // der Schulter. Stößt sie dabei an eine Wand, rückt sie näher heran, statt durch die Wand zu sehen.
    // Läuft in LateUpdate, also nachdem sich der Spieler in diesem Frame bewegt hat.
    [AddComponentMenu(KitBrand.ComponentMenu + "Spieler-Kamera")]
    public class KitPlayerCamera : MonoBehaviour, IKitViewer
    {
        [Hint("Der Spieler, dem die Kamera folgt. Richtet das Prefab ein.", label: "Spieler")]
        public KitPlayer player;
        /// <summary>For nodes that work "from the player" (IKitViewer).</summary>
        public Transform Body => player ? player.transform : null;
        [Hint("Wie weit die Kamera in der Ansicht von hinten hinter dem Kopf schwebt.", unit: "m", range: "2 bis 6", edges: "Steht eine Wand im Weg, rückt sie näher heran.", label: "Abstand")] public float distance = 3.5f;
        [Hint("Wie weit die Kamera seitlich versetzt ist, damit der Spieler nicht die Bildmitte verdeckt.", unit: "m", range: "0 bis 1", edges: "0: genau dahinter. Negativ: über der linken Schulter.", label: "Schulterversatz")] public float shoulder = 0.5f;
        [Hint("Wie viel Abstand die Kamera zu Wänden hält, damit sie nicht hineinschaut.", unit: "m", range: "0,1 bis 0,5", label: "Wandabstand")] public float wallPadding = 0.25f;
        [Hint("Diese Renderer werden in der Ich-Perspektive ausgeblendet, z.B. der eigene Körper.", label: "In Ich-Perspektive ausblenden")] public Renderer[] hideInFirstPerson;

        public bool Blocked { get; private set; }   // für Tests und Anzeige: wurde die Kamera herangeholt?

        // Wackeln: ein Versatz, der nach dem Platzieren dazukommt und bis zum Ende ausklingt. Läuft in echter Zeit,
        // also auch während eines Anhaltens (Time.timeScale = 0).
        float m_ShakeStrength, m_ShakeStart, m_ShakeEnd;
        public bool Shaking => Time.unscaledTime < m_ShakeEnd;
        public Vector3 ShakeOffset { get; private set; }

        /// <summary>Lässt die Kamera wackeln: Stärke in Metern, Dauer in Sekunden (echte Zeit).</summary>
        public void Shake(float strength, float seconds)
        {
            float now = Time.unscaledTime;
            if (Shaking) strength = Mathf.Max(strength, m_ShakeStrength * Remaining(now));   // ein laufendes Wackeln nicht abschwächen
            m_ShakeStrength = Mathf.Max(0f, strength);
            m_ShakeStart = now; m_ShakeEnd = now + Mathf.Max(0.01f, seconds);
        }

        float Remaining(float now) => Mathf.Clamp01((m_ShakeEnd - now) / Mathf.Max(0.0001f, m_ShakeEnd - m_ShakeStart));

        void LateUpdate()
        {
            if (!player || !player.head) return;
            var head = player.head;
            head.rotation = Quaternion.Euler(player.Pitch, player.Yaw, 0);
            bool first = player.view == KitPlayer.View.ErstePerson;
            if (hideInFirstPerson != null) foreach (var r in hideInFirstPerson) if (r) r.enabled = !first;   // per Skript angelegt: noch leer

            Vector3 pos;
            if (first) { pos = head.position; Blocked = false; }
            else
            {
                Vector3 wanted = head.position - head.forward * distance + head.right * shoulder;
                Vector3 dir = wanted - head.position;
                float dist = dir.magnitude;
                // Kugel vom Kopf zur Wunschposition schieben: was trifft sie zuerst?
                Blocked = Physics.SphereCast(head.position, wallPadding, dir / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);
                pos = Blocked ? head.position + dir / dist * Mathf.Max(0.1f, hit.distance) : wanted;
            }
            ShakeOffset = Vector3.zero;
            if (Shaking)
            {
                float t = Time.unscaledTime * 30f, k = m_ShakeStrength * Remaining(Time.unscaledTime);
                ShakeOffset = head.rotation * new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * k);
            }
            transform.SetPositionAndRotation(pos + ShakeOffset, head.rotation);
        }
    }

    /// <summary>Fängt den Mauszeiger im Spiel ein. Esc gibt ihn frei, ein Klick fängt ihn wieder.</summary>
    public static class KitCursor
    {
        public static bool Locked => Cursor.lockState == CursorLockMode.Locked;

        // Owners that need the cursor free (a visible menu screen). While any holds, nothing locks it, not even a click.
        static readonly System.Collections.Generic.HashSet<object> s_FreeHolds = new System.Collections.Generic.HashSet<object>();

        /// <summary>True while some owner needs the cursor free.</summary>
        public static bool HeldFree => s_FreeHolds.Count > 0;

        /// <summary>Keeps the cursor free for this owner (e.g. a visible menu), until <see cref="ReleaseFree"/>.</summary>
        public static void HoldFree(object owner) { if (owner != null && s_FreeHolds.Add(owner)) Lock(false); }

        /// <summary>Lets go of this owner's hold. The cursor stays free until something locks it again.</summary>
        public static void ReleaseFree(object owner) { if (owner != null) s_FreeHolds.Remove(owner); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => s_FreeHolds.Clear();

        public static void Lock(bool on)
        {
            if (on && HeldFree) on = false;                                   // a visible menu keeps it free
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        public static void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Lock(false);
            else if (!Locked && !HeldFree && mouse != null && mouse.leftButton.wasPressedThisFrame) Lock(true);
        }
    }
}
