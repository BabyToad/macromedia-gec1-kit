using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Lebendiges Licht: lets a lamp flicker like a candle, stutter like a broken neon tube, breathe slowly or blink.
    /// Works on top of whatever else changes the light (the node Licht can still fade it): the flicker multiplies
    /// the brightness the light has anyway.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Lebendiges Licht")]
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public enum Style
        {
            [InspectorName("Kerze / Feuer")] Candle,
            [InspectorName("Kaputte Neonröhre")] Neon,
            [InspectorName("Atmen (langsam pulsieren)")] Breathe,
            [InspectorName("Blinken")] Blink,
        }

        [Hint("Wie das Licht lebt. Kerze: unruhiges Züngeln. Neonröhre: meist an, manchmal Aussetzer. Atmen: ruhiges Auf und Ab. Blinken: an, aus, an.", label: "Art")]
        public Style style = Style.Candle;

        [Hint("Wie stark die Helligkeit schwankt.", range: "0,1 bis 0,6", edges: "0: gar nicht. 1: bis ganz dunkel.", label: "Stärke")]
        [Range(0f, 1f)] public float strength = 0.35f;

        [Hint("Wie schnell das Licht lebt. Bei 1 züngelt eine Kerze, ein Atemzug dauert etwa 3 Sekunden.", range: "0,3 bis 3", edges: "Klein: träge. Groß: hektisch.", label: "Tempo")]
        [Range(0.1f, 5f)] public float speed = 1f;

        Light m_Light;
        float m_Base;             // brightness without the flicker
        float m_Written = -1f;    // what the flicker wrote last frame; anything else means someone else set the light
        float m_Seed;
        float m_NeonOff;          // Neon: dark until this time

        void OnEnable() { m_Light = GetComponent<Light>(); m_Seed = Random.value * 100f; m_Written = -1f; }

        void OnDisable()
        {
            if (m_Light && Mathf.Approximately(m_Light.intensity, m_Written)) m_Light.intensity = m_Base;   // die eigentliche Helligkeit zurück
            m_Written = -1f;
        }

        void LateUpdate()
        {
            // Hat seit dem letzten Frame jemand anderes die Helligkeit gesetzt (der Knoten „Licht“), ist das die neue Grundhelligkeit.
            if (!Mathf.Approximately(m_Light.intensity, m_Written)) m_Base = m_Light.intensity;
            m_Written = m_Base * Factor(Time.time * speed);
            m_Light.intensity = m_Written;
        }

        float Factor(float t)
        {
            switch (style)
            {
                case Style.Neon:
                    if (Time.time < m_NeonOff) return 1f - strength;
                    if (Random.value < 0.012f * speed) m_NeonOff = Time.time + Random.Range(0.04f, 0.18f);   // ein Aussetzer
                    return 1f - strength * 0.08f * Mathf.PerlinNoise(t * 30f, m_Seed);
                case Style.Breathe:
                    return 1f - strength * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 3f));
                case Style.Blink:
                    return Mathf.Repeat(t, 1f) < 0.5f ? 1f : 1f - strength;
                default:
                    // Zwei Rauschfrequenzen: ein langsames Wogen und ein schnelles Züngeln.
                    float n = 0.6f * Mathf.PerlinNoise(t * 1.7f, m_Seed) + 0.4f * Mathf.PerlinNoise(t * 9f, m_Seed + 7f);
                    return 1f - strength * n;
            }
        }
    }
}
