using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Which Interaction the graph window shows and edits bindings for. Chosen explicitly (menu in the window,
    /// "Graph öffnen" on an Interaction, or selecting an Interaction in the Hierarchy) and remembered per graph.
    /// It never changes on its own: if the chosen one is gone, the window says so.
    /// </summary>
    public static class KitInstances
    {
        static string Key(string graphPath) => "Kit.Instance." + graphPath;

        public static void Select(string graphPath, Interaction it)
        {
            if (string.IsNullOrEmpty(graphPath)) return;
            SessionState.SetString(Key(graphPath), it ? GlobalObjectId.GetGlobalObjectIdSlow(it).ToString() : "");
            SessionState.SetInt(Key(graphPath) + ".id", it ? it.GetInstanceID() : 0);
            Changed?.Invoke(graphPath);
        }

        public static event System.Action<string> Changed;

        /// <summary>The chosen instance, or null. Play mode reloads the scene, so fall back to the stable global id.</summary>
        public static Interaction Get(string graphPath)
        {
            int id = SessionState.GetInt(Key(graphPath) + ".id", 0);
            if (id != 0 && EditorUtility.InstanceIDToObject(id) is Interaction byId && byId) return byId;
            var gid = SessionState.GetString(Key(graphPath), "");
            if (!string.IsNullOrEmpty(gid) && GlobalObjectId.TryParse(gid, out var g) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) is Interaction it && it)
            {
                SessionState.SetInt(Key(graphPath) + ".id", it.GetInstanceID());
                return it;
            }
            return null;
        }

        public static bool HasChoice(string graphPath) => !string.IsNullOrEmpty(SessionState.GetString(Key(graphPath), ""));
    }

    public static class KitWindows
    {
        public static string PathOf(KitGraph g) => g ? AssetDatabase.GetAssetPath(g) : null;

        public static void OpenGraph(KitGraph g, Interaction instance, string revealSlot = null)
        {
            var path = PathOf(g);
            if (string.IsNullOrEmpty(path)) return;
            if (instance) KitInstances.Select(path, instance);
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
            if (revealSlot != null) KitGtkBridge.RevealSlot(path, revealSlot);
        }

        public static void CreateGraphFor(Interaction it)
        {
            var path = EditorUtility.SaveFilePanelInProject("Neue Interaktion", it.name, KitEditorGraph.Extension, "Wo soll der Graph liegen?");
            if (string.IsNullOrEmpty(path)) return;
            CreateGraphAt(it, path);
            OpenGraph(it.Graph, it);
        }

        /// <summary>The "Neuer Graph …" button without the file dialog.</summary>
        public static KitGraph CreateGraphAt(Interaction it, string path)
        {
            KitGraphEditing.CreateAsset(path);
            var graph = AssetDatabase.LoadAssetAtPath<KitGraph>(path);
            KitBinding.AssignGraph(it, graph);
            return graph;
        }

        [MenuItem("GameObject/Kit/Interaktion", false, 10)]
        static void CreateInteraction(MenuCommand cmd)
        {
            var go = new GameObject("Neue Interaktion");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Kit: Interaktion anlegen");
            go.AddComponent<Interaction>();
            Selection.activeGameObject = go;
        }
    }

    /// <summary>◆ marks in the Hierarchy: how many kit slots use this object. Hover for which.</summary>
    [InitializeOnLoad]
    static class KitHierarchyBadges
    {
        static Dictionary<int, List<string>> s_Uses;
        static double s_Next;

        static KitHierarchyBadges()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnItem;
            EditorApplication.hierarchyChanged += () => s_Uses = null;
            Undo.undoRedoPerformed += () => s_Uses = null;
        }

        static void Rebuild()
        {
            s_Uses = new Dictionary<int, List<string>>();
            foreach (var it in Object.FindObjectsByType<Interaction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!it.Graph) continue;
                foreach (var b in it.Bindings)
                {
                    var go = b.target as GameObject ?? (b.target as Component)?.gameObject;
                    if (!go) continue;
                    var slot = it.Graph.FindSlot(b.slot);
                    if (slot == null) continue;
                    if (!s_Uses.TryGetValue(go.GetInstanceID(), out var list)) s_Uses[go.GetInstanceID()] = list = new List<string>();
                    list.Add($"{it.name} › {slot.label}");
                }
            }
        }

        static void OnItem(int id, Rect rect)
        {
            if (s_Uses == null || EditorApplication.timeSinceStartup > s_Next) { Rebuild(); s_Next = EditorApplication.timeSinceStartup + 1; }
            if (!s_Uses.TryGetValue(id, out var uses)) return;
            var r = new Rect(rect.xMax - 26, rect.y, 26, rect.height);
            var c = GUI.color; GUI.color = KitStyle.Ref;
            GUI.Label(r, new GUIContent("◆" + uses.Count, "Kit: " + string.Join("\n", uses)), EditorStyles.miniLabel);
            GUI.color = c;
        }
    }

    /// <summary>Before Play: check every Interaction and list what would fail.</summary>
    [InitializeOnLoad]
    static class KitPlayCheck
    {
        static KitPlayCheck()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.ExitingEditMode) return;
                // Save open kit graphs first: Play runs the saved (imported) graph, and GTK would otherwise stop
                // Play with an "Unsaved Changes" dialog. This handler is registered before GTK's windows exist,
                // so it runs before GTK's own check.
                foreach (var w in KitGtkBridge.GraphWindows())
                {
                    var graph = KitGtkBridge.GraphOf(w);
                    if (graph == null) continue;
                    try
                    {
                        Unity.GraphToolkit.Editor.GraphDatabase.SaveGraphIfDirty(graph);
                        AssetDatabase.ImportAsset(Unity.GraphToolkit.Editor.GraphDatabase.GetGraphAssetPath(graph), ImportAssetOptions.ForceSynchronousImport);
                        typeof(EditorWindow).GetProperty("hasUnsavedChanges")?.SetValue(w, false);   // protected setter
                    }
                    catch (System.Exception e) { Debug.LogWarning("[Kit] Graph vor Play nicht gespeichert: " + e.Message); }
                }
                foreach (var it in Object.FindObjectsByType<Interaction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    foreach (var p in it.Validate())
                        Debug.LogWarning($"[Kit] Vor Play: {it.name} › {(p.node >= 0 && it.Graph ? it.Graph.nodes[p.node].title : "Interaktion")}: {p.text}", it);
            };
        }
    }
}
