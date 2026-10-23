using UnityEngine;

namespace Kit
{
    /// <summary>
    /// How a change runs over its duration. One list for the whole kit (Blende, Licht, Zeitlupe, Kamera …), so a
    /// student who learned "Weich" on one node finds it on every other.
    /// </summary>
    public enum Ease
    {
        [InspectorName("Weich (langsam an, langsam aus)")] Smooth,
        [InspectorName("Gleichmäßig")] Linear,
        [InspectorName("Langsam anfangen")] In,
        [InspectorName("Langsam aufhören")] Out,
        [InspectorName("Überschwingen")] Overshoot,
        [InspectorName("Sofort")] Instant,
    }

    public static class Easing
    {
        /// <summary>Maps progress t (0..1) through the curve. Overshoot leaves 0..1 briefly; everything ends at 1.</summary>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.In: return t * t * t;
                case Ease.Out: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.Overshoot:
                {
                    // easeOutBack: passes the target by ~10 % and settles. Reads as "springy" without wobbling.
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.Instant: return 1f;
                default: return t * t * (3f - 2f * t);   // Smooth = smoothstep
            }
        }

        /// <summary>Seconds that actually pass for a change: Sofort is always 0.</summary>
        public static float Duration(Ease ease, float seconds) => ease == Ease.Instant ? 0f : Mathf.Max(0f, seconds);
    }
}
