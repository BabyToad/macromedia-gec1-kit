using System;
using System.Globalization;

namespace Kit
{
    // HUD-Anzeige: zeigt eine Zahl dauerhaft in einem Textfeld an (Leben, Münzen, Zeit) und hält sie aktuell,
    // solange die Interaktion läuft. Im Unterschied zu "Text setzen" braucht es kein Ereignis.
    // {0} im Text wird durch die ● Zahl ersetzt, mit deutschem Komma.
    [Serializable]
    [NodeInfo("HUD-Anzeige", "Spielsysteme", "Zahl dauerhaft anzeigen")]
    public class HudDisplay : KitNode
    {
        [Ref("Textfeld", Permanent = true)] public TMPro.TMP_Text field;
        [Setting("Text")] public string format = "Münzen: {0}";
        [DataIn("Zahl")] public float number;

        static readonly CultureInfo k_German = CultureInfo.GetCultureInfo("de-DE");
        string shown;
        bool lost;

        public override void Tick()
        {
            if (lost) return;
            if (field == null)                               // Textfeld gelöscht: einmal melden, dann Ruhe
            {
                lost = true;
                Fail("◆ Textfeld wurde gelöscht: die Anzeige hat kein Ziel mehr");
                return;
            }
            Runner.RefreshData(this);                       // ● Zahl neu lesen (ohne Ereignis)
            string text = string.Format(k_German, format, number.ToString("0.##", k_German));
            if (text == shown) return;                       // nur schreiben, wenn sich etwas geändert hat
            field.text = shown = text;
        }

        public override string Readout() => shown != null ? $"zeigt „{shown}“" : null;
    }
}
