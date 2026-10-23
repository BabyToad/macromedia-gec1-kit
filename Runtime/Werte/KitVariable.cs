using System;
using UnityEngine;

namespace Kit
{
    // Variable: ein Wert, der an einem Objekt hängt – sichtbar im Inspector, während das Spiel läuft.
    // Beispiel: „Lebenspunkte“ 0 … 100 am Spieler, „Haltbarkeit“ an jeder Kiste.
    // Knoten (Zahl, Ja-Nein, Text) mit demselben Namen benutzen diesen Wert: fest verbunden über ◆ Objekt,
    // mit dem Ereignis (◆ Wer kam in die Zone) oder einfach über den Namen, wenn es ihn in der Szene nur einmal gibt.
    // Bereich Spiel: der Wert bleibt beim Laden einer anderen Szene erhalten (dort mit demselben Namen).
    [DefaultExecutionOrder(-1000)]   // vor jeder Logik da, damit niemand den Namen vorher mit eigenen Startwerten belegt
    [AddComponentMenu(KitBrand.ComponentMenu + "Variable")]
    public class KitVariable : MonoBehaviour
    {
        [Hint("Name der Variable, z.B. „Lebenspunkte“. Knoten mit demselben Namen benutzen diesen Wert.")]
        [SerializeField] string valueName = "Variable";

        [Hint("Zahl (z.B. 3 oder 2,5), Ja-Nein (z.B. „Tür offen“) oder Text (z.B. ein Name).")]
        [SerializeField] ValueKind kind = ValueKind.Zahl;

        [Hint("Szene: gilt, bis eine andere Szene geladen wird. Spiel: bleibt beim Szenenwechsel erhalten – in der nächsten Szene gilt der Wert der Variable mit demselben Namen weiter.")]
        [SerializeField] ObjectScope scope = ObjectScope.Szene;

        [Hint("Wert beim Start. Jedes Play beginnt wieder damit (Bereich Spiel: nur beim ersten Mal im Spiel).")]
        [SerializeField] float startNumber = 0f;
        [Hint("Wert beim Start: angehakt = ja.")]
        [SerializeField] bool startFlag;
        [Hint("Text beim Start.")]
        [SerializeField] string startText = "";

        [Hint("An: die Zahl bleibt zwischen Minimum und Maximum. Erreicht sie eine Grenze, melden die Knoten „Minimum erreicht“ bzw. „Maximum erreicht“.")]
        [SerializeField] bool limited = false;
        [Hint("Kleinster möglicher Wert, z.B. 0.")]
        [SerializeField] float min = 0f;
        [Hint("Größter möglicher Wert, z.B. 100. Ein Balken ist bei diesem Wert voll.")]
        [SerializeField] float max = 10f;

        KitValue m_Value;
        string m_Problem;
        int m_Session;      // the Play session m_Value belongs to (see KitValues.Session)

        public string ValueName => valueName;
        public ValueKind Kind => kind;
        public ObjectScope Bereich => scope;
        public Scope ScopeAsValueScope => scope == ObjectScope.Spiel ? Scope.Spiel : Scope.Szene;

        /// <summary>The live value (null outside Play, or if the name is taken by another type).</summary>
        public KitValue Value { get { EnsureValue(); return m_Value; } }

        /// <summary>Why there is no value (German), or null.</summary>
        public string Problem { get { EnsureValue(); return m_Problem; } }

        /// <summary>For code and tests: sets the settings before the component wakes up.</summary>
        public void Configure(string name, ValueKind kind, ObjectScope scope = ObjectScope.Szene,
                              float startNumber = 0f, bool limited = false, float min = 0f, float max = 100f,
                              bool startFlag = false, string startText = "")
        {
            valueName = name; this.kind = kind; this.scope = scope;
            this.startNumber = startNumber; this.limited = limited; this.min = min; this.max = max;
            this.startFlag = startFlag; this.startText = startText;
            m_Value = null; m_Problem = null;
            KitVariableIndex.Changed();      // a renamed Variable may now be found by other nodes
        }

        // A switched-off object still owns its value (it lives as long as the scene): it stays findable by name.
        // OnEnable re-registers after "Enter Play Mode" without domain reload, when Awake does not run again.
        void Awake() => EnsureValue();
        void OnEnable() { if (Application.isPlaying) EnsureValue(); }
        void OnDestroy() => KitVariableIndex.Remove(this);

        void EnsureValue()
        {
            if (!Application.isPlaying) return;
            // Enter Play Mode without domain reload keeps this object, but the stores start empty: start over too.
            if (m_Session != KitValues.Session) { m_Value = null; m_Problem = null; m_Session = KitValues.Session; }
            if (m_Value != null) { KitVariableIndex.Add(this); return; }
            // Szene: one per object, owned by the object (other objects may carry the same name).
            // Spiel: shared by name across scenes, so it goes to the game table.
            if (scope == ObjectScope.Spiel)
                m_Value = KitValues.Get(Declare(Scope.Spiel), null, gameObject.scene, out m_Problem);
            else
                m_Value = new KitValue(valueName, kind, Scope.Szene, $"„{name}“", startNumber, startFlag, startText, limited, min, max);
            if (m_Value != null) KitVariableIndex.Add(this);
        }

        KitValues.Declaration Declare(Scope s) => new KitValues.Declaration
        {
            name = valueName, kind = kind, scope = s, declaredBy = $"die Variable an „{name}“",
            startNumber = startNumber, startFlag = startFlag, startText = startText, limited = limited, min = min, max = max,
        };

        void OnValidate()
        {
            if (max < min) max = min;
            if (string.IsNullOrWhiteSpace(valueName)) valueName = "Variable";
        }

        /// <summary>"Lebenspunkte: 80 (0 … 100)" or the settings outside Play.</summary>
        public string Describe()
        {
            if (Application.isPlaying) return m_Value != null ? m_Value.Describe() : $"{valueName}: {m_Problem}";
            string start = kind == ValueKind.Zahl ? KitValue.FormatNumber(startNumber) : kind == ValueKind.JaNein ? (startFlag ? "ja" : "nein") : $"„{startText}“";
            return $"{valueName}: {start}" + (kind == ValueKind.Zahl && limited ? $" ({KitValue.FormatNumber(Mathf.Min(min, max))} … {KitValue.FormatNumber(Mathf.Max(min, max))})" : "");
        }
    }

    /// <summary>All awake Variable components, so nodes can find "the one called Münzen" without searching the scene.</summary>
    public static class KitVariableIndex
    {
        static readonly System.Collections.Generic.List<KitVariable> s_All = new System.Collections.Generic.List<KitVariable>();

        /// <summary>Goes up whenever a Variable appears, disappears or is renamed: nodes that found one by name check again.</summary>
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { s_All.Clear(); Version++; }

        internal static void Add(KitVariable v) { if (!s_All.Contains(v)) { s_All.Add(v); Version++; } }
        internal static void Remove(KitVariable v) { if (s_All.Remove(v)) Version++; }
        internal static void Changed() => Version++;

        /// <summary>The Variable called <paramref name="name"/> on this object (or its parents' children), or null.</summary>
        public static KitVariable On(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var v in go.GetComponents<KitVariable>()) if (v.ValueName == name) return v;
            // ◆ Wer is often the player's root while the Variable sits on a child (or the other way round).
            foreach (var v in go.GetComponentsInChildren<KitVariable>(true)) if (v.ValueName == name) return v;
            foreach (var v in go.GetComponentsInParent<KitVariable>(true)) if (v.ValueName == name) return v;
            return null;
        }

        /// <summary>Szene-scope components with this name in a scene. More than one means: say which object.</summary>
        public static int InScene(UnityEngine.SceneManagement.Scene scene, string name, out KitVariable first)
        {
            first = null; int n = 0;
            foreach (var v in s_All)
            {
                if (v == null || v.gameObject.scene != scene || v.ValueName != name || v.Bereich != ObjectScope.Szene) continue;
                if (n++ == 0) first = v;
            }
            return n;
        }
    }
}
