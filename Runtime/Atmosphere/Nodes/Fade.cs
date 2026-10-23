using System;
using UnityEngine;

namespace Kit
{
    // Blende: deckt den ganzen Bildschirm mit einer Farbe zu (Abblenden) und gibt ihn wieder frei (Aufblenden).
    // Für Szenenwechsel, Ohnmacht, Kapitelanfang, einen weißen Blitz. Läuft in echter Zeit, also auch in Pause und
    // Zeitlupe, und bleibt über einen Szenenwechsel hinweg stehen. Eine neue Blende löst die laufende ab.
    [Serializable]
    [NodeInfo("Blende", KitGroups.Atmosphaere, "Bildschirm in eine Farbe ab- oder aufblenden")]
    public class Fade : KitNode
    {
        [Setting("Farbe")]
        [Hint("Farbe, mit der der Bildschirm zugedeckt wird.", edges: "Schwarz für Szenenwechsel, Weiß für einen Blitz, Rot für einen Schreck.")]
        public Color color = Color.black;

        [Setting("Dauer")]
        [Hint("So lange dauert das Ab- oder Aufblenden (echte Zeit, auch in Pause).", unit: "s", range: "0,2 bis 3", edges: "0,3: schnell. 1: ruhig. 3: feierlich. 0: sofort.")]
        public float seconds = 0.6f;

        [Setting("Verlauf")]
        [Hint("Wie die Blende über die Dauer läuft.", edges: "„Weich“ passt fast immer; „Langsam anfangen“ zieht das Ende an, „Sofort“ springt.")]
        public Ease ease = Ease.Smooth;

        [Setting("Deckkraft")]
        [Hint("Wie weit „Abblenden“ den Bildschirm zudeckt.", range: "0,3 bis 1", edges: "1: ganz zu. 0,6: abgedunkelt, z. B. hinter einem Dialog.")]
        public float amount = 1f;

        [Hint("Kommt, wenn die Blende angekommen ist. Löst eine neuere Blende sie ab, kommt es nicht.")] [Output("Fertig")] [NonSerialized] public Output done;

        FadeHandle m_Handle;

        [Hint("Deckt den Bildschirm mit der Farbe zu.")] [Input("Abblenden", UsesRefs = false)] public void FadeOut(Signal s) => Run(ScreenFade.To(amount, color, seconds, ease), "abblenden", s);
        [Hint("Gibt den Bildschirm wieder frei.")] [Input("Aufblenden", UsesRefs = false)] public void FadeIn(Signal s) => Run(ScreenFade.In(seconds, ease), "aufblenden", s);

        void Run(FadeHandle h, string what, Signal s)
        {
            m_Handle = h;
            if (h.IsDone) { Done($"✓ {what}"); done.Fire(s); return; }
            Running(what);
            StartWork(Wait(h, what, s));
        }

        System.Collections.IEnumerator Wait(FadeHandle h, string what, Signal s)
        {
            while (!h.IsDone)
            {
                Running($"{what}: {ScreenFade.Amount * 100f:0} % zu", ScreenFade.Amount);
                yield return null;
            }
            if (h.Interrupted) { Blocked("eine neuere Blende hat übernommen"); yield break; }
            Done($"✓ {what}");
            done.Fire(s);
        }

        public override string Readout() => ScreenFade.Amount > 0f ? $"Bildschirm {ScreenFade.Amount * 100f:0} % zu" : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (seconds < 0f) problems.Add("Dauer darf nicht negativ sein");
            if (amount <= 0f || amount > 1f) problems.Add("Deckkraft beim Abblenden muss zwischen 0 und 1 liegen (0 wäre unsichtbar)");
        }
    }
}
