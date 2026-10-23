using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kit
{
    /// <summary>What a variable holds.</summary>
    public enum ValueKind
    {
        Zahl,
        [InspectorName("Ja-Nein")] JaNein,
        Text,
    }

    /// <summary>
    /// Who shares a variable and how long it lives. Logik: one Logik (each copy of a prefab has its own).
    /// Szene: everyone in the scene, by name, until the scene is left. Spiel: everyone, by name, across scene loads.
    /// </summary>
    public enum Scope { Logik, Szene, Spiel }

    /// <summary>The two scopes a value on an object can have (a Logik value never lives on an object).</summary>
    public enum ObjectScope { Szene, Spiel }

    /// <summary>
    /// One variable's value. Nodes and the Variable component share these objects: whoever holds the same
    /// KitValue sees the same number. Every real change raises <see cref="Changed"/> once.
    /// </summary>
    public sealed class KitValue
    {
        public readonly string Name;
        public readonly ValueKind Kind;
        public readonly Scope Scope;
        /// <summary>Who declared it first (for messages): a node title, a component, "Inventar" …</summary>
        public readonly string DeclaredBy;

        public float Number { get; private set; }
        public bool Flag { get; private set; }
        public string Text { get; private set; } = "";

        public bool Limited { get; private set; }
        public float Min { get; private set; }
        public float Max { get; private set; }

        // The start state, so "Zurücksetzen" and a restored snapshot know where to go.
        readonly float m_StartNumber; readonly bool m_StartFlag; readonly string m_StartText;

        /// <summary>
        /// Raised after every real change, never for a write that changes nothing. The <see cref="KitChange"/> holds the
        /// values of that one change: by the time a listener runs, someone else may already have changed it again.
        /// </summary>
        public event Action<KitChange> Changed;

        internal KitValue(string name, ValueKind kind, Scope scope, string declaredBy,
                          float number, bool flag, string text, bool limited, float min, float max)
        {
            Name = name; Kind = kind; Scope = scope; DeclaredBy = declaredBy;
            Limited = limited && kind == ValueKind.Zahl;
            Min = Mathf.Min(min, max); Max = Mathf.Max(min, max);
            m_StartNumber = Clamp(number); m_StartFlag = flag; m_StartText = text ?? "";
            Number = m_StartNumber; Flag = m_StartFlag; Text = m_StartText;
        }

        float Clamp(float v) => Limited ? Mathf.Clamp(v, Min, Max) : v;

        /// <summary>0 … 1 between Minimum and Maximum (for bars). Without limits: 0.</summary>
        public float Fraction => Limited && Max > Min ? Mathf.InverseLerp(Min, Max, Number) : 0f;

        public object Boxed => Kind == ValueKind.Zahl ? Number : Kind == ValueKind.JaNein ? (object)Flag : Text;

        /// <summary>Sets the number (limited to Min … Max). Returns true if it really changed.</summary>
        public bool SetNumber(float v, object writer) => SetNumber(v, writer, null);
        public bool SetFlag(bool v, object writer) => SetFlag(v, writer, null);
        public bool SetText(string v, object writer) => SetText(v, writer, null);
        public bool ResetToStart(object writer) => ResetToStart(writer, null);

        /// <summary>
        /// The same, for a writer that reacts to its own change (a node firing Geändert …): <paramref name="own"/> runs in
        /// the same ordered delivery as everyone else, right before the other listeners of this change. So a listener
        /// that changes the value again is heard after the writer, never before. The writer skips its own Changed.
        /// </summary>
        public bool SetNumber(float v, object writer, Action<KitChange> own)
        {
            v = Clamp(v);
            if (Kind != ValueKind.Zahl || v == Number) return false;
            float old = Number; Number = v;
            Raise(new KitChange(this, old, v, writer, own));
            return true;
        }

        public bool SetFlag(bool v, object writer, Action<KitChange> own)
        {
            if (Kind != ValueKind.JaNein || v == Flag) return false;
            bool old = Flag; Flag = v;
            Raise(new KitChange(this, old, v, writer, own));
            return true;
        }

        public bool SetText(string v, object writer, Action<KitChange> own)
        {
            v ??= "";
            if (Kind != ValueKind.Text || v == Text) return false;
            string old = Text; Text = v;
            Raise(new KitChange(this, old, v, writer, own));
            return true;
        }

        /// <summary>Back to the start value (raises Changed if that is a change).</summary>
        public bool ResetToStart(object writer, Action<KitChange> own) =>
            Kind == ValueKind.Zahl ? SetNumber(m_StartNumber, writer, own)
            : Kind == ValueKind.JaNein ? SetFlag(m_StartFlag, writer, own)
            : SetText(m_StartText, writer, own);

        /// <summary>Back to the start value without raising events (Neues Spiel; the scene reloads anyway).</summary>
        internal void ResetSilently() => Restore(Kind == ValueKind.Zahl ? m_StartNumber : Kind == ValueKind.JaNein ? (object)m_StartFlag : m_StartText);

        /// <summary>Puts a stored value back without raising events (a snapshot being restored).</summary>
        internal void Restore(object boxed)
        {
            switch (boxed)
            {
                case float f when Kind == ValueKind.Zahl: Number = Clamp(f); break;
                case bool b when Kind == ValueKind.JaNein: Flag = b; break;
                case string s when Kind == ValueKind.Text: Text = s; break;
            }
        }

        // Notifications are delivered one after the other, never nested: a listener that changes a value again only
        // queues that change. So every listener sees each change in order, with that change's own old and new value.
        // Variables that keep changing each other (A changes B, B changes A …) stop after this many deliveries.
        // A loop through several Logiks restarts each Logik's own step budget, so there is also a cap per frame.
        const int k_MaxDeliveries = 64, k_MaxPerFrame = 500;
        static readonly Queue<KitChange> s_Queue = new Queue<KitChange>();
        static bool s_Delivering;
        static int s_Frame = -1, s_ThisFrame;

        void Raise(in KitChange change)
        {
            if (Changed == null && change.Own == null) return;
            if (Time.frameCount != s_Frame) { s_Frame = Time.frameCount; s_ThisFrame = 0; }
            if (s_ThisFrame > k_MaxPerFrame) return;          // already stopped (and said so) in this frame
            s_Queue.Enqueue(change);
            if (s_Delivering) return;
            s_Delivering = true;
            int delivered = 0;
            try
            {
                while (s_Queue.Count > 0)
                {
                    var c = s_Queue.Dequeue();
                    if (++delivered > k_MaxDeliveries || ++s_ThisFrame > k_MaxPerFrame)
                    {
                        Debug.LogError($"{KitBrand.Log}Variable „{c.Value.Name}“: Variablen ändern sich gegenseitig im Kreis – gestoppt. Einen der Drähte entfernen, über die sich die Variablen gegenseitig ändern, oder ihn mit „Einmal“ absichern.");
                        s_Queue.Clear();
                        break;
                    }
                    c.Own?.Invoke(c);
                    c.Value.Changed?.Invoke(c);
                }
            }
            finally { s_Delivering = false; }
        }

        internal static void ResetDelivery() { s_Queue.Clear(); s_Delivering = false; }

        static readonly CultureInfo k_German = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>The value as a student reads it: "3", "2,5", "ja", "„Hallo“".</summary>
        public string Display =>
            Kind == ValueKind.Zahl ? FormatNumber(Number)
            : Kind == ValueKind.JaNein ? (Flag ? "ja" : "nein")
            : $"„{Text}“";

        public static string FormatNumber(float v) => v.ToString("0.##", k_German);

        /// <summary>"Münzen: 3 (0 … 10)" for Inspectors and node readouts.</summary>
        public string Describe() => $"{Name}: {Display}" + (Limited ? $" ({FormatNumber(Min)} … {FormatNumber(Max)})" : "");

        internal bool SameShape(ValueKind kind, bool limited, float min, float max, float startNumber, bool startFlag, string startText) =>
            kind == Kind && (kind != ValueKind.Zahl || (limited == Limited && (!limited || (Mathf.Approximately(Mathf.Min(min, max), Min) && Mathf.Approximately(Mathf.Max(min, max), Max)))
                && Mathf.Approximately(Clamp(startNumber), m_StartNumber)))
            && (kind != ValueKind.JaNein || startFlag == m_StartFlag)
            && (kind != ValueKind.Text || (startText ?? "") == m_StartText);
    }

    /// <summary>One change of one variable: the value before and after, and who wrote it (a node, a component or null).</summary>
    public readonly struct KitChange
    {
        public readonly KitValue Value;
        public readonly object Old, Now, Writer;
        /// <summary>The writer's own reaction to this change, delivered first (see <see cref="KitValue.SetNumber(float, object, Action{KitChange})"/>).</summary>
        public readonly Action<KitChange> Own;
        public KitChange(KitValue value, object old, object now, object writer, Action<KitChange> own = null) { Value = value; Old = old; Now = now; Writer = writer; Own = own; }
    }

    /// <summary>
    /// Where variables live. Logik values belong to one Logik; Szene values to one loaded scene; Spiel values to
    /// the whole game (they survive scene loads; entering Play starts empty). Nodes and components ask here by
    /// name; the first to ask declares the value (type, start, limits), later askers share it.
    /// </summary>
    public static class KitValues
    {
        /// <summary>What a request for a variable looks like (from a node's settings or a component).</summary>
        public struct Declaration
        {
            public string name;
            public ValueKind kind;
            public Scope scope;
            public float startNumber; public bool startFlag; public string startText;
            public bool limited; public float min, max;
            public string declaredBy;
        }

        static readonly Dictionary<Logic, Dictionary<string, KitValue>> s_Logik = new Dictionary<Logic, Dictionary<string, KitValue>>();
        static readonly Dictionary<int, Dictionary<string, KitValue>> s_Scene = new Dictionary<int, Dictionary<string, KitValue>>();
        static readonly Dictionary<string, KitValue> s_Game = new Dictionary<string, KitValue>();
        static Dictionary<string, object> s_Entry;           // Spiel values when the current scene was entered
        static readonly HashSet<string> s_Warned = new HashSet<string>();

        /// <summary>Raised when a value is declared or a scope is cleared (for editor windows that list values).</summary>
        public static event Action ListChanged;

        /// <summary>
        /// Counts Play sessions. Components keep their cached values across "Enter Play Mode" without domain reload;
        /// they compare this number to notice a new session and start over.
        /// </summary>
        public static int Session { get; private set; }

        /// <summary>A new Play session: every store starts empty. Runs on entering Play (also without domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSession()
        {
            Session++;
            KitValue.ResetDelivery();
            s_Logik.Clear(); s_Scene.Clear(); s_Game.Clear(); s_Entry = null; s_Warned.Clear();
            KitBags.ResetOnPlay();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Neustart / Neues Spiel (Szenen): KitGame says when; the values live here.
            KitGame.Resetting -= ResetGame;
            KitGame.Resetting += ResetGame;
            KitGame.RestoringSceneEntry -= RestoreSceneEntry;
            KitGame.RestoringSceneEntry += RestoreSceneEntry;
        }

        static void OnSceneUnloaded(Scene s)
        {
            if (s_Scene.Remove(s.handle)) ListChanged?.Invoke();
            KitBags.SceneUnloaded(s.handle);
        }

        // A single-mode load is a new start in a new place: remember the Spiel values as they were on entry.
        static void OnSceneLoaded(Scene s, LoadSceneMode mode) { if (mode == LoadSceneMode.Single) MarkSceneEntry(); }

        /// <summary>
        /// Finds or declares a variable. Returns null and a German reason if the name is taken by a different type.
        /// A second declaration with other start/limits shares the first one and warns once in the Console.
        /// </summary>
        public static KitValue Get(in Declaration d, Logic logik, Scene scene, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(d.name)) { problem = "die Variable hat keinen Namen"; return null; }
            Dictionary<string, KitValue> table;
            switch (d.scope)
            {
                case Scope.Logik:
                    if (logik == null) { problem = "Bereich Logik braucht eine Logik"; return null; }
                    if (!s_Logik.TryGetValue(logik, out table)) s_Logik[logik] = table = new Dictionary<string, KitValue>();
                    break;
                case Scope.Szene:
                    if (!scene.IsValid()) { problem = "keine Szene geladen"; return null; }
                    if (!s_Scene.TryGetValue(scene.handle, out table)) s_Scene[scene.handle] = table = new Dictionary<string, KitValue>();
                    break;
                default: table = s_Game; break;
            }
            if (table.TryGetValue(d.name, out var v))
            {
                if (v.Kind != d.kind)
                {
                    problem = $"„{d.name}“ ist schon eine {KindName(v.Kind)}-Variable (von {v.DeclaredBy}), hier wird sie als {KindName(d.kind)} benutzt";
                    return null;
                }
                if (!v.SameShape(d.kind, d.limited, d.min, d.max, d.startNumber, d.startFlag, d.startText) && s_Warned.Add(d.scope + "/" + d.name + "/" + d.declaredBy))
                    Debug.LogWarning($"{KitBrand.Log}Variable „{d.name}“ ({ScopeName(d.scope)}): {d.declaredBy} hat einen anderen Startwert oder andere Grenzen als {v.DeclaredBy}. Es gilt, was {v.DeclaredBy} eingestellt hat.");
                return v;
            }
            v = new KitValue(d.name, d.kind, d.scope, d.declaredBy, d.startNumber, d.startFlag, d.startText, d.limited, d.min, d.max);
            table[d.name] = v;
            ListChanged?.Invoke();
            return v;
        }

        /// <summary>A Logik stopped: its own values are gone (the next start begins at the start values).</summary>
        public static void ReleaseLogik(Logic logik)
        {
            if (logik != null && s_Logik.Remove(logik)) ListChanged?.Invoke();
        }

        // ---- reading (for Inspectors, windows, tests) ---------------------------------------------------
        public static bool TryFind(Scope scope, string name, Logic logik, Scene scene, out KitValue value)
        {
            value = null;
            Dictionary<string, KitValue> table = null;
            if (scope == Scope.Logik) { if (logik != null) s_Logik.TryGetValue(logik, out table); }
            else if (scope == Scope.Szene) { if (scene.IsValid()) s_Scene.TryGetValue(scene.handle, out table); }
            else table = s_Game;
            return table != null && name != null && table.TryGetValue(name, out value);
        }

        public static IEnumerable<KitValue> OfLogik(Logic logik) =>
            logik != null && s_Logik.TryGetValue(logik, out var t) ? t.Values : (IEnumerable<KitValue>)Array.Empty<KitValue>();

        public static IEnumerable<KitValue> OfScene(Scene scene) =>
            scene.IsValid() && s_Scene.TryGetValue(scene.handle, out var t) ? t.Values : (IEnumerable<KitValue>)Array.Empty<KitValue>();

        public static IEnumerable<KitValue> OfGame => s_Game.Values;

        // ---- Spiel values and scene changes (used by the Szenen nodes) -----------------------------------
        /// <summary>Remembers the Spiel values as they are now: "Neustart" goes back to this state.</summary>
        public static void MarkSceneEntry()
        {
            s_Entry = new Dictionary<string, object>();
            foreach (var kv in s_Game) s_Entry[kv.Key] = kv.Value.Boxed;
            KitBags.MarkSceneEntry();
        }

        // Spiel values are never removed while a Play session lasts, only put back: whoever holds one (a Variable
        // on a DontDestroyOnLoad object, a node, a display) keeps holding the same, current value.

        /// <summary>
        /// Puts Spiel values back to how they were when the scene was entered (a retry keeps nothing from the
        /// failed attempt). Values that did not exist yet go back to their start. No events fire: the scene reloads anyway.
        /// </summary>
        public static void RestoreSceneEntry()
        {
            if (s_Entry == null) return;
            foreach (var kv in s_Game)
            {
                if (s_Entry.TryGetValue(kv.Key, out var old)) kv.Value.Restore(old);
                else kv.Value.ResetSilently();
            }
            KitBags.RestoreSceneEntry();
            ListChanged?.Invoke();
        }

        /// <summary>Every Spiel value back to its start (a new game from the start menu). No events fire.</summary>
        public static void ResetGame()
        {
            foreach (var v in s_Game.Values) v.ResetSilently();
            s_Entry = null;
            KitBags.ResetGame();
            ListChanged?.Invoke();
        }

        public static string KindName(ValueKind k) => k == ValueKind.Zahl ? "Zahl" : k == ValueKind.JaNein ? "Ja-Nein" : "Text";
        public static string ScopeName(Scope s) => s == Scope.Logik ? "Logik" : s == Scope.Szene ? "Szene" : "Spiel";
    }
}
