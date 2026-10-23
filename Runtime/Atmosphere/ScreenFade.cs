using System;
using UnityEngine;

namespace Kit
{
    /// <summary>A running fade. Yield it in a coroutine, or check <see cref="IsDone"/> / listen to <see cref="Completed"/>.</summary>
    public sealed class FadeHandle : CustomYieldInstruction
    {
        /// <summary>The fade reached its end, or a newer fade replaced it (then <see cref="Interrupted"/> is true).</summary>
        public bool IsDone { get; private set; }
        /// <summary>A newer fade took over before this one finished.</summary>
        public bool Interrupted { get; private set; }
        /// <summary>Called once when the fade ends or is replaced. A handle that is already done never calls it again.</summary>
        public event Action Completed;

        public override bool keepWaiting => !IsDone;

        internal void Finish(bool interrupted)
        {
            if (IsDone) return;
            IsDone = true; Interrupted = interrupted;
            var c = Completed; Completed = null;
            c?.Invoke();
        }
    }

    /// <summary>
    /// Blende: covers the whole screen with a colour and uncovers it again. One screen, one Blende: the newest fade
    /// wins. Runs in real time (also while the game is paused or in Zeitlupe) and stays across scene loads, so a
    /// scene change can fade out, load and fade in. Drawn above every UI, including menus.
    /// </summary>
    public static class ScreenFade
    {
        static ScreenFadeOverlay s_Overlay;
        static float s_Amount;
        static Color s_Color = Color.black;
        static FadeHandle s_Running;
        static float s_From, s_To, s_Start, s_Duration;
        static Color s_FromColor, s_ToColor;
        static Ease s_Ease;

        /// <summary>0 = screen free, 1 = fully covered.</summary>
        public static float Amount => s_Amount;
        /// <summary>The colour the screen is (being) covered with.</summary>
        public static Color Color => s_Color;
        public static bool IsFading => s_Running != null && !s_Running.IsDone;

        /// <summary>Called whenever the amount or colour changes (for tests and live readouts).</summary>
        public static event Action Changed;

        /// <summary>Abblenden: covers the screen with <paramref name="color"/>.</summary>
        public static FadeHandle Out(Color color, float seconds = 0.5f, Ease ease = Ease.Smooth) => To(1f, color, seconds, ease);

        /// <summary>Aufblenden: uncovers the screen (keeps the current colour while it fades away).</summary>
        public static FadeHandle In(float seconds = 0.5f, Ease ease = Ease.Smooth) => To(0f, s_Color, seconds, ease);

        /// <summary>Fades to any coverage, e.g. 0.6 for a dimmed screen behind a dialog.</summary>
        public static FadeHandle To(float amount, Color color, float seconds, Ease ease = Ease.Smooth)
        {
            color.a = 1f;                                 // coverage is Amount; the colour itself stays opaque
            var handle = new FadeHandle();
            var replaced = s_Running;
            s_Running = handle;                           // first take over, then tell the old one: its Completed may start a newer fade
            s_From = s_Amount; s_To = Mathf.Clamp01(amount);
            // Coming from a free screen, the colour switches at once (nothing of the old colour is visible).
            s_FromColor = s_Amount <= 0.0001f ? color : s_Color; s_ToColor = color;
            s_Ease = ease; s_Duration = Easing.Duration(ease, seconds); s_Start = Time.unscaledTime;
            replaced?.Finish(true);
            if (s_Running != handle) return handle;       // a callback already started a newer fade; this one was replaced
            if (s_Duration <= 0f) { Apply(s_To, color); End(); return handle; }
            Overlay();
            return handle;
        }

        /// <summary>Sets coverage and colour at once and cancels a running fade.</summary>
        public static void Set(float amount, Color color)
        {
            var replaced = s_Running;
            s_Running = null;
            color.a = 1f;
            Apply(Mathf.Clamp01(amount), color);
            replaced?.Finish(true);
        }

        internal static void Step()
        {
            if (s_Running == null) return;
            float t = (Time.unscaledTime - s_Start) / s_Duration;
            float k = Easing.Evaluate(s_Ease, t);
            Apply(Mathf.LerpUnclamped(s_From, s_To, k), Color.Lerp(s_FromColor, s_ToColor, Mathf.Clamp01(k)));
            if (t >= 1f) { Apply(s_To, s_ToColor); End(); }
        }

        static void End()
        {
            var h = s_Running; s_Running = null;
            h?.Finish(false);
        }

        static void Apply(float amount, Color color)
        {
            amount = Mathf.Clamp01(amount);               // Überschwingen must not show more than "covered"
            if (amount == s_Amount && color == s_Color) return;
            s_Amount = amount; s_Color = color;
            if (s_Amount > 0f) Overlay();
            Changed?.Invoke();
        }

        static void Overlay()
        {
            if (s_Overlay || !Application.isPlaying) return;
            var go = new GameObject("Blende (Kit)");
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_Overlay = go.AddComponent<ScreenFadeOverlay>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            s_Running?.Finish(true);
            s_Running = null; s_Amount = 0f; s_Color = Color.black; s_Overlay = null;
        }
    }
}
