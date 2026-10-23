using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector of the Logik component: its Logik-Graph (Neu · Graph öffnen), every role with the object that plays
    /// it ("Dieses Objekt" first, fixed), problems found before Play, and in Play the live state of each node. Also
    /// the fallback when the graph window's overlay is unavailable.
    /// </summary>
    [CustomEditor(typeof(Logic))]
    [CanEditMultipleObjects]
    public class LogicEditor : UnityEditor.Editor
    {
        static readonly Color k_Bad = new Color(1f, 0.42f, 0.37f);

        /// <summary>
        /// Extra blocks other parts of the kit draw below the roles (e.g. the Werte lane's Szene/Spiel values), so this
        /// Inspector need not know them. Each is called with the Logik being shown, inside the Inspector's layout.
        /// </summary>
        public static readonly System.Collections.Generic.List<Action<Logic>> Sections = new System.Collections.Generic.List<Action<Logic>>();
        static GUIStyle s_Wrap;

        void OnEnable()
        {
            // Objects bound before v0.4 (or by hand in the debug Inspector) get their Rolle as soon as one looks.
            if (!Application.isPlaying)
                foreach (var t in targets) KitRoles.EnsureAll(t as Logic);
        }

        public override void OnInspectorGUI()
        {
            s_Wrap ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
            if (KitGtkBridge.GtkFieldsBroken) EditorGUILayout.HelpBox(KitGtkBridge.GtkBrokenMessage, MessageType.Error);
            serializedObject.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("graph"), new GUIContent("Logik-Graph",
                    "Der Graph, den diese Logik abspielt: welche Auslöser was bewirken.\n" +
                    "Mehrere Objekte können denselben Graph benutzen; jedes hat dann seinen eigenen Zustand."));
            }
            serializedObject.ApplyModifiedProperties();
            if (targets.Length > 1) { EditorGUILayout.HelpBox("Mehrere Logik-Komponenten ausgewählt: Rollen bitte einzeln binden.", MessageType.None); return; }

            var it = (Logic)target;
            var g = it.Graph;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Neu", "Legt einen neuen Logik-Graph an (in Assets/Logik), mit „Dieses Objekt“ schon drin, und öffnet ihn.")))
                    EditorApplication.delayCall += () => { if (it) KitWindows.CreateGraphFor(it); };
                using (new EditorGUI.DisabledScope(!g))
                    if (GUILayout.Button(new GUIContent("Graph öffnen", "Öffnet den Graph-Editor für diese Logik. Im Play-Modus zeigt er, was gerade passiert.")))
                        KitWindows.OpenGraph(g, it);
            }
            if (!g)
            {
                EditorGUILayout.HelpBox("Diese Logik hat noch keinen Graph. „Neu“ legt einen an, oder einen vorhandenen Logik-Graph aus dem Project-Fenster in das Feld ziehen.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(new GUIContent("Rollen", "Welches Objekt der Szene welche Rolle im Graph spielt (◆). Objekte aus der Hierarchy in die Felder ziehen."), EditorStyles.boldLabel);
            if (g.slots.Count == 0) EditorGUILayout.LabelField("Der Graph braucht keine Objekte aus der Szene.", EditorStyles.miniLabel);
            var noted = new System.Collections.Generic.HashSet<string>();
            foreach (var slot in g.slots.OrderBy(s => s.id == LogicGraph.SelfSlot ? 0 : 1))
                if (RoleRow(it, g, slot)) noted.Add(slot.id);

            foreach (var section in Sections)
            {
                try { section(it); }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Everything else Validate finds, incl. a role whose object lacks a component only a second node needs.
            var problems = it.Validate().Where(p => p.slot == null || !noted.Contains(p.slot)).ToList();
            if (problems.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Vor Play prüfen", EditorStyles.boldLabel);
                foreach (var p in problems) EditorGUILayout.HelpBox((p.node >= 0 ? g.nodes[p.node].title + ": " : "") + p.text, MessageType.Warning);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Jetzt (Play)", EditorStyles.boldLabel);
                for (int i = 0; i < it.NodeCount; i++) StateLine(it, i);
                Repaint();
            }
        }

        /// <summary>One role: label, object field, search button, and a note under it if something is wrong. True if it noted a problem.</summary>
        static bool RoleRow(Logic it, LogicGraph g, LogicGraph.Slot slot)
        {
            var type = Type.GetType(slot.typeName) ?? typeof(Object);
            bool self = slot.id == LogicGraph.SelfSlot;
            var bound = it.GetBinding(slot.id);
            bool destroyed = !ReferenceEquals(bound, null) && bound == null;
            using (new EditorGUILayout.HorizontalScope())
            {
                var users = string.Join(", ", slot.users.Select(u => g.nodes[g.IndexOf(u.Split('/')[0])]?.title).Distinct());
                var tip = self ? "Das Objekt, auf dem diese Logik sitzt (im Code: gameObject). Steht fest.\nbenutzt von: " + users
                               : $"benutzt von: {users}\nerwartet: {German(type)}";
                var label = new GUIContent((self ? "◆ " : "◆ ") + slot.label, tip);
                var shown = Logic.Convert(bound, type) ?? (destroyed ? null : bound);
                using (new EditorGUI.DisabledScope(self))
                {
                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.ObjectField(label, self ? it.gameObject : shown, type == typeof(GameObject) ? typeof(GameObject) : type, true);
                    if (EditorGUI.EndChangeCheck() && !self) KitBinding.Set(it, slot.id, picked);
                }
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_Search Icon", "Im Graph zeigen"), EditorStyles.iconButton, GUILayout.Width(20)))
                    KitWindows.OpenGraph(g, it, revealSlot: slot.id);
            }
            if (self) return false;
            bool optional = slot.users.All(u => { var n = g.nodes[g.IndexOf(u.Split('/')[0])]; return NodeSchema.Of(n.GetType()).Ref(u.Split('/')[1])?.Optional ?? false; });
            if (destroyed) EditorGUILayout.HelpBox($"„{slot.label}“: das Objekt wurde gelöscht. Ein neues hineinziehen oder Strg+Z.", MessageType.Error);
            else if (bound == null && !optional) EditorGUILayout.HelpBox($"„{slot.label}“ ist leer: ein Objekt aus der Hierarchy hineinziehen.", MessageType.Warning);
            else if (bound != null && Logic.Convert(bound, type) == null) EditorGUILayout.HelpBox($"„{bound.name}“ hat kein {German(type)}.", MessageType.Error);
            else return false;
            return true;
        }

        void StateLine(Logic it, int i)
        {
            var st = it.StateOf(i);
            var readout = it.NodeAt(i).Readout();
            var line = $"{KitStyle.Icon(st.status)} <b>{it.Graph.nodes[i].title}</b>   {st.text}{(readout != null ? "   · " + readout : "")}";
            var c = GUI.color;
            if (st.status == NodeStatus.Failed) GUI.color = k_Bad;
            EditorGUILayout.LabelField(line, s_Wrap);
            GUI.color = c;
        }

        /// <summary>A component type as students know it from the Inspector ("Collider", "Transform").</summary>
        public static string German(Type t) => t == typeof(GameObject) ? "Objekt" : ObjectNames.NicifyVariableName(t.Name);
    }
}
