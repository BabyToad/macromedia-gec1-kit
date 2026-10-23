using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Musik-Zone: while the player is inside, music changes: a layer comes in (drums in the arena), the whole
    /// piece crossfades to another one (calm forest → tense cave), or the atmo of a room takes over. Leaving the
    /// zone crossfades back. Put it on a trigger box; it can share the box with other zones.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Musik-Zone")]
    public class MusicZone : AtmosphereZone
    {
        [Serializable]
        public class Entry
        {
            [Hint("Welche Musik oder Atmo sich ändert: ein Objekt mit der Komponente „Musik und Atmo“.", label: "Musik")]
            public Music music;
            [Hint("Name einer Ebene dieser Musik, z. B. „Trommeln“.", edges: "Leer: die ganze Musik.", label: "Ebene")]
            public string layer = "";
            [Hint("Lautstärke in der Zone. Für „Trommeln kommen dazu“: Ebene „Trommeln“ auf 1.", range: "0 bis 1", edges: "0: stumm. 1: voll hörbar.", label: "Lautstärke")]
            [Range(0f, 1f)] public float level = 1f;
        }

        [Hint("Was sich in der Zone ändert. Mehrere Einträge laufen gleichzeitig: z. B. Höhlen-Atmo auf 1 und Wald-Musik auf 0.", label: "Änderungen")]
        public List<Entry> changes = new List<Entry> { new Entry() };

        [Hint("An: alle anderen Musiken und Atmos werden in der Zone stumm. Praktisch für einen Raum mit ganz eigener Stimmung.", label: "Andere stumm")]
        public bool muteOthers = false;

        protected override Color GizmoColor => new Color(0.95f, 0.55f, 0.85f);
        protected override string GizmoLabel
        {
            get
            {
                var e = changes.Count > 0 ? changes[0] : null;
                return e != null && e.music ? $"♪ {e.music.name}{(string.IsNullOrEmpty(e.layer) ? "" : " · " + e.layer)}" : "♪ Musik-Zone";
            }
        }

        protected override void Reset()
        {
            base.Reset();
            var m = FindAnyObjectByType<Music>();
            if (m && changes.Count == 1 && !changes[0].music) changes[0].music = m;
        }

        // Every Musik this zone changed, also ones switched off since: leaving must take back exactly these wishes.
        readonly HashSet<Music> m_Touched = new HashSet<Music>();

        protected override void Entered()
        {
            if (muteOthers)
                foreach (var m in Music.Active.ToArray())
                    if (!changes.Exists(e => e.music == m)) { m.Wish(this, null, 0f, seconds, ease); m_Touched.Add(m); }
            foreach (var e in changes)
            {
                if (!e.music) continue;
                if (e.music.Wish(this, e.layer, e.level, seconds, ease)) m_Touched.Add(e.music);
                else Debug.LogWarning($"Musik-Zone „{name}“: „{e.music.name}“ hat keine Ebene „{e.layer}“. Vorhanden: {string.Join(", ", e.music.layers.ConvertAll(l => l.name))}.", this);
            }
        }

        protected override void Left()
        {
            foreach (var m in m_Touched) if (m) m.Unwish(this, seconds, ease);
            m_Touched.Clear();
        }
    }
}
