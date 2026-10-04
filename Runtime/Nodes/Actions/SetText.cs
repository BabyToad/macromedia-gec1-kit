using System;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    // Text setzen: schreibt in ein TextMeshPro-Feld. {0} im Text wird durch die ● Zahl ersetzt,
    // mit deutschem Komma: "Münzen: {0}" → "Münzen: 3", "Zeit: {0} s" → "Zeit: 2,5 s".
    [Serializable]
    [NodeInfo("Text setzen", "Aktion", "Text in ein Textfeld schreiben")]
    public class SetText : KitNode
    {
#if KIT_TMP
        [Ref("Textfeld")] public TMPro.TMP_Text field;
#else
        [Ref("Textfeld")] public UnityEngine.UI.Text field;
#endif
        [Setting("Text")] public string format = "{0}";
        [DataIn("Zahl")] public float number;

        [Output("Gesetzt")] [NonSerialized] public Output written;

        static readonly CultureInfo k_German = CultureInfo.GetCultureInfo("de-DE");

        [Input("Setzen")]
        public void Write(Signal s)
        {
            string text = string.Format(k_German, format, number.ToString("0.##", k_German));
            field.text = text;
            Done($"✓ „{text}“");
            written.Fire(s);
        }
    }
}
