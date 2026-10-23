using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Brings the kit's Spieler camera into Cinemachine, so Kamera components and zones can take over the picture
    /// and hand it back with a camera move. The main camera gets a CinemachineBrain (real time, so moves run in
    /// pause and Zeitlupe too); the player's view moves to its own CinemachineCamera "Spieler-Sicht" beside it,
    /// still placed by KitPlayerCamera (first person, from behind, wall avoidance, shake: unchanged). The Brain runs
    /// after it (execution order 100), so without other cameras the picture is exactly what it was.
    /// The editor does this visibly when a Kamera component is added; at runtime it is the fallback for scenes
    /// that were never set up (a player spawned while playing, an older scene).
    /// </summary>
    public static class PlayerCameraBridge
    {
        public const string ViewName = "Spieler-Sicht";

        /// <summary>Sets up Brain + Spieler-Sicht if the main camera is still driven directly. Returns the player's view, if any.</summary>
        public static CinemachineCamera Ensure()
        {
            var main = Camera.main;
            if (!main) return null;
            var brain = main.GetComponent<CinemachineBrain>();
            if (!brain) brain = AddBrain(main.gameObject);
            brain.IgnoreTimeScale = true;
            var direct = main.GetComponent<KitPlayerCamera>();
            if (!direct || !direct.enabled) return Application.isPlaying ? FindView() : FindViewReadOnly();   // already moved (also earlier in this frame: the old one is off, destroyed at frame end)
            var view = CreateView(main, direct);
            direct.enabled = false;                                // from now on only the Spieler-Sicht places the camera
            if (Application.isPlaying) Object.Destroy(direct); else Object.DestroyImmediate(direct);
            return view;
        }

        /// <summary>The Spieler-Sicht in the scene, if there is one.</summary>
        public static CinemachineCamera FindView()
        {
            foreach (var kpc in Object.FindObjectsByType<KitPlayerCamera>(FindObjectsSortMode.None))
                if (kpc.enabled && kpc.TryGetComponent(out CinemachineCamera v))
                {
                    if (!v.GetComponent<SpielerSichtBody>()) v.gameObject.AddComponent<SpielerSichtBody>();   // eine Spieler-Sicht von vor dieser Fassung
                    return v;
                }
            return null;
        }

        static CinemachineCamera FindViewReadOnly()
        {
            foreach (var kpc in Object.FindObjectsByType<KitPlayerCamera>(FindObjectsSortMode.None))
                if (kpc.enabled && kpc.TryGetComponent(out CinemachineCamera v)) return v;
            return null;
        }

        public static CinemachineBrain AddBrain(GameObject cameraObject)
        {
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            Configure(brain);
            return brain;
        }

        /// <summary>The kit's Brain settings: one-second ease, real time.</summary>
        public static void Configure(CinemachineBrain brain)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1f);
            brain.IgnoreTimeScale = true;
        }

        /// <summary>Creates "Spieler-Sicht" beside the camera with a copy of its KitPlayerCamera settings. Leaves the original alone.</summary>
        public static CinemachineCamera CreateView(Camera main, KitPlayerCamera from)
        {
            var go = new GameObject(ViewName);
            go.transform.SetParent(main.transform.parent, false);
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = CameraDirector.PlayerPriority;
            vcam.Lens = LensSettings.FromCamera(main);
            go.AddComponent<CinemachineImpulseListener>();            // „Kamera wackeln (Cinemachine)“ wirkt auch hier
            var follow = go.AddComponent<KitPlayerCamera>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), follow);   // Abstand, Schulter, Spieler … wie vorher
            go.AddComponent<SpielerSichtBody>();                      // in fremden Einstellungen ist der Körper zu sehen
            return vcam;
        }
    }
}
