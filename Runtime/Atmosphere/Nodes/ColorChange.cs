using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Farbe: blendet die Farbe eines Objekts (Grundfarbe oder Leuchten) auf eine neue, oder lässt sie kurz
    // aufblitzen und zurückkehren. Eine Lampe glüht auf, ein Kristall erwacht, eine Figur blitzt weiß bei Berührung.
    // Ändert nur dieses eine Objekt: das Material-Asset und andere Objekte mit demselben Material bleiben, wie sie sind.
    [Serializable]
    [NodeInfo("Farbe", KitGroups.Atmosphaere, "Farbe oder Leuchten eines Objekts blenden oder aufblitzen")]
    public class ColorChange : KitNode
    {
        public enum Channel
        {
            [InspectorName("Grundfarbe")] Base,
            [InspectorName("Leuchten")] Emission,
        }

        [Ref("Objekt")]
        [Hint("Das Objekt, dessen Farbe sich ändert: eines mit Renderer (Mesh Renderer, Skinned Mesh Renderer, Sprite …).")]
        public Renderer target;

        [Setting("Was")]
        [Hint("Grundfarbe: die Farbe der Oberfläche. Leuchten: selbstleuchtend (Emission), sichtbar auch im Dunkeln und mit Bloom als Glühen.")]
        public Channel channel = Channel.Base;

        [Setting("Farbe")]
        [Hint("Zielfarbe bei „Ändern“ und Farbe des Blitzes bei „Aufblitzen“.")]
        public Color color = Color.white;

        [Setting("Leuchtstärke")]
        [Hint("Nur für Leuchten: Faktor auf die Farbe.", range: "1 bis 6", edges: "0: leuchtet nicht. 4: glüht deutlich (mit Bloom).")]
        public float glow = 3f;

        [Setting("Dauer")]
        [Hint("So lange dauert das Blenden (Spielzeit). Bei „Aufblitzen“ die Zeit bis zurück.", unit: "s", range: "0,1 bis 2", edges: "0,15: wirkt wie ein Treffer. 0,6: wie ein Puls. 0: springt.")]
        public float seconds = 0.5f;

        [Setting("Verlauf")]
        [Hint("Wie die Änderung über die Dauer läuft.", edges: "Beim Aufblitzen ist „Langsam aufhören“ meist am schönsten.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn die Farbe angekommen ist (beim Blitz: wenn er abgeklungen ist).")] [Output("Fertig")] [NonSerialized] public Output done;

        static readonly MaterialPropertyBlock s_Block = new MaterialPropertyBlock();

        // Die Farbe, zu der ein Blitz zurückkehrt: das Ziel des letzten „Ändern“ (oder die Startfarbe).
        static readonly Dictionary<(Renderer, string), Color> s_Rest = new Dictionary<(Renderer, string), Color>();

        [Hint("Blendet zur eingestellten Farbe.")] [Input("Ändern")] public void Change(Signal s) { var to = Target(); s_Rest[Key()] = to; Go(Current(), to, "geändert", s); }
        [Hint("Blendet zur Farbe vom Spielstart zurück.")] [Input("Zurück")] public void Restore(Signal s) { var to = Initial(); s_Rest[Key()] = to; Go(Current(), to, "zurück", s); }
        [Hint("Springt sofort auf die Farbe und blendet über die Dauer zurück.")] [Input("Aufblitzen")]
        public void Flash(Signal s)
        {
            var rest = s_Rest.TryGetValue(Key(), out var c) ? c : Initial();
            Go(Target(), rest, "aufgeblitzt", s);                     // sofort Blitzfarbe, dann zurück
        }

        (Renderer, string) Key() => (target, Property(target));

        string Property(Renderer r)
        {
            var m = r.sharedMaterial;
            if (channel == Channel.Emission) return "_EmissionColor";
            return m && !m.HasProperty("_BaseColor") && m.HasProperty("_Color") ? "_Color" : "_BaseColor";
        }

        Color Target()
        {
            if (channel != Channel.Emission) return color;
            var c = color * Mathf.Max(0f, glow); c.a = 1f;            // Leuchtstärke macht heller, nicht „deckender“
            return c;
        }

        // Farbe beim Spielstart: aus dem Material, bevor der Kit etwas geändert hat.
        Color Initial()
        {
            var r = target; string prop = Property(r);
            return Blend.Original((r, prop), () => Read(r, prop, fromMaterial: true));
        }

        Color Current() { Initial(); return Read(target, Property(target), fromMaterial: false); }

        static Color Read(Renderer r, string prop, bool fromMaterial)
        {
            if (!fromMaterial && r.HasPropertyBlock())
            {
                r.GetPropertyBlock(s_Block);
                if (s_Block.HasColor(prop)) return s_Block.GetColor(prop);
            }
            var m = r.sharedMaterial;
            if (!m || !m.HasProperty(prop)) return prop == "_EmissionColor" ? Color.black : Color.white;
            if (prop == "_EmissionColor" && !m.IsKeywordEnabled("_EMISSION")) return Color.black;   // Leuchten war aus
            return m.GetColor(prop);
        }

        void Go(Color from, Color to, string what, Signal s)
        {
            var r = target; string prop = Property(r);
            if (prop == "_EmissionColor") EnableEmission(r);
            PruneRest();
            var key = (r, prop);
            int ticket = Blend.Claim(key, this);
            StartWork(Wrap(Blend.Run(seconds, ease, false, k => Write(r, prop, Blend.Lerp(from, to, k)), key, ticket,
                                     t => Running($"{what}: {t * 100f:0} %", t)), key, r, ticket, what, s));
        }

        IEnumerator Wrap(IEnumerator run, (Renderer, string) key, Renderer r, int ticket, string what, Signal s)
        {
            while (run.MoveNext()) yield return run.Current;
            if (!r) { Fail("◆ Objekt wurde gelöscht"); yield break; }
            if (!Blend.Holds(key, ticket)) { Blocked("eine neuere Farbänderung hat übernommen"); yield break; }
            Blend.Let(key, ticket);
            Done($"✓ {what}");
            done.Fire(s);
        }

        static void Write(Renderer r, string prop, Color c)
        {
            if (!r) return;
            r.GetPropertyBlock(s_Block);
            s_Block.SetColor(prop, c);
            r.SetPropertyBlock(s_Block);
        }

        // Ein Material ohne eingeschaltetes Leuchten (Emission) leuchtet nicht, egal welche Farbe es bekommt.
        // Dann bekommt nur dieses Objekt eine eigene Kopie mit Leuchten an (EmissionCopy räumt sie mit dem Objekt weg),
        // das Asset bleibt unberührt.
        static void EnableEmission(Renderer r)
        {
            var m = r.sharedMaterial;
            if (!m || m.IsKeywordEnabled("_EMISSION") || !m.HasProperty("_EmissionColor")) return;
            m = EmissionCopy.For(r);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.GetColor("_EmissionColor").maxColorComponent <= 0f) m.SetColor("_EmissionColor", Color.black);
        }

        /// <summary>Forgets remembered colours of objects that are gone.</summary>
        static void PruneRest()
        {
            if (s_Rest.Count == 0) return;
            var stale = new List<(Renderer, string)>();
            foreach (var k in s_Rest.Keys) if (!k.Item1) stale.Add(k);
            foreach (var k in stale) s_Rest.Remove(k);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            s_Rest.Clear();
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= Unloaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += Unloaded;
        }

        static void Unloaded(UnityEngine.SceneManagement.Scene _) => PruneRest();

        public override string Readout() => target ? $"#{ColorUtility.ToHtmlStringRGB(Read(target, Property(target), false))}" : null;

        public override void Validate(List<string> problems)
        {
            if (seconds < 0f) problems.Add("Dauer darf nicht negativ sein");
            if (target != null && target.sharedMaterial != null && channel == Channel.Base
                && !target.sharedMaterial.HasProperty("_BaseColor") && !target.sharedMaterial.HasProperty("_Color"))
                problems.Add($"Das Material von „{target.name}“ hat keine Grundfarbe, die sich ändern ließe");
        }
    }
}
