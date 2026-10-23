using System;
using System.Globalization;
using UnityEngine;

namespace Kit
{
    // Text setzen: schreibt in ein TextMeshPro-Feld. {0} im Text wird durch die ● Zahl ersetzt,
    // mit deutschem Komma: "Münzen: {0}" → "Münzen: 3", "Zeit: {0} s" → "Zeit: 2,5 s".
    [Serializable]
    [NodeInfo("Text setzen", KitGroups.Aktion, "Text in ein Textfeld schreiben")]
    public class SetText : KitNode
    {
#if KIT_TMP
        [Hint("Das TextMeshPro-Textfeld, in das geschrieben wird, im Canvas oder in der Welt.")]
        [Ref("Textfeld")] public TMPro.TMP_Text field;
#else
        [Hint("Das Textfeld (UI Text), in das geschrieben wird.")]
        [Ref("Textfeld")] public UnityEngine.UI.Text field;
#endif
        [Hint("Der Text, der erscheint. {0} wird durch die ● Zahl ersetzt, z.B. „Münzen: {0}“.", edges: "Ohne {0}: der Text steht fest da. Andere geschweifte Klammern wie {1}: Fehler am Knoten, der Text bleibt.")]
        [Setting("Text")] public string format = "{0}";
        [Hint("Die Zahl, die für {0} eingesetzt wird, mit Komma und höchstens zwei Nachkommastellen.")]
        [DataIn("Zahl")] public float number;

        [Hint("Feuert direkt nach dem Schreiben.")]
        [Output("Gesetzt")] [NonSerialized] public Output written;

        static readonly CultureInfo k_German = CultureInfo.GetCultureInfo("de-DE");

        [Hint("Schreibt den Text mit der aktuellen Zahl ins Textfeld.")]
        [Input("Setzen")]
        public void Write(Signal s)
        {
            string text;
            try { text = string.Format(k_German, format, number.ToString("0.##", k_German)); }
            catch (FormatException)
            {
                // z.B. "{1}" oder eine einzelne "{": nur {0} ist erlaubt.
                Fail($"Text „{format}“: nur {{0}} ist als Platzhalter erlaubt (geschweifte Klammern sonst verdoppeln: {{{{ }}}})");
                return;
            }
            field.text = text;
            Done($"✓ „{text}“");
            written.Fire(s);
        }
    }
}
