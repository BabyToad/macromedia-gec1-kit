using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Wert anzeigen: hält eine Anzeige (Zahl, Balken, Symbolreihe – oder eine eigene) auf dem Stand einer Zahl,
    // solange die Logik läuft. Kein Ereignis nötig: der Knoten liest den ● Wert in jedem Frame neu.
    // Zustand und Darstellung sind getrennt: die Variable weiß nichts davon, wie sie aussieht. Dieselbe Zahl kann
    // als Text, als Balken und als Reihe von Herzen erscheinen – die Wahl ist eine Design-Entscheidung.
    [Serializable]
    [NodeInfo("Wert anzeigen", "Werte", "Eine Zahl dauerhaft anzeigen")]
    public class ShowValue : KitNode
    {
        [Hint("Die Anzeige im Canvas: ein Objekt mit Zahl-, Balken- oder Symbolreihe-Anzeige (GameObject › Kit › Anzeige). Auch eine eigene Komponente, die von KitValueView erbt.")]
        [Ref("Anzeige", Permanent = true)] public KitValueView view;
        [Hint("Die Zahl, die gezeigt wird – meist der ● Wert einer Zahl-Variable.")]
        [DataIn("Wert")] public float value;
        [Hint("Bei diesem Wert ist ein Balken leer. Meist 0.")]
        [DataIn("Minimum")] public float min = 0f;
        [Hint("Bei diesem Wert ist ein Balken voll; eine Symbolreihe zeigt so viele Plätze. Kann der Zahl-Text als {1} zeigen.")]
        [DataIn("Maximum")] public float max = 10f;

        [NonSerialized] float m_Shown = float.NaN, m_ShownMin, m_ShownMax;
        [NonSerialized] bool m_Lost;

        public override void OnStart() { m_Shown = float.NaN; m_Lost = false; }

        public override void Tick()
        {
            if (m_Lost) return;
            if (view == null) { m_Lost = true; Fail("◆ Anzeige wurde gelöscht: der Wert hat keinen Platz mehr"); return; }
            Runner.RefreshData(this);                       // ● Wert neu lesen (ohne Ereignis)
            if (value == m_Shown && min == m_ShownMin && max == m_ShownMax) return;
            m_Shown = value; m_ShownMin = min; m_ShownMax = max;
            try { view.Show(value, min, max); }
            catch (Exception e) { m_Lost = true; Fail("Fehler in der Anzeige: " + e.Message); Debug.LogException(e, view); }
        }

        public override void Validate(List<string> problems)
        {
            if (max <= min) problems.Add($"Maximum ({KitValue.FormatNumber(max)}) muss größer als Minimum ({KitValue.FormatNumber(min)}) sein, sonst bleibt ein Balken leer");
        }

        public override string Readout() => float.IsNaN(m_Shown) ? null : $"zeigt {KitValue.FormatNumber(m_Shown)} ({KitValue.FormatNumber(m_ShownMin)} … {KitValue.FormatNumber(m_ShownMax)})";
    }
}
