using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Turns a .kit graph into the playable <see cref="KitGraph"/> that an Interaction runs.</summary>
    [ScriptedImporter(3, KitEditorGraph.Extension)]
    public class KitImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<KitEditorGraph>(ctx.assetPath);
            if (graph == null) { ctx.LogImportError($"{ctx.assetPath}: kein Kit-Graph"); return; }
            var result = KitGraphBuilder.Build(graph, out var problems);
            foreach (var p in problems) ctx.LogImportWarning($"{System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath)}: {p}");
            result.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("graph", result);
            ctx.SetMainObject(result);
        }
    }

    /// <summary>Editor graph → runtime graph. Also used by the checks while editing.</summary>
    public static class KitGraphBuilder
    {
        public static KitGraph Build(KitEditorGraph graph, out List<string> problems)
        {
            problems = new List<string>();
            graph.SyncWireOrders();
            var rt = ScriptableObject.CreateInstance<KitGraph>();
            var kitNodes = graph.GetNodes().OfType<KitEditorNode>().ToList();
            var index = new Dictionary<INode, int>();
            var objectLabels = new Dictionary<string, string>();

            foreach (var n in kitNodes)
            {
                var node = (KitNode)System.Activator.CreateInstance(n.RuntimeType);
                node.id = KitIds.Of(n);
                node.title = n.Schema.Title;
                foreach (var s in n.Schema.Settings)
                {
                    var v = n.GetSetting(s);
                    if (v != null) s.Set(node, v);
                }
                foreach (var d in n.Schema.DataIns)
                {
                    var port = n.GetInputPortByName(d.Id);
                    if (port != null && !port.isConnected) { var v = KitValues.Read(port, d.Type); if (v != null) d.Set(node, v); }
                }
                index[n] = rt.Add(node);
            }
            foreach (var o in graph.GetNodes().OfType<ObjectNode>()) objectLabels[KitIds.Of(o)] = o.Label;

            var connected = new List<IPort>();
            foreach (var n in kitNodes)
            {
                int to = index[n];
                foreach (var port in n.GetInputPorts())
                {
                    connected.Clear();
                    port.GetConnectedPorts(connected);
                    foreach (var src in connected)
                    {
                        var srcNode = src.GetNode();
                        if (port.dataType == typeof(EventPort)) continue;   // ▶ wires are read from the outputs below
                        if (port.dataType == typeof(RefPort))
                        {
                            if (srcNode is ObjectNode on) rt.ConnectSlot(KitIds.Of(on), to, port.name);
                            else if (index.TryGetValue(srcNode, out int from)) rt.ConnectEventRef(from, src.name, to, port.name);
                        }
                        else if (index.TryGetValue(srcNode, out int from)) rt.ConnectData(from, src.name, to, port.name);
                        else problems.Add($"„{n.Schema.Title}“ ● {port.displayName}: Quelle ist kein Kit-Knoten (Variablen/Konstanten werden noch nicht unterstützt)");
                    }
                }
                foreach (var port in n.GetOutputPorts())
                {
                    if (port.dataType != typeof(EventPort)) continue;
                    connected.Clear();
                    port.GetConnectedPorts(connected);
                    foreach (var dst in connected)
                    {
                        if (!index.TryGetValue(dst.GetNode(), out int target)) continue;
                        int order = graph.OrderOf(KitIds.Of(n), port.name, KitIds.Of((Node)dst.GetNode()), dst.name);
                        rt.wires.Add(new KitGraph.Wire { from = to, fromPort = port.name, to = target, toPort = dst.name, order = order });
                    }
                }
            }
            rt.RebuildSlots(objectLabels);
            for (int i = 0; i < rt.nodes.Count; i++)
                foreach (var r in NodeSchema.Of(rt.nodes[i].GetType()).Refs)
                    if (r.Permanent && rt.IsEventRef(i, r.Id, out var l))
                        problems.Add($"„{rt.nodes[i].title}“: " + Interaction.PermanentProblem(r.Label, rt.nodes[l.from].title));
            problems.AddRange(KitGraphCheck.DataCycles(rt));
            return rt;
        }
    }

    /// <summary>Checks shown in the graph window's error bar while editing.</summary>
    public static class KitGraphCheck
    {
        public static void Run(KitEditorGraph graph, GraphLogger log)
        {
            foreach (var o in graph.GetNodes().OfType<ObjectNode>())
            {
                var outPort = o.GetOutputPortByName(ObjectNode.Out);
                if (outPort != null && !outPort.isConnected) log.LogWarning("Objekt-Knoten ist mit nichts verbunden.", o);
            }
            var rt = KitGraphBuilder.Build(graph, out var problems);
            foreach (var p in problems) log.LogError(p, graph);
            Object.DestroyImmediate(rt);
        }

        /// <summary>● wires that run in a circle can never be read. Lists each circle once.</summary>
        public static IEnumerable<string> DataCycles(KitGraph g)
        {
            var edges = g.links.Where(l => l.kind == KitGraph.LinkKind.Data).ToList();
            var state = new int[g.nodes.Count];   // 0 unseen, 1 on stack, 2 done
            var found = new List<string>();
            void Visit(int n)
            {
                state[n] = 1;
                foreach (var e in edges.Where(e => e.to == n))
                {
                    if (state[e.from] == 1) found.Add($"● Kreis: „{g.nodes[e.from].title}“ und „{g.nodes[n].title}“ lesen sich gegenseitig");
                    else if (state[e.from] == 0) Visit(e.from);
                }
                state[n] = 2;
            }
            for (int i = 0; i < g.nodes.Count; i++) if (state[i] == 0) Visit(i);
            return found;
        }
    }
}
