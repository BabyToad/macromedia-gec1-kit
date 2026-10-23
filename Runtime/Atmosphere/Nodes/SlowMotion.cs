using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Zeitlupe: bremst das ganze Spiel weich ab, hält das Tempo eine Weile und beschleunigt wieder. Für den einen
    // Moment, der sitzen soll: der letzte Sprung, eine Tür, die aufschwingt. Läuft über KitTime: mit Anhalten oder
    // einem Pause-Menü zugleich gilt das langsamste Tempo, und nach dem Ende kommt das Tempo von vorher zurück.
    [Serializable]
    [NodeInfo("Zeitlupe", KitGroups.Atmosphaere, "Spiel weich verlangsamen und wieder beschleunigen")]
    public class SlowMotion : KitNode
    {
        [Setting("Tempo")]
        [Hint("Wie schnell das Spiel in der Zeitlupe läuft (1 = normal).", unit: "×", range: "0,1 bis 0,5", edges: "0,3: deutlich langsam. 0,1: fast still. Ganz still: Knoten „Anhalten“.")]
        public float timeScale = 0.3f;

        [Setting("Abbremsen")]
        [Hint("So lange dauert der Übergang vom normalen Tempo in die Zeitlupe (echte Zeit).", unit: "s", range: "0,05 bis 0,5", edges: "0: sofort.")]
        public float rampIn = 0.15f;

        [Setting("Halten")]
        [Hint("So lange bleibt die Zeitlupe (echte Zeit).", unit: "s", range: "0,5 bis 3", edges: "0: bis „Beenden“ kommt.")]
        public float hold = 1.5f;

        [Setting("Beschleunigen")]
        [Hint("So lange dauert der Weg zurück zum normalen Tempo (echte Zeit).", unit: "s", range: "0,3 bis 1,5", edges: "Etwas länger als das Abbremsen wirkt natürlich.")]
        public float rampOut = 0.6f;

        [Setting("Verlauf")]
        [Hint("Wie Abbremsen und Beschleunigen verlaufen.", edges: "„Weich“ passt fast immer.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn das Tempo der Zeitlupe erreicht ist.")] [Output("Langsam")] [NonSerialized] public Output slowed;
        [Hint("Kommt, wenn das Spiel wieder im normalen Tempo läuft.")] [Output("Wieder normal")] [NonSerialized] public Output normal;

        Coroutine m_Work;
        float m_Current = 1f;

        [Hint("Bremst das Spiel auf das Tempo ab. Erneut: beginnt von vorn.")] [Input("Starten", UsesRefs = false)]
        public void Begin(Signal s)
        {
            if (m_Work != null) StopWork(m_Work);
            m_Work = StartWork(Run(s));
        }

        [Hint("Beschleunigt sofort wieder auf normal (für Halten = 0).")] [Input("Beenden", UsesRefs = false)]
        public void End(Signal s)
        {
            if (!KitTime.IsHeld(this)) { Done("war nicht in Zeitlupe"); return; }
            if (m_Work != null) StopWork(m_Work);
            m_Work = StartWork(Out(s));
        }

        IEnumerator Run(Signal s)
        {
            float target = Mathf.Clamp(timeScale, 0.01f, 1f);
            for (var r = Ramp(m_Current, target, rampIn, "bremst ab"); r.MoveNext();) yield return r.Current;   // eingebettet: Stopp hält alles an
            slowed.Fire(s);
            if (hold <= 0f) { Running($"Zeitlupe ({target:0.##}×) bis „Beenden“"); m_Work = null; yield break; }
            float end = Time.unscaledTime + hold;
            while (Time.unscaledTime < end)
            {
                Running($"Zeitlupe {target:0.##}×: noch {end - Time.unscaledTime:0.0} s", 1f - (end - Time.unscaledTime) / hold);
                yield return null;
            }
            for (var r = Out(s); r.MoveNext();) yield return r.Current;
        }

        IEnumerator Out(Signal s)
        {
            for (var r = Ramp(m_Current, 1f, rampOut, "beschleunigt"); r.MoveNext();) yield return r.Current;
            KitTime.Release(this);
            m_Current = 1f;
            m_Work = null;
            Done("✓ wieder normal");
            normal.Fire(s);
        }

        IEnumerator Ramp(float from, float to, float seconds, string what)
        {
            float d = Easing.Duration(ease, seconds), start = Time.unscaledTime;
            while (d > 0f && Time.unscaledTime - start < d)
            {
                float t = (Time.unscaledTime - start) / d;
                Set(Mathf.LerpUnclamped(from, to, Easing.Evaluate(ease, t)));
                Running($"{what}: {m_Current:0.00}×", t);
                yield return null;
            }
            Set(to);
        }

        void Set(float scale)
        {
            m_Current = Mathf.Clamp(scale, 0.01f, 1f);
            KitTime.Hold(this, m_Current);
        }

        public override void OnStop()
        {
            m_Work = null;
            m_Current = 1f;
            KitTime.Release(this);   // nie in Zeitlupe hängen bleiben
        }

        public override string Readout() => KitTime.IsHeld(this) ? $"Tempo {m_Current:0.00}×" : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (timeScale <= 0f || timeScale > 1f) problems.Add("Tempo muss zwischen 0,01 und 1 liegen (für Stillstand: Knoten „Anhalten“)");
            if (rampIn < 0f || rampOut < 0f || hold < 0f) problems.Add("Zeiten dürfen nicht negativ sein");
        }
    }
}
