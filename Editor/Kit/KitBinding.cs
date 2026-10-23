using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Changes a Logic's binding table the editor way: with Undo and prefab overrides.</summary>
    public static class KitBinding
    {
        public static void Set(Logic it, string slot, Object target)
        {
            if (slot == LogicGraph.SelfSlot) return;            // Dieses Objekt steht fest
            Undo.IncrementCurrentGroup();                       // eigener Undo-Schritt, auch im selben Frame
            var old = it.GetBinding(slot);
            Undo.RecordObject(it, KitBrand.Name + ": Rolle binden");
            it.SetBinding(slot, target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(it);
            EditorUtility.SetDirty(it);
            if (!Application.isPlaying) KitRoles.AfterBinding(old, target);   // Rolle-Komponente mitführen
        }

        /// <summary>
        /// Gives a Logic a graph through its serialized property, so Undo, dirty state and
        /// prefab-instance overrides are recorded like an Inspector edit.
        /// </summary>
        public static void AssignGraph(Logic it, LogicGraph graph)
        {
            var before = KitRoles.Targets(it).Select(t => t.go).ToList();
            var so = new SerializedObject(it);
            so.FindProperty("graph").objectReferenceValue = graph;
            so.ApplyModifiedProperties();     // records Undo and the prefab override
            if (!Application.isPlaying) KitRoles.AfterGraphChange(it, before);   // ended roles lose, new ones get their Rolle
        }

        /// <summary>All interactions in the open scenes (and prefab stage) that play this graph.</summary>
        public static Logic[] InstancesOf(LogicGraph graph)
        {
            var all = Object.FindObjectsByType<Logic>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            var list = new System.Collections.Generic.List<Logic>();
            foreach (var i in all) if (i.Graph == graph) list.Add(i);
            if (stage != null)
                foreach (var i in stage.prefabContentsRoot.GetComponentsInChildren<Logic>(true))
                    if (i.Graph == graph && !list.Contains(i)) list.Add(i);
            return list.ToArray();
        }

        /// <summary>Which roles of this Logik <paramref name="go"/> plays (incl. Dieses Objekt; stale bindings left out).</summary>
        public static System.Collections.Generic.List<string> SlotsUsing(Logic it, GameObject go) =>
            KitRoles.Targets(it).Where(t => t.go == go).Select(t => t.slot.id).ToList();
    }
}
