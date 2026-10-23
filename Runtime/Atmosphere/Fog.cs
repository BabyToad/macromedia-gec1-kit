using UnityEngine;

namespace Kit
{
    /// <summary>
    /// The scene's fog (Lighting › Environment › Fog) in one understandable number: Sichtweite, the distance in
    /// metres at which the world has vanished in the fog. The kit uses Exponential Squared fog: clear near the
    /// camera, then closing in fast, which reads as "Nebel" rather than "dunstig".
    /// </summary>
    public struct FogLook
    {
        public bool on;
        public Color color;
        public float visibility;   // metres; where ~98 % of the view is fog
        // The scene's own fog exactly as it was (mode, density, distances). Set by Current(); Apply() then restores it
        // bit for bit, so "Zurück" and leaving a Nebel-Zone never change a Linear or Exponential fog into another one.
        public bool exact;
        public FogMode mode;
        public float density, start, end;

        /// <summary>Visibility that counts as "no fog" when fading fog in or out.</summary>
        public const float Clear = 2000f;
        // ExponentialSquared: f = exp(-(density·d)²) = 0.02 at d = visibility → density = √(-ln 0.02) / visibility
        const float k_Factor = 1.9779f;

        public static FogLook Current()
        {
            float d = RenderSettings.fogDensity;
            float vis;
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear: vis = RenderSettings.fogEndDistance; break;
                case FogMode.Exponential: vis = d > 0f ? 3.912f / d : Clear; break;     // exp(-d·x) = 0.02
                default: vis = d > 0f ? k_Factor / d : Clear; break;
            }
            return new FogLook
            {
                on = RenderSettings.fog, color = RenderSettings.fogColor, visibility = Mathf.Min(vis, Clear),
                exact = true, mode = RenderSettings.fogMode, density = d,
                start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance,
            };
        }

        /// <summary>Writes this look into the scene's RenderSettings.</summary>
        public void Apply()
        {
            if (exact)
            {
                RenderSettings.fog = on; RenderSettings.fogColor = color; RenderSettings.fogMode = mode;
                RenderSettings.fogDensity = density; RenderSettings.fogStartDistance = start; RenderSettings.fogEndDistance = end;
                return;
            }
            RenderSettings.fog = on;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = k_Factor / Mathf.Max(0.5f, visibility);
        }

        /// <summary>Blends two looks. Fading from or to "off" blends through clear air instead of jumping.</summary>
        public static FogLook Lerp(FogLook a, FogLook b, float k)
        {
            if (k == 1f) return b;                                      // the end (Blend.Run ends on exactly 1) is the target itself, also an exact original
            float va = a.on ? a.visibility : Clear, vb = b.on ? b.visibility : Clear;
            Color ca = a.on ? a.color : b.color, cb = b.on ? b.color : a.color;
            // Visibility blends in 1/x: the eye reads density, so 2000 → 20 m must not stay clear until the last frame.
            float inv = Mathf.LerpUnclamped(1f / va, 1f / vb, k);
            return new FogLook
            {
                on = a.on || b.on,
                color = Blend.Lerp(ca, cb, k),
                visibility = 1f / Mathf.Max(1f / Clear, inv),
            };
        }
    }
}
