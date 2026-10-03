using UnityEngine;

namespace Kit
{
    // Kamera zum Spieler: in der Ich-Perspektive sitzt sie im Kopf, von hinten schwebt sie hinter
    // der Schulter. Stößt sie dabei an eine Wand, rückt sie näher heran, statt durch die Wand zu sehen.
    // Läuft in LateUpdate, also nachdem sich der Spieler in diesem Frame bewegt hat.
    [AddComponentMenu("Kit/Spieler-Kamera")]
    public class KitPlayerCamera : MonoBehaviour
    {
        public KitPlayer player;
        [Tooltip("Abstand hinter dem Kopf (Ansicht von hinten)")] public float distance = 3.5f;
        [Tooltip("Seitlich versetzt, damit der Spieler nicht die Mitte verdeckt")] public float shoulder = 0.5f;
        [Tooltip("Abstand, den die Kamera zu Wänden hält")] public float wallPadding = 0.25f;
        [Tooltip("Diese Renderer werden in der Ich-Perspektive ausgeblendet")] public Renderer[] hideInFirstPerson;

        public bool Blocked { get; private set; }   // für Tests und Anzeige: wurde die Kamera herangeholt?

        void LateUpdate()
        {
            if (!player || !player.head) return;
            var head = player.head;
            head.rotation = Quaternion.Euler(player.Pitch, player.Yaw, 0);
            bool first = player.view == KitPlayer.View.ErstePerson;
            foreach (var r in hideInFirstPerson) if (r) r.enabled = !first;

            if (first) { transform.SetPositionAndRotation(head.position, head.rotation); Blocked = false; return; }

            Vector3 wanted = head.position - head.forward * distance + head.right * shoulder;
            Vector3 dir = wanted - head.position;
            float dist = dir.magnitude;
            // Kugel vom Kopf zur Wunschposition schieben: was trifft sie zuerst?
            Blocked = Physics.SphereCast(head.position, wallPadding, dir / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);
            Vector3 pos = Blocked ? head.position + dir / dist * Mathf.Max(0.1f, hit.distance) : wanted;
            transform.SetPositionAndRotation(pos, head.rotation);
        }
    }

    /// <summary>Fängt den Mauszeiger im Spiel ein. Esc gibt ihn frei, ein Klick fängt ihn wieder.</summary>
    public static class KitCursor
    {
        public static bool Locked => Cursor.lockState == CursorLockMode.Locked;

        public static void Lock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        public static void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Lock(false);
            else if (!Locked && mouse != null && mouse.leftButton.wasPressedThisFrame) Lock(true);
        }
    }
}
