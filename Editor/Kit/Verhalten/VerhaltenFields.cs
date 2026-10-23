using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Draws a kit component's fields the way students read them, in the kit's one format: the German name from
    /// [Hint(label:)] with the unit in brackets („Sichtweite (m)“), the composed Hint as tooltip. Unity never routes
    /// a TooltipAttribute (which [Hint] is) to a property drawer, so the kit's Inspectors pass the label themselves.
    /// Headers, ranges and lists keep Unity's own drawing.
    /// </summary>
    static class VerhaltenFields
    {
        public static void Draw(SerializedObject so)
        {
            var type = so.targetObject.GetType();
            var p = so.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                enter = false;
                if (p.name == "m_Script") continue;
                var hint = Find(type, p.name)?.GetCustomAttribute<HintAttribute>();
                var text = string.IsNullOrEmpty(hint?.Label) ? p.displayName : hint.Label;
                if (!string.IsNullOrEmpty(hint?.Unit) && !text.EndsWith(")")) text += $" ({hint.Unit})";
                EditorGUILayout.PropertyField(p, new GUIContent(text, hint?.tooltip), true);
            }
        }

        static FieldInfo Find(Type t, string name)
        {
            for (; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }
}
