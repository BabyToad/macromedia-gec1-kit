using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector for kit components with list fields: Unity's property drawers reach a list's elements but not its
    /// header, so the German label of a [Hint] is applied here (single fields get theirs from <see cref="HintDrawer"/>).
    /// </summary>
    public abstract class HintInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                    continue;
                }
                var hint = HintAttribute.Of(FieldOf(target.GetType(), it.name));
                if (it.isArray && it.propertyType != SerializedPropertyType.String && hint != null && !string.IsNullOrEmpty(hint.Label))
                    EditorGUILayout.PropertyField(it, new GUIContent(hint.Label, hint.Text), true);
                else
                    EditorGUILayout.PropertyField(it, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static FieldInfo FieldOf(System.Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }

    [CustomEditor(typeof(KitPlayerCamera)), CanEditMultipleObjects] public class KitPlayerCameraInspector : HintInspector { }
    [CustomEditor(typeof(KitDialogBox)), CanEditMultipleObjects] public class KitDialogBoxInspector : HintInspector { }
    [CustomEditor(typeof(KitInventory)), CanEditMultipleObjects] public class KitInventoryInspector : HintInspector { }
}
