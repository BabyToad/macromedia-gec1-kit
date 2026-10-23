using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    // Zahl-Anzeige: schreibt die Zahl in ein Textfeld, z.B. „Münzen: 3“ oder „3 / 10“.
    [AddComponentMenu(KitBrand.ComponentMenu + "Anzeige/Zahl")]
    public class KitNumberView : KitValueView
    {
        [Hint("Das Textfeld (TextMeshPro). Leer: das Textfeld an diesem Objekt.", label: "Textfeld")]
        public TMPro.TMP_Text field;
        [Hint("Was dasteht. {0} = der Wert, {1} = Maximum, {2} = Minimum. Beispiele: „Münzen: {0}“, „{0} / {1}“, „{0} s“.", label: "Text")]
        public string format = "{0}";
        [Hint("Wie viele Stellen nach dem Komma.", range: "0 bis 3", edges: "0: nur ganze Zahlen (gerundet).", label: "Nachkommastellen")]
        [Range(0, 3)] public int decimals = 0;
        [Hint("So lange zählt die Zahl zum neuen Wert hoch oder herunter.", unit: "s", range: "0 bis 1", edges: "0: springt sofort.", label: "Hochzählen")]
        [Min(0)] public float countSeconds = 0f;

        static readonly CultureInfo k_German = CultureInfo.GetCultureInfo("de-DE");
        float m_From, m_To, m_Min, m_Max, m_Start = -1f;

        void Reset() => field = GetComponent<TMPro.TMP_Text>();

        public override void Show(float value, float min, float max)
        {
            m_From = m_Start < 0f ? value : Current();
            m_To = value; m_Min = min; m_Max = max;
            m_Start = countSeconds > 0f ? Time.unscaledTime : 0f;
            Write(countSeconds > 0f ? m_From : value);
        }

        float Current() => countSeconds <= 0f ? m_To : Mathf.Lerp(m_From, m_To, Mathf.Clamp01((Time.unscaledTime - m_Start) / countSeconds));

        void Update()
        {
            if (countSeconds > 0f && m_Start >= 0f && Time.unscaledTime - m_Start <= countSeconds + Time.unscaledDeltaTime) Write(Current());
        }

        void Write(float shown)
        {
            var target = field != null ? field : GetComponent<TMPro.TMP_Text>();
            if (target == null) return;
            string pattern = decimals == 0 ? "0" : "0." + new string('#', decimals);
            string Num(float v) => (decimals == 0 ? Mathf.Round(v) : v).ToString(pattern, k_German);
            try { target.text = string.Format(k_German, format ?? "{0}", Num(shown), Num(m_Max), Num(m_Min)); }
            catch (System.FormatException) { target.text = Num(shown) + "  (Format prüfen: nur {0}, {1}, {2})"; }
        }
    }
}
