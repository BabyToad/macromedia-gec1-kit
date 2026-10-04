using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Changes an Interaction's binding table the editor way: with Undo and prefab overrides.</summary>
    public static class KitBinding
    {
        public static void Set(Interaction it, string slot, Object target)
        {
            Undo.IncrementCurrentGroup();                       // eigener Undo-Schritt, auch im selben Frame
            Undo.RecordObject(it, "Kit: Objekt binden");
            it.SetBinding(slot, target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(it);
            EditorUtility.SetDirty(it);
        }

        /// <summary>
        /// Gives an Interaction a graph through its serialized property, so Undo, dirty state and
        /// prefab-instance overrides are recorded like an Inspector edit.
        /// </summary>
        public static void AssignGraph(Interaction it, KitGraph graph)
        {
            var so = new SerializedObject(it);
            so.FindProperty("graph").objectReferenceValue = graph;
            so.ApplyModifiedProperties();     // records Undo and the prefab override
        }

        /// <summary>All interactions in the open scenes (and prefab stage) that play this graph.</summary>
        public static Interaction[] InstancesOf(KitGraph graph)
        {
            var all = Object.FindObjectsByType<Interaction>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            var list = new System.Collections.Generic.List<Interaction>();
            foreach (var i in all) if (i.Graph == graph) list.Add(i);
            if (stage != null)
                foreach (var i in stage.prefabContentsRoot.GetComponentsInChildren<Interaction>(true))
                    if (i.Graph == graph && !list.Contains(i)) list.Add(i);
            return list.ToArray();
        }

        /// <summary>Which slots of this interaction point at <paramref name="go"/> or one of its components.</summary>
        public static System.Collections.Generic.List<string> SlotsUsing(Interaction it, GameObject go)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var b in it.Bindings)
            {
                var t = b.target;
                if (t == null) continue;
                var bgo = t as GameObject ?? (t as Component)?.gameObject;
                if (bgo == go) list.Add(b.slot);
            }
            return list;
        }
    }
}
