using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Which Logic the graph window shows and edits bindings for. Chosen explicitly (menu in the window,
    /// "Graph öffnen" on a Logic, or selecting a Logic in the Hierarchy) and remembered per graph.
    /// It never changes on its own: if the chosen one is gone, the window says so.
    /// </summary>
    public static class KitInstances
    {
        static string Key(string graphPath) => "Kit.Instance." + graphPath;

        public static void Select(string graphPath, Logic it)
        {
            if (string.IsNullOrEmpty(graphPath)) return;
            SessionState.SetString(Key(graphPath), it ? GlobalObjectId.GetGlobalObjectIdSlow(it).ToString() : "");
            SessionState.SetInt(Key(graphPath) + ".id", it ? it.GetInstanceID() : 0);
            Changed?.Invoke(graphPath);
        }

        public static event System.Action<string> Changed;

        /// <summary>The chosen instance, or null. Play mode reloads the scene, so fall back to the stable global id.</summary>
        public static Logic Get(string graphPath)
        {
            int id = SessionState.GetInt(Key(graphPath) + ".id", 0);
            if (id != 0 && EditorUtility.InstanceIDToObject(id) is Logic byId && byId) return byId;
            var gid = SessionState.GetString(Key(graphPath), "");
            if (!string.IsNullOrEmpty(gid) && GlobalObjectId.TryParse(gid, out var g) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) is Logic it && it)
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
        public static string PathOf(LogicGraph g) => g ? AssetDatabase.GetAssetPath(g) : null;

        /// <summary>
        /// Opens the graph for this Logik. <paramref name="revealSlot"/> lights up the nodes using that role;
        /// <paramref name="frame"/> also scrolls the canvas to the first of them and selects it.
        /// </summary>
        public static void OpenGraph(LogicGraph g, Logic instance, string revealSlot = null, bool frame = false)
        {
            var path = PathOf(g);
            if (string.IsNullOrEmpty(path)) return;
            if (instance) KitInstances.Select(path, instance);
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
            if (revealSlot != null) KitGtkBridge.RevealSlot(path, revealSlot, frame);
        }

        /// <summary>
        /// "Neu" on the Logik: a new Logik-Graph named after the object, in <see cref="KitPaths.LogicFolder"/>, with
        /// "Dieses Objekt" already placed; assigned and opened. No file dialog: renaming happens in the Project window.
        /// </summary>
        public static void CreateGraphFor(Logic it)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"{KitPaths.EnsureFolder(KitPaths.LogicFolder)}/{SafeName(it.name)}.{KitEditorGraph.Extension}");
            var graph = CreateGraphAt(it, path);
            EditorGUIUtility.PingObject(graph);
            OpenGraph(graph, it);
        }

        /// <summary>Creates the graph file at exactly this path (with "Dieses Objekt") and gives it to the Logik.</summary>
        public static LogicGraph CreateGraphAt(Logic it, string path)
        {
            SeedSelf(KitGraphEditing.CreateAsset(path));
            var graph = AssetDatabase.LoadAssetAtPath<LogicGraph>(path);
            KitBinding.AssignGraph(it, graph);
            return graph;
        }

        /// <summary>Puts a "Dieses Objekt" node into a brand-new graph (every way of making a graph does this).</summary>
        public static void SeedSelf(KitEditorGraph g)
        {
            if (g == null || !KitGraphEditing.Available || g.GetNodes().OfType<SelfNode>().Any()) return;
            try { KitGraphEditing.AddNode<SelfNode>(g, new Vector2(-260, 0)); KitGraphEditing.Save(g); }
            catch (System.Exception e) { Debug.LogWarning(KitBrand.Log + "„Dieses Objekt“ nicht angelegt (über + Knoten holen): " + e.Message); }
        }

        static string SafeName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Logik" : name.Trim();
        }

        /// <summary>
        /// GameObject › Kit › Logik: puts a Logik on the selected object (or, with nothing selected, on a new empty
        /// object). Unity calls this once per selected object.
        /// </summary>
        [MenuItem(KitBrand.GameObjectMenu + "Logik", false, 10)]
        static void AddLogicMenu(MenuCommand cmd) => AddLogic(cmd.context as GameObject);

        public static Logic AddLogic(GameObject target)
        {
            if (!target)
            {
                target = new GameObject("Logik");
                Undo.RegisterCreatedObjectUndo(target, KitBrand.Name + ": Logik anlegen");
                var view = SceneView.lastActiveSceneView;
                if (view) target.transform.position = view.pivot;
            }
            var logic = Undo.AddComponent<Logic>(target);
            Selection.activeGameObject = target;
            return logic;
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
            foreach (var it in Object.FindObjectsByType<Logic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var (go, slot) in KitRoles.Targets(it))   // incl. Dieses Objekt, without stale bindings
                {
                    if (!s_Uses.TryGetValue(go.GetInstanceID(), out var list)) s_Uses[go.GetInstanceID()] = list = new List<string>();
                    list.Add($"{slot.label} in {it.Graph.name} (Logik auf „{it.name}“)");
                }
            }
        }

        static void OnItem(int id, Rect rect)
        {
            if (s_Uses == null || EditorApplication.timeSinceStartup > s_Next) { Rebuild(); s_Next = EditorApplication.timeSinceStartup + 1; }
            if (!s_Uses.TryGetValue(id, out var uses)) return;
            var r = new Rect(rect.xMax - 26, rect.y, 26, rect.height);
            var c = GUI.color; GUI.color = KitStyle.Ref;
            GUI.Label(r, new GUIContent("◆" + uses.Count, KitBrand.Name + ": " + string.Join("\n", uses)), EditorStyles.miniLabel);
            GUI.color = c;
        }
    }

    /// <summary>
    /// Before Play: save open graphs and check every Logic. The list is written when Play has started:
    /// the Console's "Clear on Play" would wipe warnings logged while leaving edit mode (Weiche, Day 3).
    /// </summary>
    [InitializeOnLoad]
    public static class KitPlayCheck
    {
        const string Key = "Kit.PlayCheck.Pending";   // SessionState survives the domain reload on entering Play

        static KitPlayCheck() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

        public static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode)
            {
                SaveOpenGraphs();
                SessionState.SetString(Key, string.Join("\n", Check()));   // the state before Play counts
            }
            else if (s == PlayModeStateChange.EnteredPlayMode)
            {
                var pending = SessionState.GetString(Key, "");
                SessionState.EraseString(Key);
                foreach (var line in pending.Split('\n'))
                    if (line.Length > 0) Debug.LogWarning(line);
            }
        }

        /// <summary>One "[Kit] Vor Play: …" line per problem of the open scenes' interactions.</summary>
        public static System.Collections.Generic.List<string> Check()
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (var it in Object.FindObjectsByType<Logic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var p in it.Validate())
                    lines.Add($"{KitBrand.Log}Vor Play: {it.name} › {(p.node >= 0 && it.Graph ? it.Graph.nodes[p.node].title : "Logik")}: {p.text.Replace('\n', ' ')}");
            return lines;
        }

        static void SaveOpenGraphs()
        {
            // Play runs the saved (imported) graph, and GTK would otherwise stop Play with an "Unsaved Changes"
            // dialog. This handler is registered before GTK's windows exist, so it runs before GTK's own check.
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
                catch (System.Exception e) { Debug.LogWarning(KitBrand.Log + "Graph vor Play nicht gespeichert: " + e.Message); }
            }
        }
    }
}
