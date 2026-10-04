using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kit.Editor
{
    /// <summary>
    /// Makes the Graph Toolkit window the kit's one canvas for building AND watching:
    /// German titles, ◆ chips to bind scene objects, live state per node (received / running / done /
    /// blocked / failed, with the reason), wire colours by kind and run-order numbers, an instance picker,
    /// an info bar for the selected node or wire, double-click to reveal in Hierarchy/Inspector, and
    /// "Hierarchy selection → which nodes touch this object".
    ///
    /// GTK 0.4 has no public API for any of this, so it reads internal view types by reflection
    /// (pinned to 6.3 for the semester). If those types are missing, the window shows a notice and the
    /// Interaction inspector carries the same information.
    /// </summary>
    [InitializeOnLoad]
    public static partial class KitGtkBridge
    {
        /// <summary>Extra console lines about selection highlighting (set by the studio's screenshot driver).</summary>
        internal static bool Verbose;

        const BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type s_ModelView = Type.GetType("Unity.GraphToolkit.Editor.ModelView, Unity.GraphToolkit.Internal.Editor");
        static readonly Type s_GraphElement = Type.GetType("Unity.GraphToolkit.Editor.GraphElement, Unity.GraphToolkit.Internal.Editor");
        static readonly Type s_Wire = Type.GetType("Unity.GraphToolkit.Editor.Wire, Unity.GraphToolkit.Internal.Editor");
        static readonly PropertyInfo s_ModelProp = s_ModelView?.GetProperty("Model", k_Any);
        static readonly MethodInfo s_IsSelected = s_GraphElement?.GetMethod("IsSelected", k_Any);
        public static bool ReflectionOk => s_ModelView != null && s_Wire != null && s_ModelProp != null;

        /// <summary>
        /// GTK 0.4 looks up UIToolkit's RegisterCallback&lt;TEvent,TArgs&gt; with SingleOrDefault. From Unity 6000.3.16 on
        /// there are two such overloads, and every node with settings fails to draw. True = this editor has that problem.
        /// </summary>
        public static bool GtkFieldsBroken =>
            typeof(CallbackEventHandler).GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                .Count(m => m.Name == nameof(CallbackEventHandler.RegisterCallback) && m.GetGenericArguments().Length == 2) > 1;

        public const string GtkBrokenMessage =
            "Diese Unity-Version passt nicht zum Graph-Werkzeug: Knoten mit Einstellungen werden nicht gezeichnet. " +
            "Für den Kurs Unity 6000.3.15f1 verwenden (Unity Hub › Installs). Spielen funktioniert trotzdem; " +
            "Objekte lassen sich im Inspector der Interaktion binden.";

        static readonly Dictionary<EditorWindow, WindowView> s_Windows = new Dictionary<EditorWindow, WindowView>();
        static double s_Next;
        static StyleSheet s_Style;

        static KitGtkBridge()
        {
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
            KitInstances.Changed += _ => { foreach (var w in s_Windows.Values) w.InstanceChanged(); };
        }

        public static IEnumerable<EditorWindow> GraphWindows() =>
            Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w && w.GetType().Name.StartsWith("GraphViewEditorWindow"));

        static void Update()
        {
            if (EditorApplication.timeSinceStartup < s_Next) return;
            s_Next = EditorApplication.timeSinceStartup + 1.0 / 15;
            foreach (var dead in s_Windows.Keys.Where(k => !k).ToList()) s_Windows.Remove(dead);
            foreach (var w in GraphWindows())
            {
                var graph = GraphOf(w);
                if (graph == null) continue;
                if (!s_Windows.TryGetValue(w, out var view) || view.Root != w.rootVisualElement)
                    s_Windows[w] = view = new WindowView(w, w.rootVisualElement);
                try { view.Refresh(graph); }
                catch (Exception e) { view.ShowNotice("Kit-Anzeige gestört: " + e.Message); Debug.LogException(e); }
            }
        }

        /// <summary>The KitEditorGraph a GTK window shows (window.GraphTool.ToolState.GraphModel.Graph).</summary>
        internal static KitEditorGraph GraphOf(EditorWindow w)
        {
            try
            {
                var tool = w.GetType().GetProperty("GraphTool", k_Any)?.GetValue(w);
                var state = tool?.GetType().GetProperty("ToolState", k_Any)?.GetValue(tool);
                var model = state?.GetType().GetProperty("GraphModel", k_Any)?.GetValue(state);
                return model?.GetType().GetProperty("Graph", k_Any)?.GetValue(model) as KitEditorGraph;
            }
            catch { return null; }
        }

        internal static object ModelOf(VisualElement ve) => ve != null && s_ModelView != null && s_ModelView.IsInstanceOfType(ve) ? s_ModelProp.GetValue(ve) : null;
        internal static object Get(object o, string prop) => o?.GetType().GetProperty(prop, k_Any)?.GetValue(o);
        internal static bool IsSelected(VisualElement ve) => s_IsSelected != null && s_GraphElement.IsInstanceOfType(ve) && (bool)s_IsSelected.Invoke(ve, null);
        internal static bool IsWire(VisualElement ve) => s_Wire != null && s_Wire.IsInstanceOfType(ve);

        internal static StyleSheet Style => s_Style ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("KitGraph t:StyleSheet").FirstOrDefault() ?? ""));

        static void OnSelectionChanged()
        {
            foreach (var view in s_Windows.Values) view.HierarchySelectionChanged();
        }

        /// <summary>The kit overlay of an open graph window (null if none yet).</summary>
        internal static WindowView ViewFor(EditorWindow w) => w != null && s_Windows.TryGetValue(w, out var v) ? v : null;

        /// <summary>Called from the Interaction inspector's search button.</summary>
        public static void RevealSlot(string graphPath, string slot)
        {
            EditorApplication.delayCall += () =>
            {
                foreach (var v in s_Windows.Values.Where(v => v.Path == graphPath)) v.FlashSlot(slot);
            };
        }
    }
}
