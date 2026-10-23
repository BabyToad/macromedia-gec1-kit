using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Kit
{
    /// <summary>
    /// Musik / Atmo: a looping piece made of layers (Ebenen) that start together and stay in sync: a pad, drums,
    /// a melody; or wind, birds, a far-off engine. Zones and nodes turn layers up and down with a crossfade, so the
    /// music reacts to where the player is without ever restarting. Two Musik objects (a calm and a tense one)
    /// crossfade the same way.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Musik und Atmo")]
    [DisallowMultipleComponent]
    public class Music : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            [Hint("Name der Ebene, z. B. „Trommeln“. Zonen und Knoten finden die Ebene über diesen Namen.", label: "Name")]
            public string name = "Ebene";
            [Hint("Der Audio-Clip dieser Ebene.", edges: "Alle Ebenen gleich lang (gleiches Tempo, gleiche Takte), dann bleiben sie im Takt.", label: "Clip")]
            public AudioClip clip;
            [Hint("Lautstärke dieser Ebene im Verhältnis zu den anderen.", range: "0 bis 1", edges: "1: voll. 0,5: halb so laut.", label: "Lautstärke")]
            [Range(0f, 1f)] public float volume = 1f;
            [Hint("An: von Anfang an zu hören. Aus: spielt stumm mit und wird erst von einer Zone oder einem Knoten hörbar.", label: "Zu Beginn an")]
            public bool startOn = true;
        }

        [Hint("Die Ebenen. Sie starten gemeinsam und laufen in Schleife, darum bleiben sie im Takt, auch wenn eine erst später hörbar wird.", label: "Ebenen")]
        public List<Layer> layers = new List<Layer> { new Layer { name = "Grundton" } };

        [Hint("Gesamtlautstärke.", range: "0,5 bis 0,8", edges: "Nicht ganz auf 1: Geräusche brauchen darüber noch Platz.", label: "Lautstärke")]
        [Range(0f, 1f)] public float volume = 0.7f;

        [Hint("An: ist beim Start zu hören. Aus: läuft stumm mit, bis eine Musik-Zone oder ein Knoten „Musik“ sie hörbar macht.", label: "Beim Start hörbar")]
        public bool audibleAtStart = true;

        [Hint("So lange blendet die Musik beim Start ein.", unit: "s", range: "0 bis 5", edges: "0: sofort da.", label: "Einblenden")]
        public float fadeInAtStart = 2f;

        [Hint("Wie sehr der Klang von diesem Ort kommt.", range: "0 bis 1", edges: "0: überall gleich laut (Musik). 1: wird mit Abstand leiser (Atmo: Wasserfall, Maschine, Lagerfeuer).", label: "Raumklang")]
        [Range(0f, 1f)] public float spatial = 0f;

        [Hint("Nur bei Raumklang: bis zu dieser Entfernung ist der Klang zu hören.", unit: "m", range: "5 bis 60", label: "Reichweite")]
        public float range = 25f;

        [Hint("Optional: Audio-Mixer-Gruppe, z. B. „Musik“, damit ein Lautstärke-Regler im Menü alle Musik zugleich steuert.", edges: "Leer: direkt auf die Lautsprecher.", label: "Mixer-Gruppe")]
        public AudioMixerGroup output;

        // ---- runtime --------------------------------------------------------------------------------
        sealed class Channel
        {
            public Wishes<float> wishes;
            public float level, from, to, start, duration;
            public Ease ease;
            public AudioSource source;   // null for the master channel
            public Layer layer;          // the Inspector entry this channel plays; reordering the list keeps the pairing
            public int revision;         // counts every change of target: a waiter knows whether "its" fade is still the one running
        }

        Channel m_Master;
        readonly List<Channel> m_Layers = new List<Channel>();
        bool m_Started;

        /// <summary>All Musik objects that are running, for zones that fade "the others" out.</summary>
        public static readonly List<Music> Active = new List<Music>();

        /// <summary>Current audible level of the whole piece, 0..1 (before <see cref="volume"/>).</summary>
        public float Level => m_Master?.level ?? 0f;
        /// <summary>Current level of one layer, 0..1 (before its own volume), or -1 if there is no such layer.</summary>
        public float LayerLevel(string layer) => string.IsNullOrEmpty(layer) ? -1f : ChannelOf(layer)?.level ?? -1f;
        public AudioSource SourceOf(string layer) => string.IsNullOrEmpty(layer) ? null : ChannelOf(layer)?.source;

        public int IndexOf(string layer) => layers.FindIndex(l => string.Equals(l.name, layer, StringComparison.OrdinalIgnoreCase));

        float m_PausedAt = -1f;   // seconds into the piece when switched off, so all layers resume at the same musical point

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            if (!m_Started || m_PausedAt < 0) return;
            // Switched on again: restart every layer from where it stopped, on one shared tick, so they stay in sync.
            double at = AudioSettings.dspTime + 0.05;
            foreach (var ch in m_Layers)
            {
                if (!ch.source || !ch.source.clip) continue;
                ch.source.time = m_PausedAt % ch.source.clip.length;   // in seconds: also right for clips with different sample rates
                ch.source.PlayScheduled(at);
            }
            m_PausedAt = -1f;
        }

        void OnDisable()
        {
            Active.Remove(this);
            if (!m_Started) return;
            // Switched off: the layers fall silent (a component that is off makes no sound), and remember where they were.
            m_PausedAt = 0f;
            bool found = false;
            foreach (var ch in m_Layers)
            {
                if (!ch.source) continue;
                if (ch.source.isPlaying && !found) { m_PausedAt = ch.source.time; found = true; }
                ch.source.Stop();
            }
        }

        void Start() => Begin();

        void Begin()
        {
            if (m_Started) return;
            m_Started = true;
            m_Master = new Channel { wishes = new Wishes<float>(audibleAtStart ? 1f : 0f) };
            Retarget(m_Master, fadeInAtStart, Ease.Smooth, startLevel: 0f);
            double at = AudioSettings.dspTime + 0.1;                   // gemeinsamer Start: alle Ebenen im selben Takt
            foreach (var l in layers)
            {
                var go = new GameObject("Ebene: " + l.name);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.clip = l.clip; src.loop = true; src.playOnAwake = false;
                src.spatialBlend = spatial; src.maxDistance = Mathf.Max(1f, range); src.rolloffMode = AudioRolloffMode.Linear;
                src.outputAudioMixerGroup = output;
                src.volume = 0f;
                var ch = new Channel { wishes = new Wishes<float>(l.startOn ? 1f : 0f), source = src, layer = l };
                ch.level = ch.wishes.Current; ch.to = ch.level; ch.duration = 0f;
                m_Layers.Add(ch);
                if (l.clip) src.PlayScheduled(at);
            }
            Apply();
        }

        /// <summary>Is <paramref name="owner"/>'s wish the one that counts on this layer (or the whole piece) right now?</summary>
        public bool IsNewest(object owner, string layer)
        {
            var ch = ChannelOf(layer);
            return ch != null && ch.wishes.IsNewest(owner);
        }

        /// <summary>Has the layer (or the whole piece) arrived at its level, no fade running?</summary>
        public bool Settled(string layer)
        {
            var ch = ChannelOf(layer);
            return ch == null || ch.duration <= 0f;
        }

        /// <summary>Counts changes of this layer's (or the whole piece's) target; -1 if there is no such layer.</summary>
        public int Revision(string layer) => ChannelOf(layer)?.revision ?? -1;

        /// <summary>Whose wish counts on this layer (or the whole piece) right now; null = nobody's, the base level.</summary>
        public object NewestOwner(string layer) => ChannelOf(layer)?.wishes.NewestOwner;

        /// <summary>
        /// Wish for a level (0..1) of one layer, or of the whole piece when <paramref name="layer"/> is empty. The
        /// newest wish wins; <see cref="Unwish"/> falls back to the one before. False if there is no such layer.
        /// </summary>
        public bool Wish(object owner, string layer, float level, float seconds, Ease ease = Ease.Smooth)
        {
            Begin();
            var ch = ChannelOf(layer);
            if (ch == null) return false;
            ch.wishes.Set(owner, Mathf.Clamp01(level));
            Retarget(ch, seconds, ease);
            return true;
        }

        /// <summary>Takes back every wish of this owner, on all layers.</summary>
        public void Unwish(object owner, float seconds, Ease ease = Ease.Smooth)
        {
            if (!m_Started) return;
            if (m_Master.wishes.Remove(owner)) Retarget(m_Master, seconds, ease);
            foreach (var ch in m_Layers) if (ch.wishes.Remove(owner)) Retarget(ch, seconds, ease);
        }

        Channel ChannelOf(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return m_Master;
            foreach (var ch in m_Layers)
                if (layers.Contains(ch.layer) && string.Equals(ch.layer.name, layer, StringComparison.OrdinalIgnoreCase)) return ch;
            return null;
        }

        void Retarget(Channel ch, float seconds, Ease ease, float? startLevel = null)
        {
            if (startLevel.HasValue) ch.level = startLevel.Value;
            ch.from = ch.level; ch.to = ch.wishes.Current; ch.ease = ease; ch.revision++;
            ch.start = Time.unscaledTime; ch.duration = Easing.Duration(ease, seconds);
            if (ch.duration <= 0f) ch.level = ch.to;
        }

        static void Step(Channel ch)
        {
            if (ch.duration <= 0f) { ch.level = ch.to; return; }
            float t = (Time.unscaledTime - ch.start) / ch.duration;
            ch.level = t >= 1f ? ch.to : Mathf.Clamp01(Mathf.LerpUnclamped(ch.from, ch.to, Easing.Evaluate(ch.ease, t)));
            if (t >= 1f) ch.duration = 0f;
        }

        void Update()
        {
            if (!m_Started) return;
            Step(m_Master);
            foreach (var ch in m_Layers) Step(ch);
            Apply();
        }

        void Apply()
        {
            foreach (var ch in m_Layers)
            {
                if (!ch.source) continue;
                // A layer removed from the list during Play falls silent; one added during Play starts with the next Play.
                if (!layers.Contains(ch.layer)) { ch.source.volume = 0f; continue; }
                // Equal-power: two layers crossfading at 50 % each sound as loud as one at full, no dip in the middle.
                float shaped = Mathf.Sin(ch.level * m_Master.level * Mathf.PI * 0.5f);
                ch.source.volume = shaped * ch.layer.volume * volume;
            }
        }

        void OnValidate()
        {
            fadeInAtStart = Mathf.Max(0f, fadeInAtStart);
            range = Mathf.Max(1f, range);
            if (m_Started && layers.Exists(l => !m_Layers.Exists(ch => ch.layer == l)))
                Debug.LogWarning($"Musik „{name}“: eine Ebene, die während Play dazukommt, spielt erst beim nächsten Start von Play. Entfernte Ebenen verstummen sofort; Lautstärken und Namen gelten sofort.", this);
            if (m_Started) foreach (var ch in m_Layers) if (ch.source) { ch.source.spatialBlend = spatial; ch.source.maxDistance = range; ch.source.outputAudioMixerGroup = output; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => Active.Clear();
    }
}
