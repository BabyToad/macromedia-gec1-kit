using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kit.Editor
{
    /// <summary>
    /// Keeps the Rolle component where it belongs: on every object that plays a role in some Logik of its scene (or
    /// prefab), and nowhere else. The Rolle is derived data, like a cache of the role table: the kit re-derives it after
    /// every change that can add or end a role (binding, another graph, a Logik added or removed, objects deleted, a
    /// graph re-imported, and every Undo/Redo) and on saving. It is never recorded in the Undo history, so Ctrl+Z only
    /// ever steps through the student's own edits and the Rollen simply follow. Objects outside the scene or prefab
    /// being edited (a prefab asset in the Project window, anything in a read-only package) are never touched.
    /// </summary>
    [InitializeOnLoad]
    public static class KitRoles
    {
        static bool s_Scheduled;

        static KitRoles()
        {
            EditorSceneManager.sceneSaving += (scene, _) => Reconcile(scene);
            PrefabStage.prefabSaving += root => Reconcile(root);
            ObjectChangeEvents.changesPublished += OnChanges;
            // Removing a component (e.g. the Logik, via its context menu or Undo) does not always reach the event stream;
            // the Hierarchy notices every structural change. Undo/Redo of a binding: re-derive, never record.
            EditorApplication.hierarchyChanged += Schedule;
            Undo.undoRedoPerformed += () => { if (!Application.isPlaying) { FlushPending(); Schedule(); } };   // now, and once more after Unity settles
        }

        // Only what can change roles schedules a tidy-up: a Logik's own properties (graph, bindings) and structural
        // changes (a Logik or object added, removed, deleted). Moving a wall or dragging a light slider does not.
        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (Application.isPlaying) return;
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var p);
                        if (EditorUtility.InstanceIDToObject(p.instanceId) is Logic) { Schedule(); return; }
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        Schedule(); return;
                }
            }
        }

        /// <summary>A Logik-Graph was re-imported (nodes added or deleted): tidy up once the import is done.</summary>
        internal static void GraphImported(LogicGraph graph)
        {
            if (graph && KitBinding.InstancesOf(graph).Length > 0) Schedule();
        }

        static void Schedule()
        {
            if (s_Scheduled || Application.isPlaying) return;
            s_Scheduled = true;
            EditorApplication.delayCall += FlushPending;
        }

        // Runs after Unity has finished applying the edit (or the Undo/Redo), over the scenes that are loaded then
        // (scene handles taken during an Undo can point at a temporary scene) and the open prefab.
        static void FlushPending()
        {
            s_Scheduled = false;
            if (Application.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) Reconcile(scene);
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) Reconcile(stage.prefabContentsRoot);
        }

        /// <summary>The GameObject a binding points at (a GameObject or one of its components); null if it was destroyed.</summary>
        public static GameObject ObjectOf(Object target)
        {
            if (!target) return null;                        // also catches destroyed objects behind a stale reference
            return target as GameObject ?? (target as Component)?.gameObject;
        }

        /// <summary>Objects the kit may give a Rolle: in a loaded scene or the open prefab, never an asset or a package file.</summary>
        static bool Editable(GameObject go) => go && !EditorUtility.IsPersistent(go);

        /// <summary>Every Logik that can see <paramref name="go"/>: same scene, or the same prefab contents.</summary>
        static IEnumerable<Logic> LogicsAround(GameObject go)
        {
            if (!go) return Enumerable.Empty<Logic>();
            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null) return stage.prefabContentsRoot.GetComponentsInChildren<Logic>(true);
            if (go.scene.IsValid() && go.scene.isLoaded)
                return go.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Logic>(true));
            return go.transform.root.GetComponentsInChildren<Logic>(true);   // prefab contents loaded for editing
        }

        /// <summary>
        /// Every object a Logik gives a role to, with the slot: stored bindings whose slot is still in the graph, plus
        /// "Dieses Objekt" (the Logik's own object) when the graph uses it.
        /// </summary>
        public static IEnumerable<(GameObject go, LogicGraph.Slot slot)> Targets(Logic logic)
        {
            if (!logic || !logic.Graph) yield break;
            foreach (var slot in logic.Graph.slots)
            {
                var go = ObjectOf(logic.GetBinding(slot.id));
                if (go) yield return (go, slot);
            }
        }

        /// <summary>The roles this object plays: (Logik, slot) pairs whose slot still exists in the Logik's graph.</summary>
        public static List<(Logic logic, LogicGraph.Slot slot)> RolesOf(GameObject go)
        {
            var list = new List<(Logic, LogicGraph.Slot)>();
            foreach (var logic in LogicsAround(go))
                foreach (var (target, slot) in Targets(logic))
                    if (target == go && slot.id != LogicGraph.SelfSlot) list.Add((logic, slot));   // the Logik shows its own object's role
            return list;
        }

        /// <summary>Adds the Rolle to the object if it has none (only in a scene or the open prefab). Returns the Rolle or null.</summary>
        public static Role Ensure(GameObject go)
        {
            if (!Editable(go)) return null;
            if (go.TryGetComponent(out Role existing)) return existing;
            var r = go.AddComponent<Role>();                 // not recorded in Undo: derived, re-made after every change
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            MarkDirty(go);
            return r;
        }

        /// <summary>
        /// Removes the Rolle if the object plays no role any more. A Rolle the object inherits from its prefab is left
        /// alone: removing it means a "removed component" override, which Unity records in the Undo history, and the
        /// automatic tidy-up must never record anything (it runs after Undo/Redo too). Such a Rolle just says it plays
        /// no role; its Inspector offers "Rolle entfernen" (<paramref name="byHand"/>), an ordinary undoable edit.
        /// </summary>
        public static bool RemoveIfUnused(GameObject go, bool byHand = false)
        {
            if (!Editable(go) || !go.TryGetComponent(out Role r)) return false;
            if (RolesOf(go).Count > 0) return false;
            bool inherited = PrefabUtility.IsPartOfPrefabInstance(r) && !PrefabUtility.IsAddedComponentOverride(r);
            if (inherited)
            {
                if (!byHand) return false;
                Undo.DestroyObjectImmediate(r);              // the student's own edit: an override of this instance
            }
            else if (byHand) Undo.DestroyObjectImmediate(r);
            else Object.DestroyImmediate(r);                 // derived data: not recorded
            MarkDirty(go);
            return true;
        }

        static void MarkDirty(GameObject go)
        {
            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            else EditorUtility.SetDirty(go);
        }

        /// <summary>After binding or unbinding: the new object gets its Rolle, the old one loses it if it is now unused.</summary>
        public static void AfterBinding(Object oldTarget, Object newTarget)
        {
            var oldGo = ObjectOf(oldTarget);
            var newGo = ObjectOf(newTarget);
            if (newGo && newGo != oldGo && RolesOf(newGo).Count > 0) Ensure(newGo);
            if (oldGo && oldGo != newGo) RemoveIfUnused(oldGo);
        }

        /// <summary>After a Logik got another graph (or none): roles that ended are tidied, new ones get their Rolle.</summary>
        public static void AfterGraphChange(Logic logic, IEnumerable<GameObject> before)
        {
            if (!logic) return;
            var now = Targets(logic).Select(t => t.go).ToList();
            foreach (var go in now) if (RolesOf(go).Count > 0) Ensure(go);
            foreach (var go in before) if (go && !now.Contains(go)) RemoveIfUnused(go);
        }

        /// <summary>Every bound object of one Logik gets its Rolle (the Inspector calls this when it opens).</summary>
        public static void EnsureAll(Logic logic)
        {
            foreach (var (go, slot) in Targets(logic))
                if (slot.id != LogicGraph.SelfSlot) Ensure(go);
        }

        /// <summary>Adds missing and removes orphaned Rollen in a whole scene. Returns (added, removed).</summary>
        public static (int added, int removed) Reconcile(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return (0, 0);
            return Reconcile(scene.GetRootGameObjects());
        }

        /// <summary>The same for the objects under these roots (a scene, or a prefab's contents). Nothing outside them changes.</summary>
        public static (int added, int removed) Reconcile(params GameObject[] roots)
        {
            int added = 0, removed = 0;
            var all = roots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
            var inside = new HashSet<GameObject>(all);
            var bound = new HashSet<GameObject>();
            foreach (var logic in all.SelectMany(g => g.GetComponents<Logic>()))
                foreach (var (go, slot) in Targets(logic))
                    if (slot.id != LogicGraph.SelfSlot && inside.Contains(go)) bound.Add(go);
            foreach (var go in bound)
                if (!go.TryGetComponent(out Role _) && Ensure(go)) added++;
            foreach (var go in all)
                if (!bound.Contains(go) && go.TryGetComponent(out Role _) && RemoveIfUnused(go)) removed++;
            return (added, removed);
        }

        /// <summary>Migration (v0.3.2 → v0.4): every bound object gets its Rolle, orphaned Rollen go.</summary>
        [KitMigrationStep(100, "Gebundene Objekte bekommen ihre Rolle", PerContainer = true)]
        static void MigrateRoles(GameObject[] roots) => Reconcile(roots);
    }
}
