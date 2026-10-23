using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Tools › Brand › Szenen: the game's scenes in order (this is Unity's Build Settings list, nothing extra).
    /// Drag to reorder, untick to leave a scene out, see each scene's Eingänge. Scenes outside the list can be
    /// added back with one click.
    /// </summary>
    public class KitScenesWindow : EditorWindow
    {
        ReorderableList m_List;
        List<EditorBuildSettingsScene> m_Scenes;
        Vector2 m_Scroll;

        [MenuItem(KitBrand.ToolsMenu + "Szenen", false, 20)]
        public static void Open() => GetWindow<KitScenesWindow>("Szenen").Show();

        void OnEnable()
        {
            EditorBuildSettings.sceneListChanged += Reload;
            Reload();
        }

        void OnDisable() => EditorBuildSettings.sceneListChanged -= Reload;
        void OnFocus() { KitBuildScenes.Sync(); Reload(); }

        void Reload()
        {
            m_Scenes = EditorBuildSettings.scenes.ToList();
            m_List = new ReorderableList(m_Scenes, typeof(EditorBuildSettingsScene), true, true, false, false)
            {
                drawHeaderCallback = r => EditorGUI.LabelField(r, new GUIContent("Reihenfolge im Spiel",
                    "Die erste Szene startet das fertige Spiel. „Nächste Szene“ geht eine Zeile weiter. Ziehen zum Umsortieren, Haken weg = nicht im Spiel.")),
                elementHeightCallback = i => EditorGUIUtility.singleLineHeight * 2 + 6,
                drawElementCallback = DrawRow,
                onReorderCallback = _ => Write("Reihenfolge der Szenen"),
            };
            Repaint();
        }

        void Write(string what) => KitSceneList.Set(m_Scenes, what);

        void DrawRow(Rect r, int i, bool active, bool focused)
        {
            var s = m_Scenes[i];
            float h = EditorGUIUtility.singleLineHeight;
            var line = new Rect(r.x, r.y + 2, r.width, h);
            int playIndex = m_Scenes.Take(i).Count(x => x.enabled);

            EditorGUI.BeginChangeCheck();
            bool on = EditorGUI.Toggle(new Rect(line.x, line.y, 18, h), new GUIContent("", "Im Spiel (Haken) oder ausgelassen"), s.enabled);
            if (EditorGUI.EndChangeCheck()) { m_Scenes[i] = new EditorBuildSettingsScene(s.path, on); Write(on ? "Szene ins Spiel" : "Szene auslassen"); }

            var name = SceneRef.NameOf(s.path);
            var style = s.enabled ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUI.LabelField(new Rect(line.x + 22, line.y, 34, h), s.enabled ? playIndex.ToString() : "–", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(line.x + 56, line.y, line.width - 56 - 140, h), new GUIContent(name, s.path), style);
            if (GUI.Button(new Rect(line.xMax - 136, line.y, 64, h), new GUIContent("Zeigen", "Im Project-Fenster zeigen")))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path));
            if (GUI.Button(new Rect(line.xMax - 68, line.y, 68, h), new GUIContent("Öffnen", "Diese Szene öffnen (fragt vorher nach dem Speichern)"))
                && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(s.path);

            var info = new Rect(line.x + 56, line.y + h + 2, line.width - 56, h);
            var names = KitEntranceIndex.NamesIn(s.path);
            string text;
            if (names != null) text = names.Count == 0 ? "keine Eingänge" : "Eingänge: " + string.Join(", ", names);
            else
            {
                // A prefab in the scene may hold Eingänge the file does not show: say so instead of "keine".
                var partial = KitEntranceIndex.FromFile(s.path).names;
                text = (partial.Count > 0 ? "Eingänge: " + string.Join(", ", partial) + " und vielleicht mehr" : "Eingänge nicht sicher bekannt")
                     + " (Prefab in der Szene: zum Nachsehen öffnen)";
            }
            if (i == 0 && s.enabled) text = "Start des Spiels · " + text;
            EditorGUI.LabelField(info, text, EditorStyles.miniLabel);
        }

        void OnGUI()
        {
            if (m_List == null) Reload();
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            EditorGUILayout.HelpBox(
                "Das ist die Szenen-Liste der Build Settings. " + KitBrand.Name + " trägt neue Szenen selbst hinten ein. " +
                "Hier die Reihenfolge festlegen: oben steht der Start des Spiels, „Nächste Szene“ geht eine Zeile weiter.",
                MessageType.None);
            m_List.DoLayoutList();

            var listed = new HashSet<string>(m_Scenes.Select(x => x.path));
            var outside = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity") && !listed.Contains(p)).OrderBy(p => p).ToList();
            if (outside.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(new GUIContent("Nicht in der Liste",
                    "Beispiele des Kits und Szenen, die jemand aus der Liste genommen hat. „Eintragen“ hängt sie hinten an."), EditorStyles.boldLabel);
                foreach (var p in outside)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent(SceneRef.NameOf(p), p));
                        if (GUILayout.Button("Eintragen", GUILayout.Width(80)))
                        {
                            m_Scenes.Add(new EditorBuildSettingsScene(p, true));
                            Write("Szene eintragen");
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Build Profiles öffnen", "Unitys eigenes Fenster mit derselben Liste")))
                EditorApplication.ExecuteMenuItem("File/Build Profiles");
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Edits of the Build Settings scene list that Ctrl+Z can take back. Unity's list is no object Undo can record, so a
    /// hidden copy is recorded instead and written back after Undo/Redo.
    /// </summary>
    public static class KitSceneList
    {
        sealed class State : ScriptableObject
        {
            public List<string> paths = new List<string>();
            public List<bool> enabled = new List<bool>();
        }

        static State s_State;
        static List<string> s_AppliedPaths = new List<string>();   // what this helper last wrote (to tell its own Undo from others)
        static List<bool> s_AppliedEnabled = new List<bool>();

        static State Current()
        {
            if (!s_State)
            {
                s_State = ScriptableObject.CreateInstance<State>();
                s_State.hideFlags = HideFlags.HideAndDontSave;
                Undo.undoRedoPerformed += Restore;
            }
            return s_State;
        }

        static void Capture(State st, IEnumerable<EditorBuildSettingsScene> scenes)
        {
            st.paths = scenes.Select(x => x.path).ToList();
            st.enabled = scenes.Select(x => x.enabled).ToList();
        }

        /// <summary>Replaces the list, as one Undo step named <paramref name="what"/>.</summary>
        public static void Set(IEnumerable<EditorBuildSettingsScene> scenes, string what)
        {
            var st = Current();
            Capture(st, EditorBuildSettings.scenes);           // what Undo goes back to
            Undo.RecordObject(st, what);
            var list = scenes.ToList();
            Capture(st, list);
            Remember(st);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void Remember(State st) { s_AppliedPaths = st.paths.ToList(); s_AppliedEnabled = st.enabled.ToList(); }

        // Undo/Redo of anything runs this. Only an Undo that changed the recorded list is a scene-list edit; scenes
        // that arrived since (the sync, Build Profiles) stay, appended as they were.
        static void Restore()
        {
            if (!s_State) return;
            if (s_State.paths.SequenceEqual(s_AppliedPaths) && s_State.enabled.SequenceEqual(s_AppliedEnabled)) return;
            var restored = s_State.paths.Select((p, i) => new EditorBuildSettingsScene(p, i < s_State.enabled.Count && s_State.enabled[i])).ToList();
            var known = new HashSet<string>(s_AppliedPaths.Concat(s_State.paths));
            restored.AddRange(EditorBuildSettings.scenes.Where(x => !known.Contains(x.path)));
            Remember(s_State);
            EditorBuildSettings.scenes = restored.ToArray();
        }
    }
}
