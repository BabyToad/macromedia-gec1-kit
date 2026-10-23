using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kit.Editor
{
    /// <summary>
    /// Every variable while the game runs, in one place: Spiel, then each loaded scene (components and named values),
    /// then each Logik's own values. Click a row to select its object. Values are read live; nothing is cached here.
    /// </summary>
    public class KitValuesWindow : EditorWindow
    {
        Vector2 m_Scroll;

        [MenuItem(KitBrand.ToolsMenu + "Werte", false, 30)]
        public static void Open() => GetWindow<KitValuesWindow>("Werte").Show();

        void OnEnable() { Kit.KitValues.ListChanged += Repaint; EditorApplication.playModeStateChanged += _ => Repaint(); }
        void OnDisable() => Kit.KitValues.ListChanged -= Repaint;
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Hier stehen im Play-Modus alle Variablen mit ihrem aktuellen Wert: Spiel, Szene und jede Logik.", MessageType.Info);
                return;
            }
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            Section("Spiel (bleibt beim Szenenwechsel)", Kit.KitValues.OfGame.Select(v => (v, (Object)null)));
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var onObjects = Object.FindObjectsByType<KitVariable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(c => c.gameObject.scene == scene && c.Bereich == ObjectScope.Szene && c.Value != null)
                    .Select(c => (c.Value, (Object)c.gameObject));
                Section($"Szene „{scene.name}“", Kit.KitValues.OfScene(scene).Select(v => (v, (Object)null)).Concat(onObjects));
            }
            foreach (var logic in Object.FindObjectsByType<Logic>(FindObjectsSortMode.None).OrderBy(l => l.name))
            {
                var own = Kit.KitValues.OfLogik(logic).ToList();
                if (own.Count > 0) Section($"Logik „{logic.name}“", own.Select(v => (v, (Object)logic.gameObject)));
            }
            EditorGUILayout.EndScrollView();
        }

        static void Section(string title, System.Collections.Generic.IEnumerable<(KitValue value, Object owner)> rows)
        {
            var list = rows.ToList();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (list.Count == 0) { EditorGUILayout.LabelField("  –", EditorStyles.miniLabel); return; }
            foreach (var (v, owner) in list)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(v.Name, GUILayout.Width(160));
                    EditorGUILayout.LabelField(v.Display + (v.Limited ? $"   ({KitValue.FormatNumber(v.Min)} … {KitValue.FormatNumber(v.Max)})" : ""), EditorStyles.boldLabel);
                    if (owner != null && GUILayout.Button(owner.name, EditorStyles.miniButton, GUILayout.MaxWidth(160))) { Selection.activeObject = owner; EditorGUIUtility.PingObject(owner); }
                }
            }
            EditorGUILayout.Space();
        }
    }
}
