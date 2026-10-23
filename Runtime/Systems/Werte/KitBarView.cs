using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    // Balken-Anzeige: füllt einen Balken von Minimum (leer) bis Maximum (voll). Die Füllung ist ein beliebiges
    // UI-Objekt; der Balken ändert nur, wie weit es reicht – Farbe, Form und Rahmen bleiben eure Sache.
    [AddComponentMenu(KitBrand.ComponentMenu + "Anzeige/Balken")]
    public class KitBarView : KitValueView
    {
        public enum Direction { [InspectorName("nach rechts")] Rechts, [InspectorName("nach links")] Links, [InspectorName("nach oben")] Oben, [InspectorName("nach unten")] Unten }

        [Hint("Die Füllung (ein Kind-Objekt mit Image). Sie wird von einer Seite her auf den Anteil gestreckt.", label: "Füllung")]
        public RectTransform fill;
        [Hint("In welche Richtung der Balken wächst, wenn der Wert steigt.", label: "Richtung")]
        public Direction direction = Direction.Rechts;
        [Hint("So lange gleitet der Balken zum neuen Stand.", unit: "s", range: "0 bis 0,5", edges: "0: springt sofort.", label: "Gleiten")]
        [Min(0)] public float smoothSeconds = 0.15f;

        float m_Shown = -1f, m_Target;

        public override void Show(float value, float min, float max)
        {
            m_Target = Fraction(value, min, max);
            if (m_Shown < 0f || smoothSeconds <= 0f) Apply(m_Shown = m_Target);
        }

        void Update()
        {
            if (m_Shown < 0f || Mathf.Approximately(m_Shown, m_Target)) return;
            m_Shown = Mathf.MoveTowards(m_Shown, m_Target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, smoothSeconds));
            Apply(m_Shown);
        }

        /// <summary>How full the bar is right now (0 … 1).</summary>
        public float Shown => Mathf.Max(0f, m_Shown);

        void Apply(float f)
        {
            if (fill == null) return;
            Vector2 lo = Vector2.zero, hi = Vector2.one;
            switch (direction)
            {
                case Direction.Rechts: hi.x = f; break;
                case Direction.Links: lo.x = 1f - f; break;
                case Direction.Oben: hi.y = f; break;
                default: lo.y = 1f - f; break;
            }
            fill.anchorMin = lo; fill.anchorMax = hi;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
    }
}
