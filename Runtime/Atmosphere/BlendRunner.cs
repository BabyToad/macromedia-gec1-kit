using UnityEngine;

namespace Kit
{
    /// <summary>Runs blends for zones, which may be switched off mid-blend. Created on first use, survives scene loads.</summary>
    [AddComponentMenu("")]
    public sealed class BlendRunner : MonoBehaviour
    {
        static BlendRunner s_Instance;
        static bool s_Ending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            s_Ending = false;
            Application.quitting -= Ending; Application.quitting += Ending;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= Changed; UnityEditor.EditorApplication.playModeStateChanged += Changed;
#endif
        }

        static void Ending() => s_Ending = true;

        /// <summary>True when blends cannot run over time (Edit Mode, or Play/the app is ending): they jump to their end.</summary>
        public static bool FinishNow => !Application.isPlaying || s_Ending;
#if UNITY_EDITOR
        static void Changed(UnityEditor.PlayModeStateChange c) { if (c == UnityEditor.PlayModeStateChange.ExitingPlayMode) s_Ending = true; }
#endif

        public static Coroutine Run(System.Collections.IEnumerator routine)
        {
            // Edit Mode, or Play is ending (zones switch off while everything closes): finish at once, create nothing.
            if (FinishNow)
            {
                // Run to the end at once. Blend.Run ends in one step when FinishNow; anything else gets a hard limit,
                // because no frame (and so no time) will pass while we loop here.
                for (int i = 0; i < 10000 && routine.MoveNext(); i++) { }
                return null;
            }
            if (!s_Instance)
            {
                var go = new GameObject("Atmosphäre (Kit)");
                DontDestroyOnLoad(go);
                s_Instance = go.AddComponent<BlendRunner>();
            }
            return s_Instance.StartCoroutine(routine);
        }
    }
}
