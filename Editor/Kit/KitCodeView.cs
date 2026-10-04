using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// "Code ansehen": the C# file behind a node, read-only, with the lines that matter marked:
    /// where an output fires (orange), where it reports "läuft" (yellow), where it fails (red),
    /// where a ◆ reference is declared (blue-green).
    /// </summary>
    public class KitCodeView : EditorWindow
    {
        [SerializeField] string m_Path;
        [SerializeField] string m_Title;
        string[] m_Lines;
        Vector2 m_Scroll;
        GUIStyle m_Mono;

        public static void Show(Type runtimeType)
        {
            var script = FindScript(runtimeType);
            var w = GetWindow<KitCodeView>(false, "Code ansehen", true);
            w.m_Title = NodeSchema.Of(runtimeType).Title;
            w.m_Path = script ? AssetDatabase.GetAssetPath(script) : null;
            w.m_Lines = null;
            w.titleContent = new GUIContent("Code: " + w.m_Title);
            w.Show();
        }

        [NonSerialized] string m_Error;

        /// <summary>
        /// The script's text through Unity (MonoScript.text). Works for scripts in Assets and in installed
        /// packages, whose "Packages/…" paths are virtual and not readable with File IO.
        /// </summary>
        public static string[] ReadLines(string assetPath, out string error)
        {
            error = null;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null) { error = $"keine C#-Datei unter {assetPath}"; return null; }
            var text = script.text;
            if (string.IsNullOrEmpty(text)) { error = $"{assetPath} ist leer oder nicht lesbar"; return null; }
            return text.Replace("\r\n", "\n").Split('\n');
        }

        public static MonoScript FindScript(Type t) =>
            AssetDatabase.FindAssets("t:MonoScript " + t.Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(s => s && s.GetClass() == t);

        void OnGUI()
        {
            if (string.IsNullOrEmpty(m_Path)) { EditorGUILayout.HelpBox("Für diesen Knoten wurde keine C#-Datei gefunden.", MessageType.Info); return; }
            m_Lines ??= ReadLines(m_Path, out m_Error);
            if (m_Lines == null) { EditorGUILayout.HelpBox($"Code konnte nicht gelesen werden: {m_Error}", MessageType.Error); return; }
            m_Mono ??= new GUIStyle(EditorStyles.label) { font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font, richText = false };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"{m_Title} · {m_Path} · {m_Lines.Length} Zeilen", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Im Code-Editor öffnen", EditorStyles.toolbarButton))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(m_Path));
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Legend(KitStyle.Event, "feuert einen Ausgang"); Legend(KitStyle.Running, "meldet „läuft“");
                Legend(KitStyle.Failed, "Fehler"); Legend(KitStyle.Ref, "◆ Objekt");
            }
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            for (int i = 0; i < m_Lines.Length; i++)
            {
                var line = m_Lines[i];
                var rect = EditorGUILayout.GetControlRect(false, 16);
                var mark = Mark(line);
                if (mark.a > 0) EditorGUI.DrawRect(rect, new Color(mark.r, mark.g, mark.b, 0.16f));
                if (mark.a > 0) EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), mark);
                GUI.Label(new Rect(rect.x + 6, rect.y, 34, rect.height), (i + 1).ToString(), EditorStyles.miniLabel);
                bool comment = line.TrimStart().StartsWith("//");
                var c = GUI.contentColor;
                if (comment) GUI.contentColor = new Color(0.6f, 0.68f, 0.55f);
                GUI.Label(new Rect(rect.x + 42, rect.y, rect.width - 42, rect.height), line.Replace("\t", "    "), m_Mono);
                GUI.contentColor = c;
            }
            EditorGUILayout.EndScrollView();
        }

        static Color Mark(string line)
        {
            if (line.TrimStart().StartsWith("//")) return Color.clear;
            if (line.Contains(".Fire(")) return KitStyle.Event;
            if (line.Contains("Fail(")) return KitStyle.Failed;
            if (line.Contains("Running(")) return KitStyle.Running;
            if (line.Contains("[Ref(") || line.Contains("[RefOut(")) return KitStyle.Ref;
            return Color.clear;
        }

        static void Legend(Color c, string text)
        {
            var r = GUILayoutUtility.GetRect(10, 14, GUILayout.Width(10));
            EditorGUI.DrawRect(new Rect(r.x, r.y + 3, 8, 8), c);
            GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }
    }
}
