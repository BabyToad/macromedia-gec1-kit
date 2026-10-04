using System;

namespace Kit
{
    public enum TraceKind { Status, Wire, Budget }

    /// <summary>One thing that happened inside an interaction. The editor draws the graph from these.</summary>
    public struct TraceEntry
    {
        public Interaction runner;   // which instance (two doors = two runners)
        public TraceKind kind;
        public int node;             // Status: the node; Wire: the target node
        public int wire;             // Wire: index into KitGraph.wires, else -1
        public NodeStatus status;
        public string text;
        public float progress;
        public float time;           // Time.time
        public int frame;            // Time.frameCount
        public int chain;            // which world event this belongs to
    }

    /// <summary>
    /// Every runner reports here. Editor windows listen; in a build nobody listens. Always identify by
    /// <see cref="TraceEntry.runner"/>: a duplicated prefab must never light up the other copy's graph.
    /// </summary>
    public static class KitTrace
    {
        public static event Action<TraceEntry> Step;
        /// <summary>Also write every step to the Console (for debugging the kit itself).</summary>
        public static bool LogToConsole;

        internal static void Emit(in TraceEntry e)
        {
            if (LogToConsole) UnityEngine.Debug.Log($"[Kit] {e.runner?.name} {e.kind} n{e.node} w{e.wire} {e.status} {e.text}", e.runner);
            Step?.Invoke(e);
        }
    }
}
