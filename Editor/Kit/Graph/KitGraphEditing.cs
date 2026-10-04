using System;
using System.Collections;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Builds and edits .kit graphs from code: add nodes, set settings, connect ports. GTK 0.4 has no public
    /// API for this (6.5 adds Graph.AddNode/Connect), so this goes through internal types by reflection.
    /// Used by the hand-build test and by "drag an object from the Hierarchy onto the canvas".
    /// <see cref="Available"/> is false when the internals are missing; callers must then fall back.
    /// </summary>
    public static class KitGraphEditing
    {
        const BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly FieldInfo s_GraphImpl = typeof(Graph).GetField("m_Implementation", k_Any);

        public static bool Available
        {
            get
            {
                try { return s_GraphImpl != null && s_GraphImpl.FieldType.GetMethod("CreateNodeModel", k_Any) != null; }
                catch { return false; }
            }
        }

        public static KitEditorGraph CreateAsset(string path)
        {
            var g = GraphDatabase.CreateGraph<KitEditorGraph>(path);
            AssetDatabase.ImportAsset(path);
            return GraphDatabase.LoadGraph<KitEditorGraph>(path) ?? g;
        }

        static object ModelOf(Graph g) => s_GraphImpl.GetValue(g);

        /// <summary>The asset object GTK saves (needed for Undo.RecordObject).</summary>
        public static UnityEngine.Object GraphObjectOf(Graph g)
        {
            try { var m = ModelOf(g); return m?.GetType().GetProperty("GraphObject", k_Any)?.GetValue(m) as UnityEngine.Object; }
            catch { return null; }
        }

        public static T AddNode<T>(Graph g, Vector2 position) where T : Node, new() => (T)AddNode(g, typeof(T), position);

        /// <summary>With a fixed node id: the same graph built twice gets the same ids, so slot ids and scene bindings match.</summary>
        public static T AddNode<T>(Graph g, Vector2 position, Hash128 id) where T : Node, new() => (T)AddNode(g, typeof(T), position, id);

        public static Node AddNode(Graph g, Type nodeType, Vector2 position) => AddNode(g, nodeType, position, default);

        public static Node AddNode(Graph g, Type nodeType, Vector2 position, Hash128 id)
        {
            var model = ModelOf(g);
            var create = model.GetType().GetMethod("CreateNodeModel", k_Any, null, new[] { typeof(Node), typeof(Vector2) }, null);
            var nodeModel = create.Invoke(model, new object[] { Activator.CreateInstance(nodeType), position });
            if (id.isValid)
            {
                // The graph indexes its elements by id: take the node out, give it the id, put it back.
                var t = model.GetType();
                var unregister = t.GetMethod("UnregisterElement", k_Any);
                var register = t.GetMethod("RegisterElement", k_Any);
                var setGuid = nodeModel.GetType().GetMethod("SetGuid", k_Any, null, new[] { typeof(Hash128) }, null);
                if (unregister == null || register == null || setGuid == null) throw new MissingMethodException("Graph Toolkit: node ids cannot be set");
                unregister.Invoke(model, new[] { nodeModel });
                setGuid.Invoke(nodeModel, new object[] { id });
                register.Invoke(model, new[] { nodeModel });
            }
            return (Node)nodeModel.GetType().GetProperty("Node", k_Any).GetValue(nodeModel);
        }

        /// <summary>Sets a node setting (GTK "option").</summary>
        public static void SetOption(Node n, string optionName, object value)
        {
            object option = n.GetNodeOptionByName(optionName);
            if (option == null) throw new ArgumentException($"{n.GetType().Name} has no option {optionName}");
            var port = option.GetType().GetProperty("PortModel", k_Any).GetValue(option);
            SetEmbedded(port, value);
        }

        /// <summary>Sets the constant typed into an unconnected ● input.</summary>
        public static void SetInputValue(Node n, string portId, object value) => SetEmbedded(PortModel(n, portId, input: true), value);

        static void SetEmbedded(object portModel, object value)
        {
            var constant = portModel.GetType().GetProperty("EmbeddedValue", k_Any).GetValue(portModel);
            constant.GetType().GetProperty("ObjectValue", k_Any).SetValue(constant, value);
        }

        static object PortModel(Node n, string portId, bool input)
        {
            // The public IPort GTK hands out is the internal PortModel itself.
            object port = input ? n.GetInputPortByName(portId) : n.GetOutputPortByName(portId);
            if (port == null) throw new ArgumentException($"{n.GetType().Name} has no {(input ? "input" : "output")} {portId}");
            return port;
        }

        /// <summary>GTK's own rule for whether a wire may be drawn between two ports (the same check the mouse uses).</summary>
        public static bool IsCompatible(Graph g, Node from, string outPort, Node to, string inPort)
        {
            var model = ModelOf(g);
            var check = model.GetType().GetMethod("IsCompatiblePort", k_Any);
            return (bool)check.Invoke(model, new[] { PortModel(from, outPort, input: false), PortModel(to, inPort, input: true) });
        }

        public static void Connect(Graph g, Node from, string outPort, Node to, string inPort)
        {
            var model = ModelOf(g);
            var fromPort = PortModel(from, outPort, input: false);
            var toPort = PortModel(to, inPort, input: true);
            if (!IsCompatible(g, from, outPort, to, inPort))
                throw new ArgumentException($"GTK refuses {from.GetType().Name}.{outPort} → {to.GetType().Name}.{inPort} (port types)");
            var create = Array.Find(model.GetType().GetMethods(k_Any), m => m.Name == "CreateWire" && m.GetParameters().Length == 3);
            create.Invoke(model, new[] { toPort, fromPort, (object)default(Hash128) });
        }

        /// <summary>Writes the graph to disk and re-imports it (which also runs OnGraphChanged-style syncing).</summary>
        public static void Save(Graph g)
        {
            var model = ModelOf(g);
            var graphObject = model.GetType().GetProperty("GraphObject", k_Any).GetValue(model);
            if (g is KitEditorGraph kg) kg.SyncWireOrders();
            EditorUtility.SetDirty((UnityEngine.Object)graphObject);
            graphObject.GetType().GetMethod("Save", k_Any, null, Type.EmptyTypes, null).Invoke(graphObject, null);
            var path = GraphDatabase.GetGraphAssetPath(g);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
