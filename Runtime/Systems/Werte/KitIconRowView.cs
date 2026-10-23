using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    // Symbolreihe: zeigt den Wert als Reihe von Symbolen – drei Herzen, fünf Schlüssel. Bis Maximum stehen
    // leere Plätze, wenn es ein Symbol für „leer“ gibt. Die Symbole sind Kinder dieses Objekts: tauscht sie aus.
    [AddComponentMenu(KitBrand.ComponentMenu + "Anzeige/Symbolreihe")]
    public class KitIconRowView : KitValueView
    {
        [Hint("Ein Symbol für „voll“ (ein Kind-Objekt mit Image). Es wird so oft kopiert, wie der Wert angibt.", label: "Symbol voll")]
        public RectTransform full;
        [Hint("Optional: ein Symbol für „leer“, für die Plätze bis Maximum. Leer lassen = nur volle Symbole.", label: "Symbol leer")]
        public RectTransform empty;
        [Hint("Lücke zwischen zwei Symbolen.", unit: "Pixel (Canvas)", range: "0 bis 20", edges: "Negativ: die Symbole überlappen.", label: "Abstand")]
        public float spacing = 8f;
        [Hint("In welche Richtung die Reihe wächst.", label: "Richtung")]
        public Vector2 direction = Vector2.right;
        [Hint("Höchstens so viele Symbole, auch wenn der Wert größer ist (gegen versehentlich 1000 Herzen).", unit: "Stück", range: "1 bis 100", label: "Höchstens")]
        [Range(1, 100)] public int limit = 20;

        // Copies are kept and reused: a value that goes up and down does not create and destroy objects every time.
        readonly List<RectTransform> m_FullCopies = new List<RectTransform>(), m_EmptyCopies = new List<RectTransform>();
        int m_Full = -1, m_Empty = -1;

        /// <summary>How many full and empty symbols are visible now.</summary>
        public int FullShown => Mathf.Max(0, m_Full);
        public int EmptyShown => Mathf.Max(0, m_Empty);

        public override void Show(float value, float min, float max)
        {
            int nFull = Mathf.Clamp(Mathf.FloorToInt(value - min + 0.0001f), 0, limit);
            int slots = Mathf.Clamp(Mathf.FloorToInt(max - min + 0.0001f), 0, limit);
            int nEmpty = empty != null ? Mathf.Max(0, slots - nFull) : 0;
            if (nFull == m_Full && nEmpty == m_Empty) return;
            m_Full = nFull; m_Empty = nEmpty;
            Layout();
        }

        void Layout()
        {
            if (full == null) return;
            full.gameObject.SetActive(false);                 // the originals are templates
            if (empty != null) empty.gameObject.SetActive(false);
            var dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            float step = Mathf.Abs(Vector2.Dot(full.rect.size, dir)) + spacing;
            Place(m_FullCopies, full, m_Full, 0, dir, step, "voll");
            if (empty != null) Place(m_EmptyCopies, empty, m_Empty, m_Full, dir, step, "leer");
        }

        void Place(List<RectTransform> copies, RectTransform template, int count, int offset, Vector2 dir, float step, string label)
        {
            copies.RemoveAll(c => c == null);                 // a student may have deleted one by hand
            while (copies.Count < count)
            {
                var copy = Instantiate(template, template.parent);
                copy.name = $"{label} {copies.Count + 1}";
                copies.Add(copy);
            }
            for (int i = 0; i < copies.Count; i++)
            {
                bool on = i < count;
                if (on) copies[i].anchoredPosition = full.anchoredPosition + dir * step * (offset + i);
                if (copies[i].gameObject.activeSelf != on) copies[i].gameObject.SetActive(on);
            }
        }
    }
}
