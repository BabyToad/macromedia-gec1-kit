using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Kurz aktivieren: schaltet etwas für ein paar Sekunden ein und danach wieder aus. Eine Zone, die nur kurz
    // da ist (ein Windstoß, eine Wasserfontäne, ein Lichtkegel), ein Licht, das aufblitzt, ein Hinweis, der kurz erscheint.
    // Ein GameObject wird ein- und ausgeschaltet; eine Komponente (Collider, Licht, Renderer, Skript) nur sie selbst.
    // Kommt das Ereignis wieder, während es noch an ist, beginnt die Zeit von vorn (oder es wird ignoriert).
    [Serializable]
    [NodeInfo("Kurz aktivieren", "Aktion", "Für ein paar Sekunden einschalten")]
    public class BrieflyActivate : KitNode
    {
        public enum Again { [InspectorName("Zeit von vorn")] Verlängern, [InspectorName("ignorieren")] Ignorieren }

        [Hint("Was kurz an sein soll: ein GameObject (z.B. eine Zone) oder eine einzelne Komponente (z.B. nur der Collider oder das Licht).")]
        [Ref("Objekt")] public UnityEngine.Object target;
        [Hint("Wie lange es an bleibt. Läuft in Spielzeit: Zeitlupe verlängert es, angehaltene Zeit hält es an.", unit: "s", range: "0,1 bis 5", edges: "0 oder weniger: geht nicht (Fehler).")]
        [DataIn("Sekunden")] public float seconds = 0.5f;
        [Hint("Umgekehrt: kurz AUS statt kurz an, z.B. ein Hindernis, das kurz verschwindet.")]
        [Setting("Umgekehrt")] public bool inverted = false;
        [Hint("Was passiert, wenn das Ereignis wieder kommt, während es noch an ist.")]
        [Setting("Erneut ausgelöst")] public Again again = Again.Verlängern;

        [Hint("Feuert, wenn es eingeschaltet wurde (bei Umgekehrt: ausgeschaltet).")]
        [Output("Begonnen")] [NonSerialized] public Output began;
        [Hint("Feuert, wenn die Zeit um ist und es wieder im Ausgangszustand ist.")]
        [Output("Vorbei")] [NonSerialized] public Output ended;

        [Hint("Ja, solange die Zeit läuft.")]
        [DataOut("Läuft")] public bool Active => m_Work != null;

        [NonSerialized] Coroutine m_Work;
        [NonSerialized] UnityEngine.Object m_On;
        [NonSerialized] float m_Until;
        [NonSerialized] int m_Op;               // which run is current; an older coroutine that wakes up just ends

        // The ◆ field is a plain Object: a GameObject, or any component it carries.
        static bool Get(UnityEngine.Object o) =>
            o is GameObject go ? go.activeSelf : o is Behaviour b ? b.enabled : o is Collider c ? c.enabled : o is Renderer r && r.enabled;

        static void Set(UnityEngine.Object o, bool on)
        {
            switch (o)
            {
                case GameObject go: go.SetActive(on); break;
                case Behaviour b: b.enabled = on; break;
                case Collider c: c.enabled = on; break;
                case Renderer r: r.enabled = on; break;
            }
        }

        static bool Switchable(UnityEngine.Object o) => o is GameObject || o is Behaviour || o is Collider || o is Renderer;

        static string NameOf(UnityEngine.Object o) => o is Component c ? $"{c.name} › {c.GetType().Name}" : o.name;

        [Hint("Einschalten (bei Umgekehrt: ausschalten) und nach Sekunden zurück.")]
        [Input("Start")]
        public void Begin(Signal s)
        {
            if (!Switchable(target)) { Fail($"„{target.name}“ lässt sich nicht ein- und ausschalten (GameObject, Collider, Renderer oder Skript verbinden)"); return; }
            if (seconds <= 0f) { Fail($"Sekunden = {KitValue.FormatNumber(seconds)}: es muss länger als 0 sein"); return; }
            bool on = !inverted;
            if (m_Work != null && m_On == target)
            {
                if (again == Again.Ignorieren) { Done("läuft schon – ignoriert"); return; }
                m_Until = Time.time + seconds;               // the running timer just sees the new end
                Running($"{NameOf(target)} {(on ? "an" : "aus")}, noch {KitValue.FormatNumber(seconds)} s", 0f);
                return;
            }
            if (m_Work != null) { StopWork(m_Work); Finish(); }   // a different target came with this event: hand the old one back first
            int op = ++m_Op;
            m_On = target;
            Set(target, on);
            m_Until = Time.time + seconds;
            m_Work = StartWork(Hold(op, target, s));
            began.Fire(s);
        }

        [Hint("Sofort beenden: zurück in den Ausgangszustand, „Vorbei“ feuert.")]
        [Input("Abbrechen", UsesRefs = false)]
        public void Cancel(Signal s)
        {
            if (m_Work == null) { Done("lief gerade nicht"); return; }
            StopWork(m_Work);
            Finish();
            Done("✓ abgebrochen");
            ended.Fire(s);
        }

        // One run of „an, warten, aus“. Its own object and event; it ends quietly if a newer run took over.
        IEnumerator Hold(int op, UnityEngine.Object on, Signal s)
        {
            float total = Mathf.Max(0.0001f, m_Until - Time.time);
            while (op == m_Op && Time.time < m_Until)
            {
                if (on == null) { m_Work = null; m_On = null; Fail("◆ Objekt wurde gelöscht, während es an war"); yield break; }
                float left = m_Until - Time.time;
                Running($"{NameOf(on)} {(!inverted ? "an" : "aus")}, noch {left:0.0} s", 1f - left / total);
                yield return null;
            }
            if (op != m_Op) yield break;
            Finish();
            Done($"✓ {(!inverted ? "wieder aus" : "wieder an")}");
            ended.Fire(s);
        }

        void Finish()
        {
            if (m_On != null) Set(m_On, inverted);
            m_Work = null; m_On = null; m_Op++;
        }

        // The Logik stopping mid-way must not leave the zone on for ever.
        public override void OnStop() { if (m_On != null) Set(m_On, inverted); m_Work = null; m_On = null; m_Op++; }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (seconds <= 0f) problems.Add("Sekunden muss größer als 0 sein");
            if (target != null && !Switchable(target)) problems.Add($"„{target.name}“ lässt sich nicht ein- und ausschalten");
        }
    }
}
