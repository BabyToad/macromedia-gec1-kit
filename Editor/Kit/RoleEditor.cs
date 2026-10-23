using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector of the Rolle: which role this object plays in which Logik-Graph ("Rolle: Tür in Tür öffnen"), a
    /// button that opens the graph at the node, what the object senses (Zone betreten, Zusammenstoß), and in Play the
    /// live state of the nodes that use it. The Rolle has no settings: everything here is read from the Logik.
    /// </summary>
    [CustomEditor(typeof(Role))]
    public class RoleEditor : UnityEditor.Editor
    {
        static GUIStyle s_Wrap, s_Head;

        public override void OnInspectorGUI()
        {
            s_Wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
            s_Head ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            var role = (Role)target;
            var roles = KitRoles.RolesOf(role.gameObject);

            if (roles.Count == 0)
            {
                EditorGUILayout.HelpBox("Dieses Objekt spielt in keiner Logik mehr eine Rolle.", MessageType.Info);
                if (!Application.isPlaying && GUILayout.Button(new GUIContent("Rolle entfernen", "Kommt die Rolle aus einem Prefab, wird sie nur bei diesem Exemplar entfernt (Override)."))) KitRoles.RemoveIfUnused(role.gameObject, byHand: true);
            }
            foreach (var (logic, slot) in roles)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var graphName = logic.Graph.name;
                    var where = logic.gameObject == role.gameObject ? "" : $"  <color=#8a8a8a>(Logik auf „{logic.name}“)</color>";
                    EditorGUILayout.LabelField(new GUIContent($"◆ <b>{slot.label}</b> in <i>{graphName}</i>{where}",
                        "Diese Rolle spielt das Objekt im Graph. Binden und lösen in der Logik-Komponente oder auf dem Chip im Graph."), s_Head);
                    if (GUILayout.Button(new GUIContent("Graph öffnen", "Öffnet den Graph und zeigt den Knoten, der dieses Objekt benutzt."), GUILayout.Width(96)))
                        KitWindows.OpenGraph(logic.Graph, logic, revealSlot: slot.id, frame: true);
                }
                if (Application.isPlaying) LiveState(logic, slot);
            }

            Sensors(role);
            if (Application.isPlaying) Repaint();
        }

        /// <summary>The nodes that use this role, with what they are doing right now.</summary>
        static void LiveState(Logic logic, LogicGraph.Slot slot)
        {
            foreach (var user in slot.users.Select(u => u.Split('/')[0]).Distinct())
            {
                int i = logic.Graph.IndexOf(user);
                if (i < 0 || i >= logic.NodeCount) continue;
                var st = logic.StateOf(i);
                var readout = logic.NodeAt(i).Readout();
                string text = st.status == NodeStatus.Idle ? KitStyle.German(st.status) : st.text;
                EditorGUILayout.LabelField($"    {KitStyle.Icon(st.status)} {logic.Graph.nodes[i].title}: {text}{(readout != null ? " · " + readout : "")}", s_Wrap);
            }
        }

        /// <summary>Auslöser rows: which nodes listen to this object, and the last thing it sensed.</summary>
        static void Sensors(Role role)
        {
            bool play = Application.isPlaying;
            var kinds = new[] { Role.EnterText, Role.ExitText, Role.HitText, Role.HitEndText };
            if (!play)
            {
                bool trigger = role.GetComponents<Collider>().Any(c => c.isTrigger);
                bool solid = role.GetComponents<Collider>().Any(c => !c.isTrigger);
                if (trigger || solid)
                    EditorGUILayout.LabelField(trigger ? "Spürt im Play, wer die Zone betritt oder verlässt." : "Spürt im Play Zusammenstöße.", EditorStyles.miniLabel);
                return;
            }
            if (role.Listeners.Count == 0 && kinds.All(k => !role.TryGetSensed(k, out _))) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Auslöser", EditorStyles.boldLabel);
            foreach (var group in role.Listeners.GroupBy(l => l.node))
            {
                var title = (group.Key as KitNode)?.title ?? group.Key?.GetType().Name ?? "?";
                var graph = (group.Key as KitNode)?.Runner?.Graph?.name;
                EditorGUILayout.LabelField($"▶ {title}{(graph != null ? $" in <i>{graph}</i>" : "")} hört auf: {string.Join(", ", group.Select(l => l.what).Distinct())}", s_Wrap);
            }
            foreach (var k in kinds)
                if (role.TryGetSensed(k, out var s))
                    EditorGUILayout.LabelField($"    zuletzt {k}: {s.who}, vor {Time.time - s.time:0.0} s ({s.count}×)", s_Wrap);
        }
    }
}
