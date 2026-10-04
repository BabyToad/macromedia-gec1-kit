using System;
using UnityEngine;

namespace Kit
{
    // Notiz in Konsole: schreibt eine Zeile in die Console. Meist nicht mehr nötig –
    // der Graph zeigt selbst, was passiert. Gut, um eigenen C#-Code zu vergleichen.
    [Serializable]
    [NodeInfo("Notiz in Konsole", "Aktion", "Eine Zeile in die Console schreiben")]
    public class Note : KitNode
    {
        [Setting("Text")] public string text = "Hallo";

        [Output("Geschrieben")] [NonSerialized] public Output written;

        [Input("Schreiben")]
        public void Write(Signal s)
        {
            Debug.Log($"[{Runner.name}] {text}", Runner);
            written.Fire(s);
        }
    }
}
