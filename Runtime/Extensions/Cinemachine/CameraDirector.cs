using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Who decides which camera shows the game. Cinemachine's rule stays as it is (the CinemachineCamera with the
    /// highest Priority is live, the Brain on the main camera blends to it); the director only hands out priorities.
    /// Every switch is a claim (owner + camera) in one ordered list; after each change the priorities are computed
    /// again from that list: the newest claim wins, taking a claim back lets the one before it win again, also when
    /// the same camera is claimed twice. Zones, nodes and the Kamera components all go through here.
    /// </summary>
    public static class CameraDirector
    {
        /// <summary>Priority of the player's own view (Spieler-Sicht). Kamera components below this wait in the wings.</summary>
        public const int PlayerPriority = 10;
        const int k_Live = 1000;

        static readonly List<(object owner, CinemachineCamera cam)> s_Claims = new List<(object, CinemachineCamera)>();
        static readonly Dictionary<CinemachineCamera, int> s_Base = new Dictionary<CinemachineCamera, int>();
        static readonly Dictionary<CinemachineCamera, int> s_Wanted = new Dictionary<CinemachineCamera, int>();
        static readonly List<CinemachineCamera> s_Free = new List<CinemachineCamera>();
        static CinemachineCamera s_Winner;

        /// <summary>Switch to <paramref name="cam"/> on behalf of <paramref name="owner"/>, with a camera move of the given time.</summary>
        public static void Switch(CinemachineCamera cam, object owner, float seconds, Ease ease)
        {
            if (!cam) return;
            PlayerCameraBridge.Ensure();
            s_Claims.RemoveAll(c => Equals(c.owner, owner) && c.cam == cam);
            s_Claims.Add((owner, cam));
            Recompute(seconds, ease);
        }

        /// <summary>Takes back what <paramref name="owner"/> switched to (only <paramref name="only"/>, if given).</summary>
        public static void Release(object owner, float seconds, Ease ease, CinemachineCamera only = null)
        {
            if (s_Claims.RemoveAll(c => Equals(c.owner, owner) && (!only || c.cam == only)) > 0) Recompute(seconds, ease);
        }

        /// <summary>Is <paramref name="cam"/> switched to by anyone right now?</summary>
        public static bool IsSwitchedTo(CinemachineCamera cam) => s_Claims.Exists(c => c.cam == cam);

        /// <summary>The camera the newest claim shows, or null (then the player's view, or whatever has the highest own priority).</summary>
        public static CinemachineCamera Winner => s_Winner;

        // A Brain that has not shown anything yet (created this frame: an older scene, a Start → Kamera wechseln in the
        // very first frame, a zone the player stands in at load) would cut to the new camera. Give it one update with
        // the Spieler-Sicht first; inside a physics callback (zones) Cinemachine forbids that, so wait for it instead.
        static bool s_Waiting;
        static CinemachineCamera s_PendingWinner;
        static float s_PendingSeconds;
        static Ease s_PendingEase;

        static CinemachineCamera NewestClaim => s_Claims.Count > 0 ? s_Claims[s_Claims.Count - 1].cam : null;

        static void Recompute(float seconds, Ease ease)
        {
            var b = Brain();
            if (b && b.isActiveAndEnabled && b.ActiveVirtualCamera == null)
            {
                if (!Time.inFixedTimeStep)
                {
                    var method = b.UpdateMethod;
                    b.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;   // Cinemachine erlaubt den Einzelschritt nur so
                    b.ManualUpdate();
                    b.UpdateMethod = method;
                }
                else
                {
                    // Wait for the Brain's first update. The move that will run is the one to the winner then: keep the
                    // time and easing of the change that made the current winner.
                    if (!s_Waiting || NewestClaim != s_PendingWinner) { s_PendingWinner = NewestClaim; s_PendingSeconds = seconds; s_PendingEase = ease; }
                    if (!s_Waiting) { s_Waiting = true; BlendRunner.Run(AfterFirstUpdate()); }
                    return;
                }
            }
            Apply(seconds, ease);
        }

        static System.Collections.IEnumerator AfterFirstUpdate()
        {
            for (int i = 0; i < 5 && Brain() && Brain().ActiveVirtualCamera == null; i++) yield return null;
            s_Waiting = false;
            Apply(s_PendingSeconds, s_PendingEase);
        }

        static void Apply(float seconds, Ease ease)
        {
            // Owners or cameras that no longer exist cannot hold the picture.
            s_Claims.RemoveAll(c => !c.cam || c.owner is Object o && !o);

            s_Wanted.Clear();
            for (int i = 0; i < s_Claims.Count; i++) s_Wanted[s_Claims[i].cam] = k_Live + 1 + i;   // a camera ranks by its newest claim

            s_Free.Clear();
            foreach (var kv in s_Base) if (!s_Wanted.ContainsKey(kv.Key)) s_Free.Add(kv.Key);
            foreach (var cam in s_Free) { if (cam) { cam.Priority = s_Base[cam]; cam.Prioritize(); } s_Base.Remove(cam); }
            foreach (var kv in s_Wanted)
            {
                if (!s_Base.ContainsKey(kv.Key)) s_Base[kv.Key] = kv.Key.Priority.Value;
                if (kv.Key.Priority.Value == kv.Value) continue;
                kv.Key.Priority = kv.Value;
                kv.Key.Prioritize();                                         // Cinemachine sortiert seine Kameras neu
            }

            var brain = Brain();
            if (brain) brain.IgnoreTimeScale = true;                         // Kamerafahrten laufen in echter Zeit, auch in Pause und Zeitlupe
            var winner = s_Claims.Count > 0 ? s_Claims[s_Claims.Count - 1].cam : null;
            if (winner == s_Winner) return;                                  // the picture does not change: leave the running move alone
            s_Winner = winner;
            if (brain) brain.DefaultBlend = Definition(seconds, ease);       // the move to the new winner uses this change's time
        }

        public static CinemachineBlendDefinition Definition(float seconds, Ease ease)
        {
            if (ease == Ease.Instant || seconds <= 0f) return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var style = ease switch
            {
                Ease.Linear => CinemachineBlendDefinition.Styles.Linear,
                Ease.In => CinemachineBlendDefinition.Styles.EaseIn,
                Ease.Out => CinemachineBlendDefinition.Styles.EaseOut,
                Ease.Overshoot => CinemachineBlendDefinition.Styles.EaseOut,   // Kamerafahrten federn nicht: das macht seekrank
                _ => CinemachineBlendDefinition.Styles.EaseInOut,
            };
            return new CinemachineBlendDefinition(style, seconds);
        }

        public static CinemachineBrain Brain()
        {
            var cam = Camera.main;
            return cam ? cam.GetComponent<CinemachineBrain>() : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() { s_Claims.Clear(); s_Base.Clear(); s_Winner = null; s_Waiting = false; s_PendingWinner = null; }
    }
}
