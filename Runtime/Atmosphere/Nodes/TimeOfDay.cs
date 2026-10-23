using System;
using UnityEngine;

namespace Kit
{
    // Tageszeit: lässt die Uhrzeit einer Tageszeit-Komponente vorwärts zu einer neuen Stunde laufen. Der Abend bricht
    // herein, wenn das letzte Rätsel gelöst ist; die Nacht vergeht beim Schlafen.
    [Serializable]
    [NodeInfo("Tageszeit", KitGroups.Atmosphaere, "Uhrzeit (Sonne, Himmelslicht) zu einer Stunde laufen lassen")]
    public class TimeOfDay : KitNode
    {
        [Ref("Tageszeit")]
        [Hint("Die Sonne mit der Komponente „Tageszeit“.")]
        public DayCycle day;

        [Setting("Uhrzeit")]
        [Hint("Zielzeit. Die Zeit läuft immer vorwärts, auch über Mitternacht hinweg.", unit: "h", range: "0 bis 24", edges: "6: Morgen. 12: Mittag. 19: Abend. 23: Nacht.")]
        public float hour = 19f;

        [Setting("Dauer")]
        [Hint("So lange läuft die Zeit bis dorthin (Spielzeit).", unit: "s", range: "2 bis 20", edges: "0: sofort.")]
        public float seconds = 5f;

        [Setting("Verlauf")]
        [Hint("Wie die Zeit läuft.", edges: "„Weich“ fährt sanft an und bremst sanft.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn die Uhrzeit erreicht ist.")] [Output("Fertig")] [NonSerialized] public Output done;

        [Hint("Lässt die Uhrzeit vorwärts zur eingestellten Stunde laufen.")] [Input("Laufen lassen")]
        public void Go(Signal s)
        {
            var d = day;
            float from = d.hour, to = Mathf.Repeat(hour, 24f);
            if (to < from) to += 24f;                                   // vorwärts über Mitternacht
            int ticket = Blend.Claim(d, this);
            StartWork(Wrap(Blend.Run(seconds, ease, false, k => { d.hour = Mathf.Repeat(Mathf.LerpUnclamped(from, to, k), 24f); d.Apply(); },
                                     d, ticket, t => Running($"{DayCycle.Clock(d.hour)} Uhr", t)), d, ticket, s));
        }

        System.Collections.IEnumerator Wrap(System.Collections.IEnumerator run, DayCycle d, int ticket, Signal s)
        {
            while (run.MoveNext()) yield return run.Current;
            if (!d) { Fail("◆ Tageszeit wurde gelöscht"); yield break; }
            if (!Blend.Holds(d, ticket)) { Blocked("ein neuerer Tageszeit-Knoten hat übernommen"); yield break; }
            Blend.Let(d, ticket);
            Done($"✓ {DayCycle.Clock(d.hour)} Uhr");
            done.Fire(s);
        }

        public override string Readout() => day ? $"{DayCycle.Clock(day.hour)} Uhr" : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (hour < 0f || hour > 24f) problems.Add("Uhrzeit muss zwischen 0 und 24 liegen");
            if (day != null && day.running) problems.Add($"„{day.name}“: die Zeit läuft schon von selbst (Läuft an); beides zugleich kämpft gegeneinander");
        }
    }
}
