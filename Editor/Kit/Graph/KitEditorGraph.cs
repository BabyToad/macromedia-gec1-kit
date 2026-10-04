using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Port marker types: they make wrong connections impossible (▶ only to ▶, ◆ only to ◆).</summary>
    [Serializable] public struct EventPort { }
    [Serializable] public struct RefPort { }

    /// <summary>
    /// One interaction graph, edited with Graph Toolkit. File extension .kit. Besides GTK's own data it
    /// stores the run order of ▶ wires that share an output (the number shown on the wire).
    /// </summary>
    [Serializable]
    [Graph(Extension)]
    public class KitEditorGraph : Graph
    {
        public const string Extension = "kit";

        [Serializable]
        public class WireOrder
        {
            public string fromNode, fromPort, toNode, toPort;
            public int order;
            public bool Matches(string fn, string fp, string tn, string tp) => fromNode == fn && fromPort == fp && toNode == tn && toPort == tp;
        }

        [SerializeField] List<WireOrder> m_WireOrders = new List<WireOrder>();
        public IReadOnlyList<WireOrder> WireOrders => m_WireOrders;

        [MenuItem("Assets/Create/Kit/Interaktion (Graph)", priority = 80)]
        static void Create() => GraphDatabase.PromptInProjectBrowserToCreateNewAsset<KitEditorGraph>("Neue Interaktion");

        public override void OnGraphChanged(GraphLogger log)
        {
            base.OnGraphChanged(log);
            SyncWireOrders();
            KitGraphCheck.Run(this, log);
        }

        /// <summary>Order number of one ▶ wire (1 = runs first). New wires get the next free number.</summary>
        public int OrderOf(string fn, string fp, string tn, string tp)
        {
            var w = m_WireOrders.Find(x => x.Matches(fn, fp, tn, tp));
            return w?.order ?? 0;
        }

        /// <summary>Moves a wire to a new place in its output's order; the others shift.</summary>
        public void SetOrder(string fn, string fp, string tn, string tp, int newOrder)
        {
            var group = m_WireOrders.Where(x => x.fromNode == fn && x.fromPort == fp).OrderBy(x => x.order).ToList();
            var me = group.Find(x => x.Matches(fn, fp, tn, tp));
            if (me == null) return;
            group.Remove(me);
            group.Insert(Mathf.Clamp(newOrder - 1, 0, group.Count), me);
            for (int i = 0; i < group.Count; i++) group[i].order = i + 1;
        }

        /// <summary>Keeps the order table in step with the wires that actually exist.</summary>
        public void SyncWireOrders()
        {
            var live = new List<(string fn, string fp, string tn, string tp)>();
            foreach (var n in GetNodes().OfType<KitEditorNode>())
            {
                string fn = KitIds.Of(n);
                foreach (var port in n.GetOutputPorts())
                {
                    if (port.dataType != typeof(EventPort)) continue;
                    var connected = new List<IPort>();
                    port.GetConnectedPorts(connected);
                    foreach (var c in connected)
                        if (c.GetNode() is Node target) live.Add((fn, port.name, KitIds.Of(target), c.name));
                }
            }
            m_WireOrders.RemoveAll(w => !live.Contains((w.fromNode, w.fromPort, w.toNode, w.toPort)));
            foreach (var l in live)
            {
                if (m_WireOrders.Exists(w => w.Matches(l.fn, l.fp, l.tn, l.tp))) continue;
                int next = m_WireOrders.Where(w => w.fromNode == l.fn && w.fromPort == l.fp).Select(w => w.order).DefaultIfEmpty(0).Max() + 1;
                m_WireOrders.Add(new WireOrder { fromNode = l.fn, fromPort = l.fp, toNode = l.tn, toPort = l.tp, order = next });
            }
            // close gaps: 1, 2, 3 …
            foreach (var g in m_WireOrders.GroupBy(w => (w.fromNode, w.fromPort)))
            {
                int i = 1;
                foreach (var w in g.OrderBy(w => w.order)) w.order = i++;
            }
        }
    }
}
