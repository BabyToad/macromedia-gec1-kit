using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Variable: ein Wert mit Namen, den sich das Spiel merkt – eine Zahl (Münzen, Lebenspunkte), ein Ja-Nein
    // (Tür offen) oder ein Text (Name). Wer ihn teilt, sagt der Bereich:
    //   Logik – nur diese Logik (jede Kopie eines Prefabs hat ihren eigenen Wert),
    //   Szene – alle in dieser Szene mit demselben Namen,
    //   Spiel – alle, auch nach dem Laden einer anderen Szene.
    // Ist ◆ Objekt verbunden, gehört der Wert diesem Objekt (seiner Komponente „Variable“ mit demselben Namen).
    // So kann eine Zone die Lebenspunkte dessen ändern, der hineinläuft (◆ Wer).
    // Ändert jemand anderes den Wert, melden alle Knoten, die ihn benutzen, „Geändert“ – mit einem neuen Ereignis.
    [Serializable]
    public abstract class VariableNode : KitNode
    {
        [Hint("Name der Variable, z.B. „Münzen“. Knoten mit demselben Namen und Bereich teilen sich den Wert.")]
        [Setting("Name")] public string label = "Variable";

        [Hint("Logik: nur diese Logik (jede Prefab-Kopie hat ihren eigenen Wert). Szene: alle in dieser Szene. Spiel: alle, auch nach dem Laden einer anderen Szene. Ist ◆ Objekt verbunden, gilt die Variable am Objekt.")]
        [Setting("Bereich")] public Scope scope = Scope.Logik;

        [Hint("Optional. Verbunden: der Wert gehört diesem Objekt (Komponente „Variable“ mit demselben Namen), z.B. ◆ Wer aus einer Zone. Leer: der Wert gehört dem Bereich.")]
        [Ref("Objekt", Optional = true)] public GameObject target;

        [Hint("Feuert bei jeder echten Änderung – egal, wer geändert hat. Ein Setzen auf denselben Wert ist keine Änderung.")]
        [Output("Geändert")] [NonSerialized] public Output changed;

        // How ◆ Objekt is wired, worked out once per start (so an idle node costs nothing per frame).
        [NonSerialized] KitRefs.Wiring m_Wiring;
        [NonSerialized] string m_Slot;
        [NonSerialized] KitValue m_Fixed;        // the value when it does not depend on the event (subscribed)
        [NonSerialized] string m_FixedProblem;
        [NonSerialized] bool m_ByName;           // found as "the one Variable component with this name in the scene"
        [NonSerialized] int m_IndexVersion;      // KitVariableIndex.Version when that was decided
        [NonSerialized] int m_RetryFrame;
        [NonSerialized] KitValue m_Last;         // the value the last event used (for ● Wert)
        [NonSerialized] bool m_Lost;
        [NonSerialized] int m_Run;               // goes up on every start and stop: a reaction queued in an older run is dropped
        [NonSerialized] bool m_Firing;           // while outputs fire, ● Wert is the value of that change
        [NonSerialized] object m_FiringNow;

        protected abstract ValueKind Kind { get; }
        protected abstract void FillStart(ref KitValues.Declaration d);
        /// <summary>Fire the outputs that belong to one change, deciding everything from the change's own values.</summary>
        protected abstract void FireOutputs(in KitChange c, Signal s);

        /// <summary>The value ● Wert reads right now: the change being fired, else the live value.</summary>
        protected object Shown(out bool known)
        {
            known = true;
            if (m_Firing) return m_FiringNow;
            var v = Current;
            known = v != null;
            return v?.Boxed;
        }

        protected KitValue Current
        {
            get
            {
                if (m_Last != null) return m_Last;
                if (m_Fixed == null && Runner != null && Application.isPlaying && m_Wiring != KitRefs.Wiring.FromEvent && m_FixedProblem == null) ResolveFixed();
                return m_Fixed;
            }
        }

        public override void OnStart()
        {
            m_Run++;
            m_Fixed = null; m_Last = null; m_FixedProblem = null; m_Lost = false; m_ByName = false; m_RetryFrame = 0; m_Firing = false;
            m_Wiring = KitRefs.Of(this, nameof(target), out m_Slot);
            // Resolve and listen now, before any Start event of this or another Logik can change the value.
            if (m_Wiring != KitRefs.Wiring.FromEvent) ResolveFixed();
        }

        public override void OnStop()
        {
            m_Run++;
            Unsubscribe();
            m_Last = null;
            KitValues.ReleaseLogik(Runner);
        }

        void Unsubscribe()
        {
            if (m_Fixed != null) m_Fixed.Changed -= OnChanged;
            m_Fixed = null;
        }

        public override void Tick()
        {
            if (m_Lost || m_Wiring == KitRefs.Wiring.FromEvent) return;
            if (m_Fixed == null)
            {
                // Not found yet (a Variable component that wakes up later, a name typed in Play): try again now and then.
                if (Time.frameCount < m_RetryFrame) return;
                m_FixedProblem = null;
                ResolveFixed();
                if (m_Fixed == null) m_RetryFrame = Time.frameCount + 30;
                return;
            }
            if (m_ByName && m_IndexVersion != KitVariableIndex.Version)
            {
                // A Variable with this name appeared, disappeared or was renamed: is it still the one?
                Unsubscribe(); m_Last = null; m_FixedProblem = null;
                ResolveFixed();
                return;
            }
            if (m_Wiring == KitRefs.Wiring.Fixed && target == null && KitRefs.Deleted(Runner, m_Slot))
            {
                m_Lost = true;
                Unsubscribe();
                Fail($"◆ Objekt wurde gelöscht: die Variable „{label}“ an ihm gibt es nicht mehr");
            }
        }

        void ResolveFixed()
        {
            m_Fixed = Find(target, out m_FixedProblem);
            if (m_Fixed != null) m_Fixed.Changed += OnChanged;
        }

        KitValues.Declaration Declare()
        {
            var d = new KitValues.Declaration { name = label, kind = Kind, scope = scope, declaredBy = $"„{title}“ in „{Runner.name}“" };
            FillStart(ref d);
            return d;
        }

        string KindProblem(KitVariable c) =>
            $"„{label}“ an „{c.name}“ ist eine {KitValues.KindName(c.Kind)}-Variable, dieser Knoten ist {KitValues.KindName(Kind)}: den Typ an der Komponente oder den Knoten ändern";

        KitValue FromComponent(KitVariable c, out string problem)
        {
            problem = null;
            if (c.Kind != Kind) { problem = KindProblem(c); return null; }
            if (c.Value == null) problem = c.Problem;
            return c.Value;
        }

        KitValue Find(GameObject owner, out string problem)
        {
            problem = null;
            m_ByName = false;
            if (m_Wiring != KitRefs.Wiring.None)
            {
                // Connected: the value must be on that object. Never fall back to the Bereich – that would quietly
                // change some other value instead of saying what is wrong.
                if (owner == null)
                {
                    problem = m_Wiring == KitRefs.Wiring.FromEvent ? $"◆ Objekt: das Ereignis bringt kein Objekt mit (oder es wurde inzwischen gelöscht)"
                            : KitRefs.Deleted(Runner, m_Slot) ? "◆ Objekt wurde gelöscht: ein anderes Objekt binden"
                            : "◆ Objekt ist verbunden, aber leer: ein Objekt binden oder die Verbindung lösen";
                    return null;
                }
                var c = KitVariableIndex.On(owner, label);
                if (c == null) { problem = $"„{owner.name}“ hat keine Variable „{label}“: Add Component › Variable an „{owner.name}“, Name „{label}“"; return null; }
                return FromComponent(c, out problem);
            }
            var scene = Runner.gameObject.scene;
            if (scope == Scope.Szene)
            {
                m_IndexVersion = KitVariableIndex.Version;
                int n = KitVariableIndex.InScene(scene, label, out var only);
                if (n > 1) { m_ByName = true; problem = $"„{label}“ gibt es an {n} Objekten in der Szene: ◆ Objekt verbinden, um eines zu wählen"; return null; }
                if (n == 1) { m_ByName = true; return FromComponent(only, out problem); }
                m_ByName = true;   // none yet: if one appears later, it is the one
            }
            return KitValues.Get(Declare(), Runner, scene, out problem);
        }

        /// <summary>The value for this event; fails the node (German reason) if there is none.</summary>
        protected KitValue Use()
        {
            KitValue v;
            string problem;
            if (m_Wiring == KitRefs.Wiring.FromEvent) v = Find(target, out problem);
            else if (m_Wiring == KitRefs.Wiring.Fixed && target == null)
            {
                // The bound object is gone (deleted since the last frame, or never bound): never write to what is left of it.
                Unsubscribe(); m_Last = null;
                v = null; problem = Find(null, out var why) == null ? why : null;
            }
            else
            {
                if (m_ByName && m_IndexVersion != KitVariableIndex.Version) { Unsubscribe(); m_FixedProblem = null; }
                if (m_Fixed == null) { m_FixedProblem = null; ResolveFixed(); }
                v = m_Fixed; problem = m_FixedProblem;
            }
            if (v == null) { Fail(problem ?? $"Variable „{label}“ nicht gefunden"); return null; }
            m_Last = v;
            return v;
        }

        void OnChanged(KitChange c)
        {
            if (c.Writer == this) return;                     // our own write fires in the event that caused it
            m_Last = c.Value;
            string from = c.Writer is KitNode n && n.Runner != null && n.Runner != Runner ? $" (von „{n.Runner.name}“)"
                        : c.Writer is Component comp && comp != null ? $" (von „{comp.name}“)" : "";
            Done($"{c.Value.Name}: {Format(c.Now)}{from}");
            Fire(c, new Signal());
        }

        /// <summary>
        /// This node's reaction to its own write: Done text and outputs, with the event that caused the write. Runs inside
        /// the ordered delivery of changes (see KitValue.SetNumber), so nobody's later change overtakes it.
        /// </summary>
        protected System.Action<KitChange> Own(Signal s, string note = null)
        {
            int run = m_Run;
            return c =>
            {
                if (run != m_Run) return;              // the Logik stopped (or restarted) since this write: its event is over
                Done($"{label}: " + (c.Old is bool ? Format(c.Now) : $"{Format(c.Old)} → {Format(c.Now)}") + (note != null ? $" ({note})" : ""));
                Fire(c, s);
            };
        }

        /// <summary>Fires the outputs of one change, with ● Wert showing that change's value while they run.</summary>
        protected void Fire(in KitChange c, Signal s)
        {
            bool wasFiring = m_Firing; object was = m_FiringNow;
            m_Firing = true; m_FiringNow = c.Now;
            try { FireOutputs(c, s); }
            finally { m_Firing = wasFiring; m_FiringNow = was; }
        }

        protected static string Format(object v) => v is float f ? KitValue.FormatNumber(f) : v is bool b ? JaNein(b) : $"„{v}“";

        /// <summary>Text for a write that changed nothing.</summary>
        protected void Unchanged(KitValue v, string why) => Done($"{v.Describe()} – {why}");

        public override void Validate(List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(label)) problems.Add("die Variable hat keinen Namen: unter Name einen eintragen, z.B. „Münzen“");
            // A fixed object must carry the variable now, not only in Play.
            if (target != null && (Runner == null || Runner.Graph == null || !Runner.Graph.IsEventRef(index, nameof(target), out _)))
            {
                bool found = false;
                foreach (var c in target.GetComponentsInChildren<KitVariable>(true))
                    if (c.ValueName == label) { found = true; if (c.Kind != Kind) problems.Add(KindProblem(c)); }
                if (!found) problems.Add($"„{target.name}“ hat keine Variable „{label}“: Add Component › Variable an „{target.name}“, Name „{label}“");
            }
        }

        public override string Readout()
        {
            var v = m_Last ?? m_Fixed;
            if (v != null) return v.Describe() + ScopeTag(v);
            return m_FixedProblem;
        }

        string ScopeTag(KitValue v) => target != null ? $" · an „{target.name}“" : v.Scope == Scope.Logik ? "" : " · " + KitValues.ScopeName(v.Scope);
    }

    // Zahl: zählt, rechnet, begrenzt. Münzen, Lebenspunkte, Zeit, Runde.
    // Mit Grenzen bleibt sie zwischen Minimum und Maximum; „Minimum erreicht“ feuert in dem Moment, in dem sie
    // dort ankommt (nicht noch einmal, solange sie dort bleibt). „Wert erreicht“ feuert, wenn sie den Zielwert
    // trifft oder überschreitet – aus beiden Richtungen.
    [Serializable]
    [NodeInfo("Zahl", "Werte", "Eine Zahl merken, ändern, prüfen")]
    public class NumberVariable : VariableNode
    {
        [Hint("Wert beim Start (bei jedem Play neu). Mit Grenzen wird er auf Minimum … Maximum begrenzt.")]
        [Setting("Startwert")] public float start = 0f;
        [Hint("An: die Zahl bleibt zwischen Minimum und Maximum.")]
        [Setting("Grenzen")] public bool limited = false;
        [Hint("Kleinster Wert (nur mit Grenzen), z.B. 0.")]
        [Setting("Minimum")] public float min = 0f;
        [Hint("Größter Wert (nur mit Grenzen), z.B. 10.")]
        [Setting("Maximum")] public float max = 10f;
        [Hint("„Wert erreicht“ feuert, sobald die Zahl diesen Wert trifft oder darüber (bzw. darunter) springt.")]
        [Setting("Zielwert")] public float goal = 10f;

        [Hint("Um so viel ändern Erhöhen und Verringern. Kann ein ● Draht liefern.", range: "meist 1", edges: "Das Vorzeichen zählt nicht: Verringern zieht immer ab.")]
        [DataIn("Schritt")] public float step = 1f;
        [Hint("Der Wert für „Setzen“. Kann ein ● Draht liefern (z.B. aus Rechnen oder Zufallszahl).")]
        [DataIn("Neuer Wert")] public float newValue = 0f;

        [Hint("Feuert, wenn die Zahl am Minimum ankommt (nur mit Grenzen). Z.B. Lebenspunkte bei 0.")]
        [Output("Minimum erreicht")] [NonSerialized] public Output reachedMin;
        [Hint("Feuert, wenn die Zahl am Maximum ankommt (nur mit Grenzen). Z.B. Tank voll.")]
        [Output("Maximum erreicht")] [NonSerialized] public Output reachedMax;
        [Hint("Feuert, wenn die Zahl den Zielwert erreicht.")]
        [Output("Wert erreicht")] [NonSerialized] public Output reachedTarget;

        [Hint("Der aktuelle Wert, für Vergleich, Rechnen, Wert anzeigen, Text setzen …")]
        [DataOut("Wert", EventLocal = true)] public float Value { get { var v = Shown(out bool known); return known && v is float f ? f : start; } }

        protected override ValueKind Kind => ValueKind.Zahl;
        protected override void FillStart(ref KitValues.Declaration d) { d.startNumber = start; d.limited = limited; d.min = min; d.max = max; }

        [Hint("Zahl + Schritt.")]
        [Input("Erhöhen")] public void Increase(Signal s) => Change(Math.Abs(step), s);
        [Hint("Zahl − Schritt.")]
        [Input("Verringern")] public void Decrease(Signal s) => Change(-Math.Abs(step), s);
        [Hint("Zahl = Neuer Wert.")]
        [Input("Setzen")] public void Set(Signal s) { var v = Use(); if (v != null) Write(v, newValue, s); }
        [Hint("Zurück auf den Startwert (meldet „Geändert“, wenn das eine Änderung ist).")]
        [Input("Zurücksetzen")]
        public void ResetValue(Signal s)
        {
            var v = Use(); if (v == null) return;
            if (!v.ResetToStart(this, Own(s, "zurückgesetzt"))) Unchanged(v, "war schon beim Startwert");
        }

        void Change(float delta, Signal s)
        {
            var v = Use();
            if (v != null) Write(v, v.Number + delta, s);
        }

        void Write(KitValue v, float to, Signal s)
        {
            if (v.SetNumber(to, this, Own(s))) return;
            if (v.Limited && (to < v.Min || to > v.Max)) Unchanged(v, to < v.Min ? "schon am Minimum" : "schon am Maximum");
            else Unchanged(v, "keine Änderung");
        }

        protected override void FireOutputs(in KitChange c, Signal s)
        {
            // Decide everything first, from this change alone: a listener on Geändert may change the value again.
            float old = (float)c.Old, now = (float)c.Now;
            var v = c.Value;
            bool atMin = v.Limited && now == v.Min && old != v.Min;
            bool atMax = v.Limited && now == v.Max && old != v.Max;
            bool atGoal = Reached(old, now, goal);
            changed.Fire(s);
            if (atMin) reachedMin.Fire(s);
            if (atMax) reachedMax.Fire(s);
            if (atGoal) reachedTarget.Fire(s);
        }

        /// <summary>The value got to the goal: landed on it or jumped over it, from either side.</summary>
        public static bool Reached(float old, float now, float goal) =>
            old != goal && (now == goal || (old < goal && now > goal) || (old > goal && now < goal));

        public override void Validate(List<string> problems)
        {
            base.Validate(problems);
            if (limited && max < min) problems.Add($"Maximum ({KitValue.FormatNumber(max)}) ist kleiner als Minimum ({KitValue.FormatNumber(min)}): die beiden tauschen");
        }
    }

    // Ja-Nein: merkt sich ein Ja oder Nein, z.B. „Schlüssel gefunden“ oder „Strom an“.
    [Serializable]
    [NodeInfo("Ja-Nein", "Werte", "Ja oder Nein merken")]
    public class FlagVariable : VariableNode
    {
        [Hint("Wert beim Start (bei jedem Play neu): angehakt = ja.")]
        [Setting("Startwert")] public bool startValue = false;

        [Hint("Der Wert für „Setzen“. Kann ein ● Draht liefern, z.B. das Ergebnis eines Vergleichs.")]
        [DataIn("Neuer Wert")] public bool newValue = true;

        [Hint("Feuert, wenn der Wert von nein zu ja wechselt.")]
        [Output("Wurde ja")] [NonSerialized] public Output becameTrue;
        [Hint("Feuert, wenn der Wert von ja zu nein wechselt.")]
        [Output("Wurde nein")] [NonSerialized] public Output becameFalse;

        [Hint("Der aktuelle Wert, z.B. als Bedingung für „Wenn“.")]
        [DataOut("Wert", EventLocal = true)] public bool Value { get { var v = Shown(out bool known); return known && v is bool b ? b : startValue; } }

        protected override ValueKind Kind => ValueKind.JaNein;
        protected override void FillStart(ref KitValues.Declaration d) => d.startFlag = startValue;

        [Hint("Wert = ja.")]
        [Input("Ja setzen")] public void SetTrue(Signal s) => Write(true, s);
        [Hint("Wert = nein.")]
        [Input("Nein setzen")] public void SetFalse(Signal s) => Write(false, s);
        [Hint("Aus ja wird nein, aus nein wird ja.")]
        [Input("Umschalten")] public void Toggle(Signal s) { var v = Use(); if (v != null) Write(v, !v.Flag, s); }
        [Hint("Wert = Neuer Wert.")]
        [Input("Setzen")] public void Set(Signal s) => Write(newValue, s);
        [Hint("Zurück auf den Startwert.")]
        [Input("Zurücksetzen")]
        public void ResetValue(Signal s)
        {
            var v = Use(); if (v == null) return;
            if (!v.ResetToStart(this, Own(s, "zurückgesetzt"))) Unchanged(v, "war schon beim Startwert");
        }

        void Write(bool to, Signal s) { var v = Use(); if (v != null) Write(v, to, s); }

        void Write(KitValue v, bool to, Signal s)
        {
            if (!v.SetFlag(to, this, Own(s))) Unchanged(v, $"war schon {JaNein(to)}");
        }

        protected override void FireOutputs(in KitChange c, Signal s)
        {
            bool now = (bool)c.Now;
            changed.Fire(s);
            (now ? becameTrue : becameFalse).Fire(s);
        }
    }

    // Text: merkt sich einen Text, z.B. den Namen der Figur oder das gewählte Passwort.
    [Serializable]
    [NodeInfo("Text", "Werte", "Einen Text merken")]
    public class TextVariable : VariableNode
    {
        [Hint("Text beim Start (bei jedem Play neu).")]
        [Setting("Startwert")] public string start = "";
        [Hint("„Wert erreicht“ feuert, sobald der Text genau so lautet (Groß/klein zählt).")]
        [Setting("Zielwert")] public string goal = "";

        [Hint("Der Text für „Setzen“. Kann ein ● Draht liefern.")]
        [DataIn("Neuer Text")] public string newValue = "";

        [Hint("Feuert, wenn der Text zum Zielwert wird.")]
        [Output("Wert erreicht")] [NonSerialized] public Output reachedTarget;

        [Hint("Der aktuelle Text, z.B. für „Text setzen“.")]
        [DataOut("Wert", EventLocal = true)] public string Value { get { var v = Shown(out bool known); return known && v is string t ? t : start ?? ""; } }

        protected override ValueKind Kind => ValueKind.Text;
        protected override void FillStart(ref KitValues.Declaration d) => d.startText = start;

        [Hint("Text = Neuer Text.")]
        [Input("Setzen")]
        public void Set(Signal s)
        {
            var v = Use(); if (v == null) return;
            if (!v.SetText(newValue, this, Own(s))) Unchanged(v, "keine Änderung");
        }

        [Hint("Zurück auf den Startwert.")]
        [Input("Zurücksetzen")]
        public void ResetValue(Signal s)
        {
            var v = Use(); if (v == null) return;
            if (!v.ResetToStart(this, Own(s, "zurückgesetzt"))) Unchanged(v, "war schon beim Startwert");
        }

        protected override void FireOutputs(in KitChange c, Signal s)
        {
            string old = (string)c.Old, now = (string)c.Now;
            bool atGoal = now == (goal ?? "") && old != now;
            changed.Fire(s);
            if (atGoal) reachedTarget.Fire(s);
        }
    }
}
