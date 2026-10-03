using System;
using UnityEngine;

namespace Kit
{
    // Spieler: Gehen, Rennen, Springen und Umschauen – in der Ich-Perspektive oder von hinten.
    // Gebaut auf dem CharacterController von Unity: der kümmert sich um Kollisionen,
    // Stufen (Step Offset) und Schrägen (Slope Limit). Dieses Skript rechnet nur aus, wohin es gehen soll.
    //
    // Ablauf pro Frame: Eingabe lesen → Blickrichtung drehen → Tempo berechnen → Schwerkraft/Sprung →
    // CharacterController.Move(). Alles wird mit Time.deltaTime multipliziert, damit es bei 30 und
    // 144 Bildern pro Sekunde gleich schnell ist.
    [AddComponentMenu("Kit/Spieler")]
    [RequireComponent(typeof(CharacterController))]
    public class KitPlayer : MonoBehaviour
    {
        public enum View { ErstePerson, DrittePerson }

        [Header("Ansicht")]
        public View view = View.ErstePerson;
        [Tooltip("Dreh- und Neigepunkt der Kamera (Augenhöhe)")] public Transform head;

        [Header("Bewegung (Meter, Sekunden)")]
        public float walkSpeed = 4f;
        public float runSpeed = 7f;
        public float jumpHeight = 1.2f;
        public float gravity = -20f;
        [Tooltip("Wie schnell das Tempo erreicht wird. Klein = rutschig, groß = direkt")] public float acceleration = 40f;

        [Header("Umschauen")]
        public float mouseSensitivity = 0.12f;      // Grad pro Pixel Mausbewegung
        public float stickSensitivity = 180f;        // Grad pro Sekunde bei voll ausgelenktem Stick
        public bool invertY = false;

        [Header("Zurücksetzen")]
        [Tooltip("Fällt der Spieler tiefer, wird er zurückgesetzt")] public float fallLimit = -20f;

        /// <summary>Wird nach jedem Zurücksetzen aufgerufen (Checkpoint, Herunterfallen).</summary>
        public event Action Respawned;

        /// <summary>Was in einem Frame gedrückt ist. Kommt von Tastatur/Maus/Gamepad oder aus <see cref="scripted"/>.</summary>
        public struct InputFrame
        {
            public Vector2 move, lookMouse, lookStick;
            public bool run, jump, switchView;
        }

        /// <summary>Für Demos und Tests: ersetzt die Geräte-Eingabe, solange gesetzt.</summary>
        [NonSerialized] public InputFrame? scripted;

        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => horizontal + Vector3.up * vertical;
        public float Yaw => yaw;
        public float Pitch => pitch;

        CharacterController body;
        PlayerControls controls;
        Vector3 horizontal;          // Tempo am Boden entlang (m/s)
        float vertical;              // Tempo nach oben (+) oder unten (−)
        float yaw, pitch;            // Blickrichtung in Grad
        float lastGroundedTime = -1f, jumpPressedTime = -1f;
        Vector3 spawnPosition; Quaternion spawnRotation;

        const float CoyoteTime = 0.12f;    // kurz nach der Kante darf noch gesprungen werden
        const float JumpBuffer = 0.12f;    // Sprung kurz vor der Landung wird gemerkt

        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.minMoveDistance = 0f;   // sonst gehen bei hoher Bildrate kleine Schritte verloren
            controls = new PlayerControls();
            yaw = transform.eulerAngles.y;
            SetRespawnPoint(transform.position, transform.rotation);
        }

        void OnEnable() { controls.Enable(); KitCursor.Lock(true); }
        void OnDisable() { controls.Disable(); KitCursor.Lock(false); }

        void Update()
        {
            KitCursor.Update();
            var input = scripted ?? ReadDevices();
            if (scripted.HasValue)   // Springen und Umschalten gelten wie ein Tastendruck nur einen Frame
                scripted = new InputFrame { move = input.move, lookMouse = input.lookMouse, lookStick = input.lookStick, run = input.run };
            if (input.switchView)
                view = view == View.ErstePerson ? View.DrittePerson : View.ErstePerson;

            Look(input);
            Walk(input.move, input.run);
            if (input.jump) jumpPressedTime = Time.time;
            JumpAndFall();

            var flags = body.Move((horizontal + Vector3.up * vertical) * Time.deltaTime);
            IsGrounded = body.isGrounded || GroundBelow();
            if (IsGrounded) lastGroundedTime = Time.time;
            if ((flags & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;   // Kopf an der Decke

            if (transform.position.y < fallLimit) Respawn();
        }

        // Der CharacterController meldet "am Boden" nur, wenn er in diesem Frame nach unten gestoßen ist.
        // Bei sehr kleinen Schritten (hohe Bildrate) klappt das nicht immer: zusätzlich kurz nach unten prüfen.
        bool GroundBelow()
        {
            if (vertical > 0) return false;   // im Sprung nach oben nie "am Boden"
            float r = body.radius * 0.9f;
            Vector3 origin = transform.position + body.center + Vector3.down * (body.height * 0.5f - body.radius);
            return Physics.SphereCast(origin, r, Vector3.down, out _, body.radius - r + body.skinWidth + 0.06f, ~0, QueryTriggerInteraction.Ignore);
        }

        InputFrame ReadDevices() => new InputFrame
        {
            move = controls.Move.ReadValue<Vector2>(),
            // Die Maus dreht nur, solange der Zeiger gefangen ist (sonst dreht jede Bewegung zum Menü mit).
            lookMouse = KitCursor.Locked ? controls.LookMouse.ReadValue<Vector2>() : Vector2.zero,
            lookStick = controls.LookStick.ReadValue<Vector2>(),
            run = controls.Run.IsPressed(),
            jump = controls.Jump.WasPressedThisFrame(),            // nur im Frame des Drückens
            switchView = controls.SwitchView.WasPressedThisFrame(),
        };

        void Look(InputFrame input)
        {
            Vector2 mouse = input.lookMouse * mouseSensitivity;                       // Pixel → Grad
            Vector2 stick = input.lookStick * (stickSensitivity * Time.deltaTime);    // Auslenkung → Grad in diesem Frame
            Vector2 look = mouse + stick;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch + (invertY ? look.y : -look.y), -80f, 80f);
        }

        void Walk(Vector2 input, bool running)
        {
            // Eingabe (x = seitwärts, y = vorwärts) in die Blickrichtung drehen.
            var wish = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            wish = Vector3.ClampMagnitude(wish, 1f) * (running ? runSpeed : walkSpeed);
            float accel = IsGrounded ? acceleration : acceleration * 0.3f;   // in der Luft weniger Kontrolle
            horizontal = Vector3.MoveTowards(horizontal, wish, accel * Time.deltaTime);

            if (view == View.ErstePerson) transform.rotation = Quaternion.Euler(0, yaw, 0);
            else if (wish.sqrMagnitude > 0.01f)                               // von hinten: Körper dreht in Laufrichtung
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish), 720f * Time.deltaTime);
        }

        void JumpAndFall()
        {
            bool canJump = Time.time - lastGroundedTime <= CoyoteTime;
            bool wantsJump = Time.time - jumpPressedTime <= JumpBuffer;
            if (canJump && wantsJump)
            {
                vertical = Mathf.Sqrt(2f * jumpHeight * -gravity);   // Tempo, das genau jumpHeight hoch trägt
                jumpPressedTime = lastGroundedTime = -1f;
            }
            else if (IsGrounded && vertical < 0) vertical = -2f;     // leicht an den Boden drücken (Schrägen hinunter)
            vertical += gravity * Time.deltaTime;
        }

        /// <summary>Setzt den Punkt, zu dem <see cref="Respawn"/> zurückkehrt (z.B. ein Checkpoint).</summary>
        public void SetRespawnPoint(Vector3 position, Quaternion rotation) { spawnPosition = position; spawnRotation = rotation; }
        public void SetRespawnPoint(Transform point) => SetRespawnPoint(point.position, point.rotation);

        /// <summary>Zurück zum Respawn-Punkt, Tempo auf null.</summary>
        public void Respawn()
        {
            Teleport(spawnPosition, spawnRotation);
            Respawned?.Invoke();
        }

        /// <summary>Versetzt den Spieler sofort (ohne durch Wände zu laufen).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            body.enabled = false;                  // sonst setzt der CharacterController die alte Position zurück
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            yaw = rotation.eulerAngles.y; pitch = 0;
            horizontal = Vector3.zero; vertical = 0;
        }

        // Rigidbodies anschieben, gegen die der Spieler läuft.
        // Der CharacterController meldet Berührungen pro Bild (Update), die Physik rechnet in festen
        // Schritten (FixedUpdate). Damit das Schieben bei 30 und bei 200 Bildern pro Sekunde gleich stark ist,
        // merken wir uns nur, WEN wir gerade berühren, und schieben im Physik-Schritt mit fester Kraft.
        [Header("Schieben")]
        [Tooltip("Kraft in Newton, mit der der Spieler Rigidbodies schiebt (schwere bewegen sich weniger)")]
        public float pushForce = 60f;

        readonly System.Collections.Generic.Dictionary<Rigidbody, (Vector3 dir, float time)> touching =
            new System.Collections.Generic.Dictionary<Rigidbody, (Vector3, float)>();

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;   // nicht, worauf man steht
            var dir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z).normalized;
            touching[rb] = (dir, Time.time);       // pro Körper nur ein Eintrag, egal wie oft pro Bild
        }

        void FixedUpdate()
        {
            if (touching.Count == 0) return;
            var gone = new System.Collections.Generic.List<Rigidbody>();
            foreach (var kv in touching)
            {
                // Berührung gilt kurz nach (bei niedriger Bildrate fallen sonst Physik-Schritte aus).
                if (kv.Key == null || Time.time - kv.Value.time > 0.1f) { gone.Add(kv.Key); continue; }
                kv.Key.AddForce(kv.Value.dir * pushForce, ForceMode.Force);   // Kraft pro Physik-Schritt, nicht pro Bild
            }
            foreach (var rb in gone) touching.Remove(rb);
        }

        /// <summary>Die Eingabe-Aktionen (Tastenbelegung), z.B. um sie umzubelegen.</summary>
        public PlayerControls Controls => controls;
    }
}
