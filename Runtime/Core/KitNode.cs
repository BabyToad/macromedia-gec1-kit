using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>What a node is doing right now, as shown on the node.</summary>
    public enum NodeStatus
    {
        Idle,       // nothing happened yet in this Play session
        Received,   // an event arrived and is being handled
        Running,    // working over time (waiting, moving)
        Completed,  // done; the named outputs fired
        Blocked,    // the event was stopped on purpose (gate closed); Reason says why
        Failed,     // could not do its job (missing target …); no success output fired
    }

    /// <summary>
    /// Carries one event through the graph. Values that belong to this event ("who entered", "what was
    /// spawned") travel with it, so a later event cannot overwrite them while this one is still waiting.
    /// </summary>
    public sealed class Signal
    {
        readonly Dictionary<string, object> m_Values;
        public readonly int Chain;   // id of the world event this started from (for the trace)
        static int s_NextChain;

        public Signal() { m_Values = new Dictionary<string, object>(); Chain = ++s_NextChain; }
        Signal(Signal parent) { m_Values = new Dictionary<string, object>(parent.m_Values); Chain = parent.Chain; }

        internal Signal With(int node, string port, object value)
        {
            var s = new Signal(this);
            s.m_Values[node + "." + port] = value;
            return s;
        }

        internal bool TryGet(int node, string port, out object value) => m_Values.TryGetValue(node + "." + port, out value);
    }

    /// <summary>▶ An event output on a node. Call <see cref="Fire"/> to send the event along its wires.</summary>
    public sealed class Output
    {
        internal Interaction runner;
        internal int node;
        internal string port;
        public void Fire(Signal signal) => runner.Emit(node, port, signal);
        /// <summary>True if at least one wire leaves this output.</summary>
        public bool IsConnected => runner.Graph.WiresFrom(node, port).Count > 0;
    }

    /// <summary>
    /// Base class for every kit node. A node reacts to ▶ inputs (methods with [Input]), reads its
    /// ◆ references and ● data from fields, and reports what it is doing: Running, Done, Blocked, Fail.
    /// One copy of the node exists per <see cref="Interaction"/> in the scene, so two doors never share state.
    /// </summary>
    [Serializable]
    public abstract class KitNode
    {
        [HideInInspector] public string id;     // stable id from the graph file
        [HideInInspector] public string title;  // German title shown on the node

        [NonSerialized] internal Interaction runner;
        [NonSerialized] internal int index;

        /// <summary>The interaction this copy belongs to.</summary>
        public Interaction Runner => runner;

        // ---- lifecycle --------------------------------------------------------------------------
        /// <summary>
        /// Interaction became active: subscribe to the world (triggers, input) here, and create any lists or
        /// dictionaries the node keeps. Each interaction gets a shallow copy of the node, so a list created in a
        /// field initializer would be shared by all copies.
        /// </summary>
        public virtual void OnStart() { }
        /// <summary>Interaction stopped: unsubscribe, forget running work. Never fire outputs here.</summary>
        public virtual void OnStop() { }
        /// <summary>Called every frame while active (only override if you need it).</summary>
        public virtual void Tick() { }
        /// <summary>Extra checks before Play, e.g. "the zone needs a trigger collider". Add German messages.</summary>
        public virtual void Validate(List<string> problems) { }
        /// <summary>A short live value for the node, e.g. "Schlüssel vorhanden: ja".</summary>
        public virtual string Readout() => null;

        // ---- reporting --------------------------------------------------------------------------
        /// <summary>Working over time. Progress 0..1, or -1 if unknown.</summary>
        protected void Running(string text, float progress = -1f) => runner.Report(index, NodeStatus.Running, text, progress);
        /// <summary>Finished. Fire the matching output right after.</summary>
        protected void Done(string text = null) => runner.Report(index, NodeStatus.Completed, text, 1f);
        /// <summary>Stopped on purpose (a closed gate). Say why, in German.</summary>
        protected void Blocked(string reason) => runner.Report(index, NodeStatus.Blocked, reason, -1f);
        /// <summary>Could not do the job. Outputs fired after this in the same step are dropped.</summary>
        protected void Fail(string reason) => runner.Report(index, NodeStatus.Failed, reason, -1f);

        // ---- work over time ---------------------------------------------------------------------
        /// <summary>Starts a coroutine owned by this node; it stops when the interaction is disabled.</summary>
        protected Coroutine StartWork(IEnumerator routine) => runner.StartNodeRoutine(index, routine);
        protected void StopWork(Coroutine c) => runner.StopNodeRoutine(index, c);

        /// <summary>Describes where a ● input gets its value, e.g. "Merker „Schlüssel vorhanden“: nein".</summary>
        protected string SourceOf(string dataField) => runner.DescribeSource(index, dataField);

        /// <summary>German yes/no.</summary>
        protected static string JaNein(bool b) => b ? "ja" : "nein";

        /// <summary>Copy for one interaction instance. Settings are shared, state is reset by the copy's own fields.</summary>
        internal KitNode CloneForRunner() => (KitNode)MemberwiseClone();
    }
}
