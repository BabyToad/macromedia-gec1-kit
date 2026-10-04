using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector for an Interaction: the graph, every ◆ slot with its bound object, problems found before
    /// Play, and in Play the live state of each node. This is also the fallback when the graph window's
    /// overlay is unavailable.
    /// </summary>
    [CustomEditor(typeof(Interaction))]
    public class InteractionEditor : UnityEditor.Editor
    {
        static readonly Color k_Bad = new Color(1f, 0.42f, 0.37f);

        public override void OnInspectorGUI()
        {
            var it = (Interaction)target;
            if (KitGtkBridge.GtkFieldsBroken) EditorGUILayout.HelpBox(KitGtkBridge.GtkBrokenMessage, MessageType.Error);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("graph"), new GUIContent("Graph"));
            serializedObject.ApplyModifiedProperties();
            var g = it.Graph;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!g))
                    if (GUILayout.Button("Graph öffnen")) KitWindows.OpenGraph(g, it);
                if (GUILayout.Button("Neuer Graph …")) KitWindows.CreateGraphFor(it);
            }
            if (!g) { EditorGUILayout.HelpBox("Diese Interaktion hat noch keinen Graph.", MessageType.Info); return; }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Objekte (◆)", EditorStyles.boldLabel);
            if (g.slots.Count == 0) EditorGUILayout.LabelField("Der Graph braucht keine Szenen-Objekte.", EditorStyles.miniLabel);
            foreach (var slot in g.slots)
            {
                var type = Type.GetType(slot.typeName) ?? typeof(Object);
                var bound = it.GetBinding(slot.id);
                bool destroyed = !ReferenceEquals(bound, null) && bound == null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var users = string.Join(", ", slot.users.Select(u => g.nodes[g.IndexOf(u.Split('/')[0])]?.title).Distinct());
                    var label = new GUIContent(slot.label, $"benutzt von: {users}\nerwartet: {type.Name}");
                    // GameObject fields accept anything in the scene; component fields show the component's type.
                    var shown = Interaction.Convert(bound, type) ?? (destroyed ? null : bound);
                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.ObjectField(label, shown, type == typeof(GameObject) ? typeof(GameObject) : type, true);
                    if (EditorGUI.EndChangeCheck()) KitBinding.Set(it, slot.id, picked);
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_Search Icon", "Im Graph zeigen"), EditorStyles.iconButton, GUILayout.Width(20)))
                        KitWindows.OpenGraph(g, it, revealSlot: slot.id);
                }
                bool optional = slot.users.All(u => { var n = g.nodes[g.IndexOf(u.Split('/')[0])]; return NodeSchema.Of(n.GetType()).Ref(u.Split('/')[1])?.Optional ?? false; });
                if (destroyed) Note($"„{slot.label}“ wurde gelöscht. Neues Objekt hineinziehen oder Undo.", MessageType.Error);
                else if (bound == null && !optional) Note($"„{slot.label}“ ist leer.", MessageType.Warning);
                else if (bound != null && Interaction.Convert(bound, type) == null) Note($"„{bound.name}“ hat kein {type.Name}.", MessageType.Error);
            }

            var problems = it.Validate().Where(p => p.slot == null).ToList();
            if (problems.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Vor Play prüfen", EditorStyles.boldLabel);
                foreach (var p in problems) Note((p.node >= 0 ? g.nodes[p.node].title + ": " : "") + p.text, MessageType.Warning);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Jetzt (Play)", EditorStyles.boldLabel);
                for (int i = 0; i < it.NodeCount; i++)
                {
                    var st = it.StateOf(i);
                    var readout = it.NodeAt(i).Readout();
                    var line = $"{KitStyle.Icon(st.status)} {g.nodes[i].title}   {st.text}{(readout != null ? "   · " + readout : "")}";
                    var c = GUI.color;
                    if (st.status == NodeStatus.Failed) GUI.color = k_Bad;
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
                    GUI.color = c;
                }
                Repaint();
            }
        }

        static void Note(string text, MessageType type) => EditorGUILayout.HelpBox(text, type);
    }

    public static class KitStyle
    {
        public static readonly Color Event = new Color32(0xf0, 0xa8, 0x30, 255);
        public static readonly Color Data = new Color32(0xc9, 0xa0, 0xff, 255);
        public static readonly Color Ref = new Color32(0x5f, 0xd0, 0xb5, 255);
        public static readonly Color Running = new Color32(0xf0, 0xe4, 0x42, 255);
        public static readonly Color Failed = new Color32(0xff, 0x6b, 0x5e, 255);
        public static readonly Color Blocked = new Color32(0x9a, 0xa0, 0xa6, 255);
        public static readonly Color Done = new Color32(0xe8, 0xe2, 0xd4, 255);
        public static readonly Color Board = new Color32(0x1c, 0x2a, 0x26, 255);   // Kurs-Tafel (Richtung A)
        public static readonly Color BoardLine = new Color32(0x26, 0x36, 0x31, 255);

        public static string Icon(NodeStatus s) => s switch
        {
            NodeStatus.Received => "○",
            NodeStatus.Running => "▬",
            NodeStatus.Completed => "✓",
            NodeStatus.Blocked => "⛔",
            NodeStatus.Failed => "!",
            _ => "·",
        };

        public static Color ColorOf(NodeStatus s) => s switch
        {
            NodeStatus.Running => Running,
            NodeStatus.Failed => Failed,
            NodeStatus.Blocked => Blocked,
            NodeStatus.Received => Color.white,
            _ => Done,
        };

        public static string German(NodeStatus s) => s switch
        {
            NodeStatus.Received => "empfangen",
            NodeStatus.Running => "läuft",
            NodeStatus.Completed => "fertig",
            NodeStatus.Blocked => "blockiert",
            NodeStatus.Failed => "Fehler",
            _ => "noch nicht erreicht",
        };
    }
}
