using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// The Werte block in the Logik Inspector: which variables and inventories this Logik uses, where they live (Logik,
    /// Szene, Spiel or an object) and – in Play – what they hold right now. Outside Play it shows what each starts with,
    /// read from the Variable component when a node is bound to one. One row per storage, not per node.
    /// </summary>
    [InitializeOnLoad]
    public static class KitLogicValuesSection
    {
        static KitLogicValuesSection() => LogicEditor.Sections.Add(Draw);

        /// <summary>One row: what it is, where it lives, and its value (live) or start value.</summary>
        public struct Row { public string key, name, where, value; }

        static void Draw(Logic logic)
        {
            bool live = Application.isPlaying && logic.isActiveAndEnabled;
            var rows = Collect(logic, live);
            if (rows.Count == 0) return;

            EditorGUILayout.Space(6);
            DrawRows(rows, live);
        }

        /// <summary>The rows of the Werte block for one Logik (also used by tests).</summary>
        public static List<Row> Collect(Logic logic, bool live)
        {
            var rows = new List<Row>();
            var graph = logic.Graph;
            if (graph == null) return rows;
            var seen = new HashSet<string>();
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                Row? row = graph.nodes[i] is VariableNode v ? VariableRow(logic, graph, i, v, live)
                         : graph.nodes[i] is Inventory inv ? InventoryRow(logic, graph, i, inv, live)
                         : (Row?)null;
                if (row.HasValue && seen.Add(row.Value.key)) rows.Add(row.Value);
            }
            return rows;
        }

        static void DrawRows(List<Row> rows, bool live)
        {
            EditorGUILayout.LabelField(new GUIContent("Werte", "Die Variablen und Inventare dieser Logik. Im Play-Modus mit ihrem aktuellen Wert, sonst mit dem Startwert."), EditorStyles.boldLabel);
            foreach (var r in rows)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(r.name, GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                    EditorGUILayout.LabelField(r.where, EditorStyles.miniLabel, GUILayout.Width(110));
                    EditorGUILayout.LabelField(r.value, live ? EditorStyles.boldLabel : EditorStyles.label);
                }
            if (live) EditorGUILayout.LabelField("Alle Werte des Spiels: Tools › " + KitBrand.Name + " › Werte", EditorStyles.miniLabel);
        }

        /// <summary>The object a ◆ field is bound to, without starting anything: inline chip, named Rolle or Dieses Objekt.</summary>
        static Object Bound(Logic logic, LogicGraph graph, int node, string field, out bool fromEvent)
        {
            fromEvent = graph.IsEventRef(node, field, out _);
            return fromEvent ? null : logic.GetBinding(graph.SlotFor(node, field));
        }

        static GameObject AsObject(Object o) => o is GameObject g ? g : o is Component c ? c.gameObject : null;

        static Row VariableRow(Logic logic, LogicGraph graph, int i, VariableNode template, bool live)
        {
            var node = live ? (VariableNode)logic.NodeAt(i) : template;
            var owner = AsObject(Bound(logic, graph, i, nameof(VariableNode.target), out bool fromEvent));
            var row = new Row { name = template.label };
            if (fromEvent)
            {
                row.key = "event|" + i;
                row.where = "mit dem Ereignis";
                row.value = live ? (node.Readout() ?? "noch keins") : "je nach Objekt";
                return row;
            }
            if (owner != null)
            {
                var component = KitVariableIndex.On(owner, template.label);
                row.key = "object|" + owner.GetInstanceID() + "|" + template.label;
                row.where = $"an „{owner.name}“";
                row.value = component == null ? $"fehlt: Add Component › Variable an „{owner.name}“"
                          : live ? (component.Value?.Display ?? component.Problem)
                          : StartOf(component.Describe());
                return row;
            }
            row.key = template.scope + "|" + template.label;
            row.where = Kit.KitValues.ScopeName(template.scope);
            row.value = live ? (node.Readout() ?? "–") : Start(template);
            return row;
        }

        static Row InventoryRow(Logic logic, LogicGraph graph, int i, Inventory template, bool live)
        {
            var node = live ? (Inventory)logic.NodeAt(i) : template;
            var bound = Bound(logic, graph, i, nameof(Inventory.inventory), out bool fromEvent);
            var owner = AsObject(bound);
            string bag = fromEvent ? "event|" + i
                       : owner != null ? "object|" + owner.GetInstanceID()
                       : template.scope + "|" + template.bagName;
            return new Row
            {
                key = "bag|" + bag + "|" + template.item,
                name = template.item,
                where = fromEvent ? "Inventar (Ereignis)" : owner != null ? $"Inventar „{owner.name}“" : $"{template.bagName} ({Kit.KitValues.ScopeName(template.scope)})",
                value = live ? (node.Readout() ?? "–") : $"braucht {template.count}",
            };
        }

        // "Lebenspunkte: 100 (0 … 100)" → "Start 100 (0 … 100)"
        static string StartOf(string describe) { int c = describe.IndexOf(": "); return c < 0 ? describe : "Start " + describe.Substring(c + 2); }

        static string Start(VariableNode v) =>
            v is NumberVariable n ? $"Start {KitValue.FormatNumber(n.start)}" + (n.limited ? $" ({KitValue.FormatNumber(n.min)} … {KitValue.FormatNumber(n.max)})" : "")
            : v is FlagVariable f ? $"Start {(f.startValue ? "ja" : "nein")}"
            : $"Start „{((TextVariable)v).start}“";
    }
}
