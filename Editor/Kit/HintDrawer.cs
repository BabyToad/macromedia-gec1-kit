using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Draws a component field that carries [Hint]: Unity's own field, with the German label (if the Hint has one),
    /// the composed tooltip, the unit after the label ("Gehtempo (m/s)"), and a [Range] slider or [Min] clamp if the field has one.
    /// Arrays keep Unity's list drawing.
    /// </summary>
    [CustomPropertyDrawer(typeof(HintAttribute))]
    public class HintDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, Label(label, property), true);

        // Unity gives a field only one drawer, so the slider of [Range] and the clamp of [Min] are drawn here as well
        // (otherwise a field with both a German label and a range would lose one of them).
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var content = Label(label, property);
            var range = fieldInfo?.GetCustomAttribute<RangeAttribute>(true);
            if (range != null && property.propertyType == SerializedPropertyType.Float) { EditorGUI.Slider(position, property, range.min, range.max, content); return; }
            if (range != null && property.propertyType == SerializedPropertyType.Integer) { EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, content); return; }
            // [Min] clamps only what the student just typed, like Unity's own MinDrawer: drawing never rewrites a value.
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, content, true);
            if (!EditorGUI.EndChangeCheck()) return;
            var min = fieldInfo?.GetCustomAttribute<MinAttribute>(true);
            if (min == null) return;
            if (property.propertyType == SerializedPropertyType.Float && property.floatValue < min.min) property.floatValue = min.min;
            else if (property.propertyType == SerializedPropertyType.Integer && property.intValue < (int)min.min) property.intValue = (int)min.min;
        }


        GUIContent Label(GUIContent unityLabel, SerializedProperty property)
        {
            var hint = (HintAttribute)attribute;
            // Unity runs this drawer for each element of a list field too: elements keep "Element 0" (the list's own
            // header gets the German label from HintInspector).
            bool element = property != null && property.propertyPath.EndsWith("]");
            string text = string.IsNullOrEmpty(hint.Label) || element ? unityLabel.text : hint.Label;
            if (!element && !string.IsNullOrEmpty(hint.Unit) && !text.EndsWith(")")) text += $" ({hint.Unit})";
            return new GUIContent(text, hint.Text);
        }
    }

    /// <summary>For custom Inspectors: the [Hint] of a serialized field (SerializedProperty.tooltip only knows Unity's [Tooltip]).</summary>
    public static class KitHints
    {
        public static HintAttribute Of(SerializedProperty p)
        {
            if (p == null || p.serializedObject.targetObject == null) return null;
            for (var t = p.serializedObject.targetObject.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(p.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return HintAttribute.Of(f);
            }
            return null;
        }

        /// <summary>The tooltip text of a field: its [Hint], else Unity's own tooltip.</summary>
        public static string Tip(SerializedProperty p) => Of(p)?.Text ?? p?.tooltip;
    }
}
