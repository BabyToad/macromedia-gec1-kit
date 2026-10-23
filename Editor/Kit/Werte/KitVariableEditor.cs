using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector of the Variable component: only the fields that matter for its type, a few ready presets, and
    /// in Play the live value (big, so it can be read while playing) with buttons to try out changes.
    /// </summary>
    [CustomEditor(typeof(KitVariable)), CanEditMultipleObjects]
    public class KitVariableEditor : UnityEditor.Editor
    {
        SerializedProperty m_Name, m_Kind, m_Scope, m_StartNumber, m_StartFlag, m_StartText, m_Limited, m_Min, m_Max;

        void OnEnable()
        {
            m_Name = serializedObject.FindProperty("valueName");
            m_Kind = serializedObject.FindProperty("kind");
            m_Scope = serializedObject.FindProperty("scope");
            m_StartNumber = serializedObject.FindProperty("startNumber");
            m_StartFlag = serializedObject.FindProperty("startFlag");
            m_StartText = serializedObject.FindProperty("startText");
            m_Limited = serializedObject.FindProperty("limited");
            m_Min = serializedObject.FindProperty("min");
            m_Max = serializedObject.FindProperty("max");
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects) Live((KitVariable)target);

            using (new EditorGUI.DisabledScope(Application.isPlaying))   // in Play the settings would only apply next time
            {
                EditorGUILayout.PropertyField(m_Name, new GUIContent("Name", KitHints.Tip(m_Name)));
                EditorGUILayout.PropertyField(m_Kind, new GUIContent("Typ", KitHints.Tip(m_Kind)));
                EditorGUILayout.PropertyField(m_Scope, new GUIContent("Bereich", KitHints.Tip(m_Scope)));
                if (!m_Kind.hasMultipleDifferentValues)
                {
                    switch ((ValueKind)m_Kind.enumValueIndex)
                    {
                        case ValueKind.Zahl:
                            EditorGUILayout.PropertyField(m_StartNumber, new GUIContent("Startwert", KitHints.Tip(m_StartNumber)));
                            EditorGUILayout.PropertyField(m_Limited, new GUIContent("Grenzen", KitHints.Tip(m_Limited)));
                            if (m_Limited.boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(m_Min, new GUIContent("Minimum", KitHints.Tip(m_Min)));
                                EditorGUILayout.PropertyField(m_Max, new GUIContent("Maximum", KitHints.Tip(m_Max)));
                                EditorGUI.indentLevel--;
                                if (m_Max.floatValue < m_Min.floatValue) EditorGUILayout.HelpBox("Maximum ist kleiner als Minimum.", MessageType.Warning);
                                else if (m_StartNumber.floatValue < m_Min.floatValue || m_StartNumber.floatValue > m_Max.floatValue)
                                    EditorGUILayout.HelpBox("Der Startwert liegt außerhalb der Grenzen und wird beim Start begrenzt.", MessageType.Info);
                            }
                            break;
                        case ValueKind.JaNein:
                            EditorGUILayout.PropertyField(m_StartFlag, new GUIContent("Startwert (ja)", KitHints.Tip(m_StartFlag)));
                            break;
                        default:
                            EditorGUILayout.PropertyField(m_StartText, new GUIContent("Startwert", KitHints.Tip(m_StartText)));
                            break;
                    }
                }
            }
            if (!Application.isPlaying) Presets();
            serializedObject.ApplyModifiedProperties();
            if (!Application.isPlaying && !serializedObject.isEditingMultipleObjects) Siblings((KitVariable)target);
        }

        static GUIStyle s_Big;

        static void Live(KitVariable v)
        {
            s_Big ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            var value = v.Value;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (value == null) { EditorGUILayout.HelpBox(v.Problem ?? "kein Wert", MessageType.Error); return; }
                EditorGUILayout.LabelField("Jetzt", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(value.Display, s_Big, GUILayout.Height(30));
                if (value.Kind == ValueKind.Zahl && value.Limited)
                {
                    var r = GUILayoutUtility.GetRect(18, 14, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(r, value.Fraction, $"{KitValue.FormatNumber(value.Min)} … {KitValue.FormatNumber(value.Max)}");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Try-out buttons: change the value as a node would, so every node listening reacts.
                    switch (value.Kind)
                    {
                        case ValueKind.Zahl:
                            if (GUILayout.Button("− 1")) value.SetNumber(value.Number - 1, v);
                            if (GUILayout.Button("+ 1")) value.SetNumber(value.Number + 1, v);
                            break;
                        case ValueKind.JaNein:
                            if (GUILayout.Button("Umschalten")) value.SetFlag(!value.Flag, v);
                            break;
                    }
                    if (GUILayout.Button("Startwert")) value.ResetToStart(v);
                }
                if (v.Bereich == ObjectScope.Spiel) EditorGUILayout.LabelField("Bereich Spiel: bleibt beim Laden einer anderen Szene erhalten.", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space();
        }

        void Presets()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Vorlage", "Füllt die Felder mit einem üblichen Beispiel. Danach frei anpassen."));
                if (GUILayout.Button(new GUIContent("Lebenspunkte", "Zahl 0 … 100, Start 100"), EditorStyles.miniButtonLeft)) Preset("Lebenspunkte", ValueKind.Zahl, 100, true, 0, 100);
                if (GUILayout.Button(new GUIContent("Zähler", "Zahl ab 0, ohne Grenzen (Münzen, Runden …)"), EditorStyles.miniButtonMid)) Preset("Münzen", ValueKind.Zahl, 0, false, 0, 10);
                if (GUILayout.Button(new GUIContent("Schalter", "Ja-Nein, Start nein (Tür offen, Strom an …)"), EditorStyles.miniButtonRight)) Preset("Strom an", ValueKind.JaNein, 0, false, 0, 1);
            }
        }

        void Preset(string name, ValueKind kind, float start, bool limited, float min, float max)
        {
            m_Name.stringValue = name; m_Kind.enumValueIndex = (int)kind;
            m_StartNumber.floatValue = start; m_Limited.boolValue = limited; m_Min.floatValue = min; m_Max.floatValue = max;
            m_StartFlag.boolValue = false;
        }

        // Two Variables with the same name on one object: the nodes would only ever find the first.
        static void Siblings(KitVariable v)
        {
            int same = 0;
            foreach (var other in v.GetComponents<KitVariable>()) if (other.ValueName == v.ValueName) same++;
            if (same > 1) EditorGUILayout.HelpBox($"„{v.ValueName}“ gibt es {same}× an diesem Objekt. Knoten finden nur die erste – bitte umbenennen.", MessageType.Warning);
        }
    }
}
