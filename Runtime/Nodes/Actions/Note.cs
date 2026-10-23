using System;
using UnityEngine;

namespace Kit
{
    // Notiz in Konsole: schreibt eine Zeile in die Console. Meist nicht mehr nötig –
    // der Graph zeigt selbst, was passiert. Gut, um eigenen C#-Code zu vergleichen.
    [Serializable]
    [NodeInfo("Notiz in Konsole", KitGroups.Aktion, "Eine Zeile in die Console schreiben")]
    public class Note : KitNode
    {
        [Hint("Die Zeile, die in die Console geschrieben wird. Davor steht der Name des Objekts mit der Logik.")]
        [Setting("Text")] public string text = "Hallo";

        [Hint("Feuert direkt nach dem Schreiben.")]
        [Output("Geschrieben")] [NonSerialized] public Output written;

        [Hint("Schreibt den Text in die Console.")]
        [Input("Schreiben")]
        public void Write(Signal s)
        {
            Debug.Log($"[{Runner.name}] {text}", Runner);
            written.Fire(s);
        }
    }
}
