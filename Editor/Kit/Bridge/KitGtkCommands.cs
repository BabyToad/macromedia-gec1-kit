using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kit.Editor
{
    /// <summary>
    /// Creates nodes in an open GTK window the way GTK's own item library does: through its
    /// CreateNodeCommand, so the node appears at once and Ctrl+Z removes it again. GTK 0.4 keeps all of
    /// this internal, so it is reached by reflection; <see cref="Available"/> says whether that worked.
    /// Callers fall back to "Knoten aus der Palette holen, dann Objekt auf das ◆-Feld ziehen".
    /// </summary>
    public static class KitGtkCommands
    {
        const BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Assembly s_Internal = Type.GetType("Unity.GraphToolkit.Editor.WireModel, Unity.GraphToolkit.Internal.Editor")?.Assembly;
        static readonly Assembly s_Public = typeof(Graph).Assembly;
        static readonly Type s_Command = s_Internal?.GetType("Unity.GraphToolkit.Editor.CreateNodeCommand");
        static readonly Type s_NodeData = s_Command?.GetNestedType("NodeData", k_Any);
        static readonly Type s_Item = s_Internal?.GetType("Unity.GraphToolkit.Editor.GraphNodeModelLibraryItem");
        static readonly Type s_ItemData = s_Internal?.GetType("Unity.GraphToolkit.Editor.NodeItemLibraryData");
        static readonly Type s_CreationData = s_Internal?.GetType("Unity.GraphToolkit.Editor.IGraphNodeCreationData");
        static readonly Type s_ElementModel = s_Internal?.GetType("Unity.GraphToolkit.Editor.GraphElementModel");
        static readonly MethodInfo s_FromData = s_Public.GetType("Unity.GraphToolkit.Editor.Implementation.GraphModelImp")?.GetMethod("CreateNodeFromData", k_Any);

        public static bool Available => s_Command != null && s_NodeData != null && s_Item != null && s_ItemData != null
                                        && s_CreationData != null && s_ElementModel != null && s_FromData != null;

        static object GraphViewOf(EditorWindow w) => w.GetType().GetProperty("GraphView", k_Any)?.GetValue(w);

        /// <summary>Window position (as in a mouse event) → graph canvas position.</summary>
        public static Vector2 ToCanvas(EditorWindow w, Vector2 windowPos)
        {
            var view = GraphViewOf(w) as VisualElement;
            var content = view?.GetType().GetProperty("ContentViewContainer", k_Any)?.GetValue(view) as VisualElement;
            if (content == null) return windowPos;
            return content.WorldToLocal(windowPos);
        }

        /// <summary>Canvas position of the visible centre (for nodes created from the palette button).</summary>
        public static Vector2 CanvasCentre(EditorWindow w)
        {
            var view = GraphViewOf(w) as VisualElement;
            return view == null ? Vector2.zero : ToCanvas(w, view.worldBound.center);
        }

        /// <summary>Creates one node with GTK's undoable command. Returns the new node, or null (see error).</summary>
        public static Node Create(EditorWindow w, Graph graph, Type nodeType, Vector2 canvasPos, out string error)
        {
            error = null;
            if (!Available) { error = "GTK-Interna für „Knoten anlegen“ fehlen"; return null; }
            try
            {
                var view = GraphViewOf(w);
                if (view == null) { error = "kein Graph-Fenster"; return null; }

                // createElement: data => GraphModelImp.CreateNodeFromData(data, nodeType)
                var p = Expression.Parameter(s_CreationData, "d");
                var body = Expression.Convert(Expression.Call(s_FromData, p, Expression.Constant(nodeType)), s_ElementModel);
                var funcType = typeof(Func<,>).MakeGenericType(s_CreationData, s_ElementModel);
                var create = Expression.Lambda(funcType, body, p).Compile();

                var itemData = Activator.CreateInstance(s_ItemData, k_Any, null, new object[] { nodeType }, null);
                var ctor = s_Item.GetConstructors(k_Any).First(c => c.GetParameters().Length == 3 && c.GetParameters()[2].ParameterType == funcType);
                var item = ctor.Invoke(new[] { nodeType.Name, itemData, create });

                var guid = Hash128.Compute(Guid.NewGuid().ToString());
                object data = Activator.CreateInstance(s_NodeData);
                s_NodeData.GetField("NodeLibraryItem").SetValue(data, item);
                s_NodeData.GetField("Position").SetValue(data, canvasPos);
                s_NodeData.GetField("Guid").SetValue(data, guid);
                var command = Activator.CreateInstance(s_Command);
                var list = (IList)s_Command.GetField("CreationData").GetValue(command);
                list.Add(data);
                s_Command.GetProperty("UndoString", k_Any)?.SetValue(command, "Kit: Knoten anlegen");

                var dispatch = view.GetType().GetMethods(k_Any).First(m => m.Name == "Dispatch" && m.GetParameters().Length == 2);
                dispatch.Invoke(view, new[] { command, Enum.ToObject(dispatch.GetParameters()[1].ParameterType, 0) });

                string id = guid.ToString();
                var node = graph.GetNodes().OfType<Node>().FirstOrDefault(n => KitIds.Of(n) == id);
                if (node == null) error = "GTK hat keinen Knoten angelegt";
                return node;
            }
            catch (Exception e)
            {
                error = "Knoten anlegen fehlgeschlagen: " + (e.InnerException ?? e).Message;
                return null;
            }
        }
    }
}
