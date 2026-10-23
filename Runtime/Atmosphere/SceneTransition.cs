using System.Collections;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Scene changes (Szene laden, Neustart, Nächste Szene) cover and reveal the screen with the Blende, so a Blende
    /// node and a scene change are one and the same curtain: no double fade, no flash in between. Without a
    /// Szenenübergang in the scene this is a short black fade.
    /// </summary>
    public sealed class ScreenFadeTransition : IKitSceneTransition
    {
        public Color color = Color.black;
        public float seconds = 0.4f;
        public Ease ease = Ease.Smooth;

        // The curtain must really arrive: a Blende node may replace the fade midway. Then the scene change takes the
        // screen back (a few times); if something keeps fighting, the change is reported as failed, so KitScenes
        // cleans up (Abort) instead of loading behind a half-open curtain or leaving the game covered.
        const int k_Tries = 3;

        public IEnumerator Cover() => Until(() => ScreenFade.Out(color, seconds, ease), "Zudecken");
        public IEnumerator Reveal() => Until(() => ScreenFade.In(seconds, ease), "Aufdecken");

        static IEnumerator Until(System.Func<FadeHandle> fade, string what)
        {
            for (int i = 0; i < k_Tries; i++)
            {
                var h = fade();
                yield return h;
                if (!h.Interrupted) yield break;
            }
            throw new System.InvalidOperationException($"Szenenwechsel: {what} wurde {k_Tries}-mal von einer anderen Blende unterbrochen.");
        }
        public void Abort() => ScreenFade.Set(0f, color);

        /// <summary>The kit's default, used when no scene has a Szenenübergang.</summary>
        public static readonly ScreenFadeTransition Default = new ScreenFadeTransition();

        // After KitScenes has reset itself (SubsystemRegistration), before the first scene's objects wake up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void NewSession() { if (KitScenes.Transition == null || KitScenes.Transition is ScreenFadeTransition) KitScenes.Transition = Default; }
    }

    /// <summary>
    /// Szenenübergang: how this scene is left and how the next one appears. Put it on any object in the scene;
    /// the curtain is the Blende, so it also covers menus.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Szenenübergang")]
    [DisallowMultipleComponent]
    public class SceneTransition : MonoBehaviour
    {
        [Hint("Farbe des Vorhangs zwischen zwei Szenen.", edges: "Schwarz: klassisch. Weiß: Traum, Erinnerung, Erwachen.", label: "Farbe")]
        public Color color = Color.black;

        [Hint("So lange dauert das Zudecken und das Aufdecken, jeweils (echte Zeit).", unit: "s", range: "0,2 bis 2", edges: "0: harter Schnitt.", label: "Dauer")]
        public float seconds = 0.6f;

        [Hint("Wie Zudecken und Aufdecken verlaufen.", edges: "„Weich“ passt fast immer.", label: "Verlauf")]
        public Ease ease = Ease.Smooth;

        readonly ScreenFadeTransition m_Transition = new ScreenFadeTransition();

        void OnEnable()
        {
            Sync();
            KitScenes.Transition = m_Transition;
        }

        // The scene is being left: the default takes over until the arriving scene's Szenenübergang (if any) wakes
        // up. Revealing then uses the arriving scene's duration and easing (the colour stays the one it was covered with).
        void OnDisable() { if (KitScenes.Transition == m_Transition) KitScenes.Transition = ScreenFadeTransition.Default; }

        void OnValidate() { seconds = Mathf.Max(0f, seconds); Sync(); }

        void Sync() { m_Transition.color = color; m_Transition.seconds = seconds; m_Transition.ease = ease; }
    }
}
