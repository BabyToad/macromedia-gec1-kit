namespace Kit
{
    /// <summary>
    /// For nodes with an optional ◆ input that do something else when it is empty (a Variable without ◆ Objekt uses
    /// its Bereich). "Empty on purpose" and "connected, but nothing arrived" must not be confused: a deleted object
    /// or an event without its object is a mistake to report, not a reason to quietly change some other value.
    /// </summary>
    public static class KitRefs
    {
        public enum Wiring
        {
            /// <summary>Nothing is connected: the node's fallback applies.</summary>
            None,
            /// <summary>A fixed object: an inline binding, a named Rolle or Dieses Objekt.</summary>
            Fixed,
            /// <summary>The object comes with the event (e.g. ◆ Wer from a Zone).</summary>
            FromEvent,
        }

        /// <summary>How a ◆ field of <paramref name="node"/> is wired. Cheap enough to cache once per start.</summary>
        public static Wiring Of(KitNode node, string field, out string slot)
        {
            slot = null;
            var logic = node.Runner;
            if (logic == null || logic.Graph == null) return Wiring.None;
            var graph = logic.Graph;
            if (graph.IsEventRef(node.index, field, out _)) return Wiring.FromEvent;
            slot = graph.SlotFor(node.index, field);
            if (slot == LogicGraph.SelfSlot) return Wiring.Fixed;
            // A named Rolle is a decision in the graph, even before an object is bound to it.
            if (slot != LogicGraph.InlineSlotId(graph.nodes[node.index].id, field)) return Wiring.Fixed;
            return Bound(logic, slot) ? Wiring.Fixed : Wiring.None;
        }

        /// <summary>True if the slot holds an object, or held one that was deleted since.</summary>
        public static bool Bound(Logic logic, string slot)
        {
            var b = logic.GetBinding(slot);
            // Unity hands out "fake null" objects for unassigned fields (instance id 0); a destroyed object keeps its id.
            return !ReferenceEquals(b, null) && (b != null || b.GetInstanceID() != 0);
        }

        /// <summary>True if the slot held an object that has been deleted.</summary>
        public static bool Deleted(Logic logic, string slot)
        {
            var b = logic.GetBinding(slot);
            return !ReferenceEquals(b, null) && b == null && b.GetInstanceID() != 0;
        }
    }
}
