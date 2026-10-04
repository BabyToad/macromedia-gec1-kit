using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// The playable form of one interaction graph. The editor's importer builds it from the graph file;
    /// tests can build it in code. It holds node templates (with their settings), wires and binding slots.
    /// It never holds scene objects: those live in the scene, in an <see cref="Interaction"/>'s binding table.
    /// </summary>
    [CreateAssetMenu(menuName = "Kit/Leerer Laufzeit-Graph (nur Tests)", order = 2000)]
    public class KitGraph : ScriptableObject
    {
        [SerializeReference] public List<KitNode> nodes = new List<KitNode>();
        /// <summary>▶ wires. Several per output are allowed; <see cref="Wire.order"/> is the order they run in.</summary>
        public List<Wire> wires = new List<Wire>();
        /// <summary>● and ◆ wires.</summary>
        public List<Link> links = new List<Link>();
        /// <summary>Everything the graph needs from the scene.</summary>
        public List<Slot> slots = new List<Slot>();

        [Serializable]
        public struct Wire
        {
            public int from; public string fromPort;
            public int to; public string toPort;
            public int order;  // 1, 2, 3 … per output
            public override string ToString() => $"{from}.{fromPort} → {to}.{toPort} (#{order})";
        }

        public enum LinkKind { Data, RefFromSlot, RefFromEvent }

        [Serializable]
        public struct Link
        {
            public LinkKind kind;
            public int from; public string fromPort;   // Data / RefFromEvent: source node and port
            public string slot;                        // RefFromSlot: slot id (an "Objekt" node)
            public int to; public string toField;
        }

        [Serializable]
        public class Slot
        {
            public string id;        // node id + "/" + field for inline chips, or the "Objekt" node id
            public string label;     // what the editor shows ("Tür")
            public string typeName;  // expected type (assembly-qualified) for validation
            public List<string> users = new List<string>();  // "node id/field" that read this slot
        }

        public int IndexOf(string nodeId) => nodes.FindIndex(n => n != null && n.id == nodeId);
        public Slot FindSlot(string id) => slots.Find(s => s.id == id);

        /// <summary>Slot used by a ◆ field: the "Objekt" node it is wired to, or the node's own inline chip.</summary>
        public string SlotFor(int node, string field)
        {
            foreach (var l in links)
                if (l.to == node && l.toField == field && l.kind == LinkKind.RefFromSlot) return l.slot;
            return InlineSlotId(nodes[node].id, field);
        }

        public bool IsEventRef(int node, string field, out Link link)
        {
            foreach (var l in links)
                if (l.to == node && l.toField == field && l.kind == LinkKind.RefFromEvent) { link = l; return true; }
            link = default; return false;
        }

        public static string InlineSlotId(string nodeId, string field) => nodeId + "/" + field;

        /// <summary>Wires leaving one output, in run order.</summary>
        public List<int> WiresFrom(int node, string port)
        {
            var list = new List<int>();
            for (int i = 0; i < wires.Count; i++) if (wires[i].from == node && wires[i].fromPort == port) list.Add(i);
            list.Sort((a, b) => wires[a].order.CompareTo(wires[b].order));
            return list;
        }

        /// <summary>Builder used by tests and by the importer.</summary>
        public int Add(KitNode n) { nodes.Add(n); return nodes.Count - 1; }

        public void Connect(int from, string fromPort, int to, string toPort)
        {
            int order = 1;
            foreach (var w in wires) if (w.from == from && w.fromPort == fromPort) order++;
            wires.Add(new Wire { from = from, fromPort = fromPort, to = to, toPort = toPort, order = order });
        }

        public void ConnectData(int from, string fromPort, int to, string toField) =>
            links.Add(new Link { kind = LinkKind.Data, from = from, fromPort = fromPort, to = to, toField = toField });

        public void ConnectEventRef(int from, string fromPort, int to, string toField) =>
            links.Add(new Link { kind = LinkKind.RefFromEvent, from = from, fromPort = fromPort, to = to, toField = toField });

        public void ConnectSlot(string slotId, int to, string toField) =>
            links.Add(new Link { kind = LinkKind.RefFromSlot, slot = slotId, to = to, toField = toField });

        /// <summary>Recomputes <see cref="slots"/> from nodes and links. Keeps labels already set.</summary>
        public void RebuildSlots(Dictionary<string, string> objectNodeLabels = null)
        {
            var old = new Dictionary<string, Slot>();
            foreach (var s in slots) old[s.id] = s;
            slots.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                var schema = NodeSchema.Of(nodes[i].GetType());
                foreach (var r in schema.Refs)
                {
                    if (IsEventRef(i, r.Id, out _)) continue;
                    var id = SlotFor(i, r.Id);
                    var slot = slots.Find(s => s.id == id);
                    if (slot == null)
                    {
                        string label = null;
                        if (objectNodeLabels != null) objectNodeLabels.TryGetValue(id, out label);
                        if (string.IsNullOrEmpty(label) && old.TryGetValue(id, out var o)) label = o.label;
                        if (string.IsNullOrEmpty(label)) label = $"{nodes[i].title}: {r.Label}";
                        slot = new Slot { id = id, label = label, typeName = r.Type.AssemblyQualifiedName };
                        slots.Add(slot);
                    }
                    slot.users.Add(InlineSlotId(nodes[i].id, r.Id));
                }
            }
        }
    }
}
