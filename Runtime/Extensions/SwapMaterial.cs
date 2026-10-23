using System;
using UnityEngine;

namespace Kit
{
    // Material tauschen: ersetzt ein Material an einem Renderer, z.B. eine Lampe "an" und "aus".
    // Benutzt das Material-Asset selbst (keine Kopie pro Aufruf). "Zurück" stellt das ursprüngliche wieder her.
    [Serializable]
    [NodeInfo("Material tauschen", KitGroups.Erweiterung, "Material eines Renderers wechseln")]
    public class SwapMaterial : KitNode
    {
        [Hint("Der Renderer (z.B. Mesh Renderer), dessen Material wechselt.")]
        [Ref("Renderer")] public Renderer target;
        [Hint("Das neue Material.", edges: "Leer: Fehler.")]
        [Setting("Material")] public Material material;
        [Hint("Welcher Platz in der Liste „Materials“ des Renderers getauscht wird.", range: "0 bis Anzahl der Plätze minus 1", edges: "0: das erste Material. Gibt es den Platz nicht: Fehler.")]
        [Setting("Platz")] public int slot = 0;

        [Hint("Feuert nach jedem Tausch, auch nach „Zurück“.")]
        [Output("Getauscht")] [NonSerialized] public Output swapped;

        // Ursprüngliches Material pro Renderer: der Renderer kann auch mit dem Ereignis kommen (z.B. ◆ Wer).
        // Neu angelegt in OnStart, also pro Logik.
        System.Collections.Generic.Dictionary<Renderer, Material> originals;

        public override void OnStart() => originals = new System.Collections.Generic.Dictionary<Renderer, Material>();

        [Hint("Setzt das neue Material ein. Das ursprüngliche wird beim ersten Tausch gemerkt.")]
        [Input("Tauschen")] public void Swap(Signal s) => Apply(target, material, s);
        [Hint("Setzt das ursprüngliche Material wieder ein. War nichts getauscht, passiert nichts.")]
        [Input("Zurück")]
        public void Restore(Signal s)
        {
            if (!originals.TryGetValue(target, out var m)) { Done($"„{target.name}“ war nicht getauscht"); return; }
            Apply(target, m, s);
        }

        void Apply(Renderer r, Material m, Signal s)
        {
            var mats = r.sharedMaterials;                          // Kopie des Arrays, nicht der Materialien
            if (slot < 0 || slot >= mats.Length) { Fail($"„{r.name}“ hat keinen Material-Platz {slot}"); return; }
            if (m == null) { Fail("kein Material eingestellt"); return; }
            if (!originals.ContainsKey(r)) originals[r] = mats[slot];   // beim ersten Tausch merken
            mats[slot] = m;
            r.sharedMaterials = mats;
            Done($"✓ {m.name}");
            swapped.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (material == null) problems.Add("kein Material eingestellt");
        }
    }
}
