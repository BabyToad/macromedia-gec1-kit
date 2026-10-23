using System;
using UnityEngine;

namespace Kit
{
    // Musik: macht eine Ebene einer Musik (oder die ganze Musik) lauter oder leiser, mit Überblendung. Der Boss
    // erscheint: Trommeln auf 1. „Zurück“ nimmt die Änderung dieses Knotens wieder weg; dann gilt, was vorher galt.
    [Serializable]
    [NodeInfo("Musik", KitGroups.Atmosphaere, "Ebene einer Musik ein- oder ausblenden")]
    public class MusicChange : KitNode
    {
        [Ref("Musik")]
        [Hint("Die Musik, die sich ändert: ein Objekt mit der Komponente „Musik und Atmo“.")]
        public Music music;

        [Setting("Ebene")]
        [Hint("Name einer Ebene, genau wie an der Musik eingetragen, z. B. „Trommeln“.", edges: "Leer: die ganze Musik.")]
        public string layer = "";

        [Setting("Lautstärke")]
        [Hint("Lautstärke bei „Setzen“.", range: "0 bis 1", edges: "0: stumm. 1: voll hörbar.")]
        public float level = 1f;

        [Setting("Dauer")]
        [Hint("So lange dauert das Überblenden (echte Zeit).", unit: "s", range: "1 bis 4", edges: "0: sofort, wirkt wie ein Schnitt.")]
        public float seconds = 2f;

        [Setting("Verlauf")]
        [Hint("Wie die Überblendung verläuft.", edges: "„Weich“ passt fast immer.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn die Überblendung vorbei ist.")] [Output("Fertig")] [NonSerialized] public Output done;

        int m_Gen;   // each Setzen/Zurück starts a new wait; an older one ends silently

        [Hint("Blendet die Ebene (oder die ganze Musik) auf die Lautstärke.")] [Input("Setzen")]
        public void Set(Signal s)
        {
            if (!music.Wish(this, layer, level, seconds, ease)) { Fail($"„{music.name}“ hat keine Ebene „{layer}“"); return; }
            StartWork(Wait(s, "gesetzt", ++m_Gen, music.Revision(layer)));
        }

        [Hint("Nimmt die Änderung dieses Knotens weg; dann gilt, was vorher galt.")] [Input("Zurück")]
        public void Restore(Signal s)
        {
            music.Unwish(this, seconds, ease);
            StartWork(Wait(s, "zurück", ++m_Gen, music.Revision(layer)));
        }

        // Fertig kommt erst, wenn die Musik wirklich angekommen ist, und nicht, wenn jemand anderes übernommen hat.
        // revision: die Überblendung, die dieser Knoten ausgelöst hat. Ändert irgendwer danach das Ziel (auch derselbe
        // Besitzer wie vorher), ist es nicht mehr seine.
        System.Collections.IEnumerator Wait(Signal s, string what, int gen, int revision)
        {
            var m = music;
            yield return null;
            while (true)
            {
                if (gen != m_Gen) yield break;                         // ein neueres Setzen/Zurück dieses Knotens wartet jetzt
                if (!m) { Fail("◆ Musik wurde gelöscht"); yield break; }
                if (!m.isActiveAndEnabled) { Blocked($"„{m.name}“ ist ausgeschaltet: sie blendet nicht weiter"); yield break; }
                if (m.Revision(layer) != revision) { Blocked("eine neuere Musik-Änderung hat übernommen"); yield break; }
                if (m.Settled(layer)) break;
                Running($"{what}: blendet");
                yield return null;
            }
            Done($"✓ {what}");
            done.Fire(s);
        }

        public override void OnStop()
        {
            if (music) music.Unwish(this, 0.5f);   // Interaktion aus: ihre Musik-Wünsche verklingen mit
        }

        public override string Readout()
        {
            if (!music) return null;
            float l = string.IsNullOrEmpty(layer) ? music.Level : music.LayerLevel(layer);
            return l < 0f ? null : $"{l * 100f:0} % laut";
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (level < 0f || level > 1f) problems.Add("Lautstärke muss zwischen 0 und 1 liegen");
            if (music != null && !string.IsNullOrEmpty(layer) && music.IndexOf(layer) < 0)
                problems.Add($"„{music.name}“ hat keine Ebene „{layer}“ (Ebenen: {string.Join(", ", music.layers.ConvertAll(l => l.name))})");
        }
    }
}
