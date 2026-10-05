using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// The one place the kit changes Time.timeScale. Every user (Anhalten, Pause, …) holds the time with its own
    /// owner token and the scale it wants. Rule: the slowest hold wins. The scale from before the first hold
    /// comes back only when the last owner lets go. Owners release on their own (finished, interaction stopped,
    /// scene unloaded via the interaction's stop); leaving Play, a script reload and the start of the next Play
    /// session (also without domain reload) release everything.
    /// </summary>
    public static class KitTime
    {
        static readonly Dictionary<object, float> s_Holds = new Dictionary<object, float>();
        static float s_Original = 1f;

        public static int HoldCount => s_Holds.Count;
        public static bool IsHeld(object owner) => owner != null && s_Holds.ContainsKey(owner);

        /// <summary>Holds the time at <paramref name="scale"/> (0 = stand still) for this owner; again = new scale.</summary>
        public static void Hold(object owner, float scale)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (s_Holds.Count == 0) s_Original = Time.timeScale;
            s_Holds[owner] = Mathf.Clamp01(scale);
            Apply();
        }

        /// <summary>Lets go of this owner's hold. The last one restores the scale from before the first hold.</summary>
        public static void Release(object owner)
        {
            if (owner == null || !s_Holds.Remove(owner)) return;
            Apply();
        }

        /// <summary>Releases every hold and restores the original scale.</summary>
        public static void ReleaseAll()
        {
            if (s_Holds.Count == 0) return;
            s_Holds.Clear();
            Apply();
        }

        static void Apply()
        {
            if (s_Holds.Count == 0) { Time.timeScale = s_Original; return; }
            float min = 1f;
            foreach (var v in s_Holds.Values) min = Mathf.Min(min, v);
            Time.timeScale = min;
        }

        // New Play session: with domain reload off, holds of the last session would still be here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => ReleaseAll();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorHooks()
        {
            UnityEditor.EditorApplication.playModeStateChanged += s => { if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) ReleaseAll(); };
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;   // statics are about to vanish
        }
#endif
    }
}
