using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kit
{
    // Bildstimmung: blendet eine Volume (Post-Processing) ein oder aus. Ein roter Rand beim Erschrecken, ein
    // Traumfilter, wenn die Figur einschläft, Entsättigung in einer Erinnerung. Den Look stellt das Profil der
    // Volume ein; der Knoten sagt nur, wann und wie stark.
    [Serializable]
    [NodeInfo("Bildstimmung", KitGroups.Atmosphaere, "Post-Processing (Volume) ein- oder ausblenden")]
    public class MoodChange : KitNode
    {
        [Ref("Volume")]
        [Hint("Eine Volume-Komponente mit Profil (Farbe, Vignette, Bloom …). Fertige Stimmungen: GameObject › " + KitBrand.Name + " › Atmosphäre › Bildstimmung.")]
        public Volume volume;

        [Setting("Stärke")]
        [Hint("Wie stark das Profil bei „Einblenden“ wirkt.", range: "0 bis 1", edges: "1: ganz. 0,5: halb.")]
        public float strength = 1f;

        [Setting("Dauer")]
        [Hint("So lange dauert das Blenden (echte Zeit).", unit: "s", range: "0,1 bis 3", edges: "0,1: ein Schreck. 3: ein Traum. 0: sofort.")]
        public float seconds = 1f;

        [Setting("Verlauf")]
        [Hint("Wie die Änderung über die Dauer läuft.", edges: "„Weich“ passt fast immer.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn das Blenden vorbei ist.")] [Output("Fertig")] [NonSerialized] public Output done;

        [Hint("Blendet die Bildstimmung auf die eingestellte Stärke ein.")] [Input("Einblenden")] public void In(Signal s) => To(strength, "eingeblendet", s);
        [Hint("Blendet die Bildstimmung aus.")] [Input("Ausblenden")] public void Out(Signal s) => To(0f, "ausgeblendet", s);
        [Hint("Zeigt die Bildstimmung sofort ganz und blendet sie über die Dauer aus (ein Schreck).")] [Input("Aufblitzen")] public void Flash(Signal s) { volume.weight = Mathf.Clamp01(strength); To(0f, "aufgeblitzt", s); }

        void To(float to, string what, Signal s)
        {
            PostFx.EnsureOnMainCamera();
            var v = volume; float from = v.weight;
            int ticket = Blend.Claim(v, this);
            StartWork(Wrap(Blend.Run(seconds, ease, true, k => v.weight = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, k)), v, ticket,
                                     t => Running($"{what}: {t * 100f:0} %", t)), v, ticket, what, s));
        }

        System.Collections.IEnumerator Wrap(System.Collections.IEnumerator run, Volume v, int ticket, string what, Signal s)
        {
            while (run.MoveNext()) yield return run.Current;
            if (!v) { Fail("◆ Volume wurde gelöscht"); yield break; }
            if (!Blend.Holds(v, ticket)) { Blocked("eine neuere Bildstimmung hat übernommen"); yield break; }
            Blend.Let(v, ticket);
            Done($"✓ {what}");
            done.Fire(s);
        }

        public override string Readout() => volume ? $"Gewicht {volume.weight:0.00}" : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (strength < 0f || strength > 1f) problems.Add("Stärke muss zwischen 0 und 1 liegen");
            if (volume != null && volume.sharedProfile == null) problems.Add($"„{volume.name}“: die Volume hat kein Profil, es gäbe nichts zu sehen");
        }
    }
}
