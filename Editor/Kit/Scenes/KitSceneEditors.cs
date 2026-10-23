using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>A <see cref="SceneRef"/> field: one slot that takes a scene file, and nothing else.</summary>
    [CustomPropertyDrawer(typeof(SceneRef))]
    public class SceneRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var asset = property.FindPropertyRelative("asset");
            var path = property.FindPropertyRelative("path");
            label = EditorGUI.BeginProperty(position, label, property);
            if (string.IsNullOrEmpty(label.tooltip)) label.tooltip = "Szene-Datei aus dem Project-Fenster hier hineinziehen. " + KitBrand.Name + " trägt sie selbst in die Build Settings ein.";
            EditorGUI.BeginChangeCheck();
            var scene = EditorGUI.ObjectField(position, label, asset.objectReferenceValue as SceneAsset, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                asset.objectReferenceValue = scene;
                path.stringValue = scene ? AssetDatabase.GetAssetPath(scene) : "";
                if (scene) KitBuildScenes.Schedule();
            }
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Eingang in the Inspector: its name is the object's name; shows which other Eingänge the scene has.</summary>
    [CustomEditor(typeof(KitEntrance))]
    [CanEditMultipleObjects]
    public class KitEntranceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var e = (KitEntrance)target;
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(new GUIContent("Name", "Der Name des Eingangs ist der Name dieses Objekts (oben im Inspector oder in der Hierarchy umbenennen). " +
                    "„Szene laden“ in einer anderen Szene nennt diesen Namen, oder die Tür kommt aus einer Szene, die genauso heißt."), e.Name);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(KitEntrance.isDefault)), new GUIContent("Standard",
                "Hier kommt der Spieler an, wenn die Tür keinen Eingang nennt und kein Eingang so heißt wie die Szene, aus der er kommt."));
            serializedObject.ApplyModifiedProperties();

            var scene = e.gameObject.scene;
            if (!scene.IsValid()) return;
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KitEntrance>(true)).ToList();
            var same = all.Where(x => x != e && x.Name == e.Name).ToList();
            if (same.Count > 0)
                EditorGUILayout.HelpBox($"Noch ein Eingang heißt „{e.Name}“. Der Spieler käme an einem der beiden an: einen umbenennen.", MessageType.Warning);
            int defaults = all.Count(x => x.isDefault);
            if (defaults > 1)
                EditorGUILayout.HelpBox($"{defaults} Eingänge sind Standard. Nur einer sollte es sein.", MessageType.Warning);
            EditorGUILayout.HelpBox(
                $"Kommt der Spieler aus einer anderen Szene hierher, steht er auf diesem Punkt und blickt in Pfeilrichtung (blaue Z-Achse).\n" +
                $"Eingänge in „{scene.name}“: {string.Join(", ", all.Select(x => x.isDefault ? x.Name + " (Standard)" : x.Name))}",
                MessageType.None);
        }
    }

    /// <summary>GameObject › Kit › Eingang: an arrival point where the Scene view looks, on the ground.</summary>
    public static class KitEntranceMenu
    {
        [MenuItem(KitBrand.GameObjectMenu + "Eingang", false, 12)]
        static void Create(MenuCommand cmd) => Create(cmd.context as GameObject);

        public static KitEntrance Create(GameObject parent = null)
        {
            var scene = parent ? parent.scene : EditorSceneManager.GetActiveScene();
            var existing = scene.IsValid() ? scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KitEntrance>(true)).ToList() : new List<KitEntrance>();
            var go = new GameObject(UniqueName(existing));
            if (parent) GameObjectUtility.SetParentAndAlign(go, parent);
            else if (SceneView.lastActiveSceneView != null)
            {
                var pivot = SceneView.lastActiveSceneView.pivot;
                go.transform.position = Physics.Raycast(pivot + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point : new Vector3(pivot.x, 0f, pivot.z);
            }
            var e = go.AddComponent<KitEntrance>();
            e.isDefault = existing.Count == 0;                 // the first Eingang of a scene is its Standard
            Undo.RegisterCreatedObjectUndo(go, KitBrand.Name + ": Eingang anlegen");
            Selection.activeGameObject = go;
            return e;
        }

        static string UniqueName(List<KitEntrance> existing)
        {
            if (!existing.Exists(x => x.Name == "Eingang")) return "Eingang";
            for (int i = 2; ; i++) if (!existing.Exists(x => x.Name == "Eingang " + i)) return "Eingang " + i;
        }
    }
}
