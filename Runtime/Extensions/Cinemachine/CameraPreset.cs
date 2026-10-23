using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// A Kamera preset: a CinemachineCamera set up for one classic shot, ready the moment it is added. The preset
    /// only does the setting-up and the "when" (from the start, or when a zone or node switches to it); everything
    /// about the shot itself stays on the Cinemachine components below it, where any Unity project tunes it.
    /// One class per file (FollowCamera.cs …), so Unity finds each preset's script for Add Component and saving.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public abstract class CameraPreset : MonoBehaviour
    {
        [Hint("An: diese Kamera zeigt das Spiel von Anfang an (statt der Spieler-Kamera). Aus: sie wartet, bis eine Kamera-Zone oder der Knoten „Kamera wechseln“ zu ihr schaltet.", label: "Von Anfang an")]
        public bool liveAtStart = false;

        [Hint("So lange dauert die Kamerafahrt, wenn zu dieser Kamera gewechselt wird (echte Zeit).", unit: "s", range: "0,5 bis 3", edges: "0: harter Schnitt.", label: "Kamerafahrt")]
        public float seconds = 1.2f;

        [Hint("Wie die Kamerafahrt verläuft.", edges: "„Weich“ wirkt wie eine Kranfahrt, „Sofort“ ist ein Schnitt.", label: "Verlauf")]
        public Ease ease = Ease.Smooth;

        public CinemachineCamera Camera => GetComponent<CinemachineCamera>();

        /// <summary>What a student sees as the preset's name, e.g. "Verfolgen".</summary>
        public abstract string Kind { get; }

        /// <summary>Adds and tunes the Cinemachine components for this shot. Leaves components that are already there.</summary>
        public abstract void Configure();

        protected virtual void Reset()
        {
            var cam = Camera;
            cam.Priority = 0;                                        // unter der Spieler-Sicht (10): wartet, bis sie dran ist
            if (!cam.Target.TrackingTarget)
            {
                var player = GameObject.FindWithTag("Player");
                if (player) cam.Target.TrackingTarget = Head(player.transform);
            }
            Configure();
        }

        // Die Kamera schaut auf den Kopf, nicht auf die Füße (Pivot) des Spielers.
        static Transform Head(Transform player)
        {
            var kp = player.GetComponent<KitPlayer>();
            return kp && kp.head ? kp.head : player;
        }

        protected T Ensure<T>() where T : Component => GetComponent<T>() ? GetComponent<T>() : gameObject.AddComponent<T>();

        // „Von Anfang an“ greift schon beim Einschalten, vor jedem Start(): so kommt es immer vor Zonen, in denen der
        // Spieler beim Laden schon steht (die melden sich erst in Start), und die Zone gewinnt, unabhängig von der
        // Reihenfolge. Wieder eingeschaltet gilt es wieder.
        protected virtual void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (liveAtStart) Show(0f, Ease.Instant);
            else PlayerCameraBridge.Ensure();                     // bereit, bevor jemand zu dieser Kamera wechselt
        }

        // Ausgeschaltet oder entfernt: die Kamera gibt ihr Bild ab, sie kann es nicht behalten.
        protected virtual void OnDisable() { if (Application.isPlaying) Hide(seconds, ease); }

        /// <summary>Makes this camera the one that shows the game.</summary>
        public void Show(float seconds, Ease ease) => CameraDirector.Switch(Camera, this, seconds, ease);
        public void Show() => Show(seconds, ease);
        /// <summary>Hands the picture back to whatever showed it before.</summary>
        public void Hide(float seconds, Ease ease) => CameraDirector.Release(this, seconds, ease);

        void OnValidate() => seconds = Mathf.Max(0f, seconds);
    }
}
