using System;
using UnityEngine;

namespace Kit
{
    // Licht: blendet Farbe und Helligkeit einer Lampe über die Zeit auf neue Werte. Alarm, Sonnenuntergang, eine
    // Kerze, die ausgeht. „Zurück“ holt die Werte vom Spielstart zurück, „Aus“ blendet auf 0 und schaltet ab, „An“
    // schaltet ein und blendet auf die Start-Helligkeit. Ändern zwei Knoten dieselbe Lampe, gilt der neuere.
    [Serializable]
    [NodeInfo("Licht", KitGroups.Atmosphaere, "Farbe und Helligkeit einer Lampe blenden")]
    public class LightChange : KitNode
    {
        [Ref("Licht")]
        [Hint("Die Lampe, die sich ändert: ein Objekt mit Light-Komponente (Directional, Point oder Spot).")]
        public Light target;

        [Setting("Farbe")]
        [Hint("Zielfarbe bei „Ändern“.", edges: "Warm (orange) wirkt gemütlich, kalt (blau) nächtlich, rot nach Alarm.")]
        public Color color = new Color(1f, 0.55f, 0.25f);

        [Setting("Farbe ändern")]
        [Hint("An: „Ändern“ blendet auch die Farbe. Aus: nur die Helligkeit, die Farbe bleibt.")]
        public bool changeColor = true;

        [Setting("Helligkeit")]
        [Hint("Ziel-Helligkeit (Intensity) bei „Ändern“.", range: "Sonne 1 bis 2, Punktlicht 1 bis 10", edges: "0: dunkel.")]
        public float intensity = 2f;

        [Setting("Dauer")]
        [Hint("So lange dauert das Blenden (Spielzeit: Zeitlupe verlangsamt es).", unit: "s", range: "0,2 bis 5", edges: "0: sofort.")]
        public float seconds = 1f;

        [Setting("Verlauf")]
        [Hint("Wie die Änderung über die Dauer läuft.", edges: "„Überschwingen“ lässt das Licht kurz aufflammen.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn das Licht angekommen ist. Übernimmt ein neuerer Licht-Knoten, kommt es nicht.")] [Output("Fertig")] [NonSerialized] public Output done;

        struct Look { public Color color; public float intensity; public bool on; }

        [Hint("Blendet zu Farbe und Helligkeit dieses Knotens.")] [Input("Ändern")] public void Change(Signal s) => To(new Look { color = changeColor ? color : target.color, intensity = intensity, on = true }, "geändert", s);
        [Hint("Blendet zurück zu Farbe und Helligkeit vom Spielstart.")] [Input("Zurück")] public void Restore(Signal s) => To(Initial(), "zurück", s);
        [Hint("Schaltet die Lampe ein und blendet auf die Helligkeit vom Spielstart.")] [Input("An")] public void On(Signal s)
        {
            var st = Initial(); st.on = true;                          // An heißt an, auch wenn die Lampe beim Start aus war
            if (!target.enabled) { target.intensity = 0f; target.enabled = true; }
            To(st, "an", s);
        }
        [Hint("Blendet auf dunkel und schaltet die Lampe dann ab.")] [Input("Aus")] public void Off(Signal s) => To(new Look { color = target.color, intensity = 0f, on = false }, "aus", s);

        Look Initial() => Blend.Original(target, () => new Look { color = target.color, intensity = target.intensity, on = target.enabled });

        void To(Look to, string what, Signal s)
        {
            var l = target;
            Initial();                                                   // Startwerte merken, bevor sich etwas ändert
            if (to.on) l.enabled = true;
            Color c0 = l.color; float i0 = l.intensity;
            int ticket = Blend.Claim(l, this);
            StartWork(Wrap(Blend.Run(seconds, ease, false, k =>
            {
                l.color = Blend.Lerp(c0, to.color, k);
                l.intensity = Mathf.Max(0f, Mathf.LerpUnclamped(i0, to.intensity, k));
            }, l, ticket, t => Running($"{what}: {t * 100f:0} %", t)), l, ticket, to, what, s));
        }

        System.Collections.IEnumerator Wrap(System.Collections.IEnumerator run, Light l, int ticket, Look to, string what, Signal s)
        {
            while (run.MoveNext()) yield return run.Current;
            if (!l) { Fail("◆ Licht wurde gelöscht"); yield break; }
            if (!Blend.Holds(l, ticket)) { Blocked("ein neueres Licht-Blenden hat übernommen"); yield break; }
            Blend.Let(l, ticket);
            if (!to.on) l.enabled = false;
            Done($"✓ {what}");
            done.Fire(s);
        }

        public override string Readout() => target ? $"{target.intensity:0.##} hell" + (target.enabled ? "" : " (aus)") : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (intensity < 0f) problems.Add("Helligkeit darf nicht negativ sein");
            if (seconds < 0f) problems.Add("Dauer darf nicht negativ sein");
        }
    }
}
