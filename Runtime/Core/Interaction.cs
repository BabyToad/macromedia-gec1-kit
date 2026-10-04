using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kit
{
    /// <summary>
    /// Plays one interaction graph ("Schlüssel und Tür") in the scene. It owns the binding table: which
    /// scene object fills which ◆ slot of the graph. Bindings are real Unity references, so renaming or
    /// moving objects keeps them, and inside a prefab they point at the prefab's own children.
    /// Every Interaction has its own copy of the nodes, so duplicated doors never share state.
    /// </summary>
    [AddComponentMenu("Kit/Interaktion")]
    public class Interaction : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            public string slot;
            public Object target;
        }

        public struct NodeState
        {
            public NodeStatus status;
            public string text;
            public float progress;
            public float time;
            public int count;        // how often an event arrived
            internal int failedFrame;
        }

        public struct Problem
        {
            public int node;         // -1 = the interaction itself
            public string slot;      // set when a binding is the cause
            public string text;
        }

        /// <summary>A synchronous chain may take at most this many steps, then it is stopped as a likely loop.</summary>
        public const int StepBudget = 64;

        [SerializeField] KitGraph graph;
        [SerializeField] List<Binding> bindings = new List<Binding>();

        public KitGraph Graph { get => graph; set { graph = value; Restart("neu gestartet: anderer Graph"); } }
        public IReadOnlyList<Binding> Bindings => bindings;

        KitNode[] m_Nodes;
        NodeState[] m_States;
        bool m_Built, m_Started;
        int m_Generation;   // changes whenever the interaction stops; a chain from an older generation stops too
        KitGraph m_BuiltGraph;      // the graph and bindings the current node copies were made from
        int m_BuiltBindings;
        readonly List<TraceEntry> m_Recent = new List<TraceEntry>();
        List<Coroutine>[] m_Routines;
        bool[] m_Live;   // OnStart ran (its ◆ targets were there)

        // dispatch state
        bool m_Dispatching;
        int m_CurrentNode = -1;
        readonly List<(int node, string port, Signal signal)> m_Pending = new List<(int, string, Signal)>();
        List<string> m_Path;

        public int NodeCount => graph ? graph.nodes.Count : 0;
        public KitNode NodeAt(int i) { EnsureBuilt(); return m_Nodes[i]; }
        public NodeState StateOf(int i) { EnsureBuilt(); return m_States[i]; }
        /// <summary>The last few things that happened here, oldest first.</summary>
        public IReadOnlyList<TraceEntry> Recent => m_Recent;

        // ---------------------------------------------------------------------------------- bindings
        public Object GetBinding(string slot)
        {
            foreach (var b in bindings) if (b.slot == slot) return b.target;
            return null;
        }

        /// <summary>Sets one slot. In the editor, call through KitBindingUtility so Undo and prefab overrides work.</summary>
        public void SetBinding(string slot, Object target)
        {
            var b = bindings.Find(x => x.slot == slot);
            if (b == null) bindings.Add(b = new Binding { slot = slot });
            b.target = target;
            Restart("neu gestartet: Objekt neu gebunden");
        }

        /// <summary>Converts what was dropped into a slot (a GameObject or any component) to what the field needs.</summary>
        public static Object Convert(Object bound, Type wanted)
        {
            if (bound == null) return null;              // also catches destroyed objects
            if (wanted.IsInstanceOfType(bound)) return bound;
            var go = bound as GameObject ?? (bound as Component)?.gameObject;
            if (go == null) return null;
            if (wanted == typeof(GameObject)) return go;
            if (typeof(Component).IsAssignableFrom(wanted)) return go.GetComponent(wanted);
            return null;
        }

        // ---------------------------------------------------------------------------------- lifecycle
        void Awake() => EnsureBuilt();

        void OnEnable() => StartNodes();
        void OnDisable() => StopNodes("abgebrochen: Interaktion ausgeschaltet");

        void StartNodes()
        {
            EnsureBuilt();
            if (!graph) { Debug.LogError("[Kit] Interaktion ohne Graph.", this); return; }
            m_Started = true;
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                m_Live[i] = false;
                if (!ResolveRefs(i, null, staticOnly: true, out var problem))
                {
                    Report(i, NodeStatus.Failed, problem, -1f);
                    continue;  // a trigger without its zone cannot listen
                }
                m_Live[i] = true;
                try { m_Nodes[i].OnStart(); }
                catch (Exception e) { Report(i, NodeStatus.Failed, "Fehler im Code: " + e.Message, -1f); Debug.LogException(e, this); }
            }
        }

        void StopNodes(string why)
        {
            if (!m_Started) return;
            m_Started = false;
            m_Generation++;                       // events still queued in a running chain are dropped
            m_Pending.Clear();
            StopAllCoroutines();
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                if (m_Routines[i] != null) m_Routines[i].Clear();
                if (!m_Live[i]) continue;
                m_Live[i] = false;
                try { m_Nodes[i].OnStop(); } catch (Exception e) { Debug.LogException(e, this); }
                if (m_States[i].status == NodeStatus.Running) Report(i, NodeStatus.Idle, why, -1f);
            }
        }

        /// <summary>
        /// Graph or binding changed while playing: stop every node (listeners off, timers off), make fresh
        /// copies and start again. Node state (Merker, Zähler …) starts over; the trace says why.
        /// </summary>
        void Restart(string why)
        {
            // Not running and not supposed to run (disabled): just rebuild later.
            if (!m_Started && !(Application.isPlaying && isActiveAndEnabled)) { m_Built = false; return; }
            StopNodes(why);
            m_Built = false;
            EnsureBuilt();
            StartNodes();
            for (int i = 0; i < m_Nodes.Length; i++)
                if (m_States[i].status == NodeStatus.Idle) m_States[i].text = why;
        }

        // Graph or bindings can also change without our setters: through the Inspector, a SerializedObject
        // or Undo. Then the copies no longer match; restart so no old listener or timer survives.
        bool OutOfDate() => graph != m_BuiltGraph || BindingsHash() != m_BuiltBindings;

        int BindingsHash()
        {
            unchecked
            {
                int h = 17;
                foreach (var b in bindings) h = h * 31 + (b.slot?.GetHashCode() ?? 0) * 7 + (ReferenceEquals(b.target, null) ? 0 : b.target.GetInstanceID());   // a deleted object keeps its id: deleting is not a re-bind
                return h;
            }
        }

#if UNITY_EDITOR
        // Called by the editor after Inspector edits, SerializedObject changes and Undo/Redo.
        void OnValidate()
        {
            if (!Application.isPlaying || !OutOfDate()) return;
            UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled && OutOfDate()) Restart("neu gestartet: im Editor geändert"); };
        }
#endif

        void Update()
        {
            // Also when not started: an enabled interaction that had no graph starts as soon as it gets one.
            // Enabled, has a graph, but not running (e.g. it had no graph for a while and something rebuilt
            // the copies in between): start it, whatever the snapshot says.
            if (OutOfDate() || (!m_Started && graph)) Restart("neu gestartet: Graph oder Objekte geändert");
            if (!m_Started) return;
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                if (!m_Live[i]) continue;
                try { m_Nodes[i].Tick(); }
                catch (Exception e) { Report(i, NodeStatus.Failed, "Fehler im Code: " + e.Message, -1f); Debug.LogException(e, this); }
            }
        }

        /// <summary>Makes this instance's node copies. Safe in edit mode (no OnStart there).</summary>
        public void EnsureBuilt()
        {
            if (m_Built && m_Nodes != null && graph == m_BuiltGraph && BindingsHash() == m_BuiltBindings
                && graph && m_Nodes.Length == graph.nodes.Count) return;
            if (m_Started) { Restart("neu gestartet: Graph geändert"); return; }   // never swap nodes under running ones
            int n = graph ? graph.nodes.Count : 0;
            m_Nodes = new KitNode[n];
            m_States = new NodeState[n];
            m_Routines = new List<Coroutine>[n];
            m_Live = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var copy = graph.nodes[i].CloneForRunner();
                copy.runner = this; copy.index = i;
                foreach (var o in NodeSchema.Of(copy.GetType()).Outputs)
                    o.Set(copy, new Output { runner = this, node = i, port = o.Id });
                m_Nodes[i] = copy;
                m_States[i].failedFrame = -1;
            }
            m_Built = true;
            m_BuiltGraph = graph;
            m_BuiltBindings = BindingsHash();
        }

        // ---------------------------------------------------------------------------------- dispatch
        /// <summary>Called by <see cref="Output.Fire"/>.</summary>
        internal void Emit(int node, string port, Signal signal)
        {
            if (!m_Started) return;                               // stopped interactions are silent
            if (m_States[node].status == NodeStatus.Failed && m_States[node].failedFrame == Time.frameCount)
                return;                                           // failed this frame: no success output
            signal = SnapshotEventValues(node, signal ?? new Signal());
            if (m_Dispatching) { m_Pending.Add((node, port, signal)); return; }
            RunChain(node, port, signal);
        }

        Signal SnapshotEventValues(int node, Signal signal)
        {
            var schema = NodeSchema.Of(m_Nodes[node].GetType());
            foreach (var p in schema.RefOuts) signal = signal.With(node, p.Id, p.Get(m_Nodes[node]));
            foreach (var p in schema.DataOuts) if (p.EventLocal) signal = signal.With(node, p.Id, p.Get(m_Nodes[node]));
            return signal;
        }

        // One world event (or one finished timer) starts a chain. Wires run depth-first in their order:
        // branch 1 finishes everything it does in this frame before branch 2 starts. No recursion, so a
        // loop cannot overflow the stack; it hits the step budget instead and reports its path.
        void RunChain(int node, string port, Signal signal) => RunChain(node, port, signal, deliverFirst: false);

        /// <summary>
        /// Sends an event straight into a node's ▶ input, as if a wire delivered it. For tests and the
        /// editor's "Test" button. Does nothing while the interaction is not running.
        /// </summary>
        public void Send(int node, string inputPort, Signal signal = null)
        {
            if (!m_Started || m_Dispatching) return;
            RunChain(node, inputPort, signal ?? new Signal(), deliverFirst: true);
        }

        /// <summary>
        /// Fires a node's ▶ output as if the node had done it (the editor's "Auslösen" button, demos).
        /// Lets you test the rest of a chain without walking into the zone.
        /// </summary>
        public void FireOutput(int node, string port)
        {
            if (!m_Started || m_Dispatching) return;
            Report(node, NodeStatus.Completed, "✓ von Hand ausgelöst", 1f);
            Emit(node, port, new Signal());
        }

        void RunChain(int node, string port, Signal signal, bool deliverFirst)
        {
            m_Dispatching = true;
            int generation = m_Generation;
            m_Path = new List<string> { Label(node, port) };
            var stack = new Stack<(int wire, Signal signal)>();
            if (deliverFirst)
            {
                Deliver(node, port, signal);
                var firstFired = new List<(int, string, Signal)>(m_Pending);
                m_Pending.Clear();
                PushOutputs(stack, firstFired);
            }
            else PushOutputs(stack, new List<(int, string, Signal)> { (node, port, signal) });
            int steps = deliverFirst ? 1 : 0;
            try
            {
                while (stack.Count > 0)
                {
                    // Branch 1 may have switched the interaction off (or off and on again): nothing queued
                    // before that may still run.
                    if (!m_Started || generation != m_Generation) { stack.Clear(); break; }
                    var (w, s) = stack.Pop();
                    var wire = graph.wires[w];
                    if (++steps > StepBudget)
                    {
                        string path = string.Join(" → ", m_Path.Skip(Math.Max(0, m_Path.Count - 12)));
                        Report(wire.to, NodeStatus.Failed, $"gestoppt nach {StepBudget} Schritten in einem Frame (Schleife?)", -1f);
                        Trace(TraceKind.Budget, wire.to, w, NodeStatus.Failed, path, -1f, s.Chain);
                        Debug.LogError($"[Kit] {name}: Kette nach {StepBudget} Schritten gestoppt. Weg: … {path}", this);
                        stack.Clear();
                        break;
                    }
                    m_Path.Add(Label(wire.to, wire.toPort));
                    Trace(TraceKind.Wire, wire.to, w, NodeStatus.Received, null, -1f, s.Chain);
                    Deliver(wire.to, wire.toPort, s);
                    var fired = new List<(int, string, Signal)>(m_Pending);
                    m_Pending.Clear();
                    PushOutputs(stack, fired);
                }
            }
            finally { m_Dispatching = false; m_Pending.Clear(); }
        }

        void PushOutputs(Stack<(int, Signal)> stack, List<(int node, string port, Signal signal)> fired)
        {
            // Outputs fired first must run first, and within one output wire 1 before wire 2.
            for (int f = fired.Count - 1; f >= 0; f--)
            {
                var ws = graph.WiresFrom(fired[f].node, fired[f].port);
                for (int k = ws.Count - 1; k >= 0; k--) stack.Push((ws[k], fired[f].signal));
            }
        }

        readonly List<string> m_FiredLabels = new List<string>();

        void Deliver(int node, string inputPort, Signal signal)
        {
            var n = m_Nodes[node];
            var schema = NodeSchema.Of(n.GetType());
            var input = schema.Input(inputPort);
            m_CurrentNode = node;
            m_States[node].count++;
            Report(node, NodeStatus.Received, "empfangen: " + (input?.Label ?? inputPort), -1f);
            try
            {
                if (input == null) { Report(node, NodeStatus.Failed, $"kein Eingang „{inputPort}“ (Graph neu importieren?)", -1f); return; }
                if (!ResolveRefs(node, signal, staticOnly: false, out var problem)) { Report(node, NodeStatus.Failed, problem, -1f); return; }
                PullData(node, signal, 0);
                int pendingBefore = m_Pending.Count;
                try { ((System.Reflection.MethodInfo)input.Member).Invoke(n, new object[] { signal }); }
                catch (System.Reflection.TargetInvocationException e)
                {
                    Report(node, NodeStatus.Failed, "Fehler im Code: " + e.InnerException?.Message, -1f);
                    Debug.LogException(e.InnerException ?? e, this);
                    return;
                }
                if (m_States[node].status == NodeStatus.Received)
                {
                    m_FiredLabels.Clear();
                    for (int i = pendingBefore; i < m_Pending.Count; i++)
                        if (m_Pending[i].node == node) m_FiredLabels.Add(schema.LabelOf(m_Pending[i].port));
                    Report(node, NodeStatus.Completed, m_FiredLabels.Count > 0 ? "✓ " + string.Join(", ", m_FiredLabels) : "✓", 1f);
                }
            }
            finally { m_CurrentNode = -1; }
        }

        // ---------------------------------------------------------------------------------- values
        /// <summary>Fills a node's ◆ fields. False + German reason if a required one is missing.</summary>
        bool ResolveRefs(int node, Signal signal, bool staticOnly, out string problem)
        {
            problem = null;
            var n = m_Nodes[node];
            foreach (var r in NodeSchema.Of(n.GetType()).Refs)
            {
                Object value;
                if (graph.IsEventRef(node, r.Id, out var link))
                {
                    if (r.Permanent)
                    {
                        problem = PermanentProblem(r.Label, graph.nodes[link.from].title);
                        return false;
                    }
                    if (staticOnly) continue;
                    object raw = null;
                    if (signal == null || !signal.TryGet(link.from, link.fromPort, out raw))
                    {
                        problem = $"◆ {r.Label}: dieses Ereignis kommt nicht von „{graph.nodes[link.from].title}“";
                        return false;
                    }
                    value = Convert(raw as Object, r.Type);
                    if (value == null && !r.Optional) { problem = $"◆ {r.Label} fehlt (das Objekt aus „{graph.nodes[link.from].title}“ gibt es nicht mehr)"; return false; }
                }
                else
                {
                    var slot = graph.SlotFor(node, r.Id);
                    var bound = GetBinding(slot);
                    value = Convert(bound, r.Type);
                    if (value == null && !r.Optional)
                    {
                        var label = graph.FindSlot(slot)?.label ?? r.Label;
                        // "bound is not null but == null" means it was there and got destroyed.
                        bool destroyed = !ReferenceEquals(bound, null) && bound == null;
                        bool wrongType = bound != null;
                        problem = destroyed ? $"◆ {r.Label} fehlt: „{label}“ wurde gelöscht"
                                : wrongType ? $"◆ {r.Label}: „{bound.name}“ hat kein {r.Type.Name}"
                                : $"◆ {r.Label} fehlt (Slot „{label}“ ist leer)";
                        return false;
                    }
                }
                r.Set(n, value);
            }
            return true;
        }

        public static string PermanentProblem(string label, string sourceTitle) =>
            $"◆ {label} muss ein festes Objekt sein: es ist mit „{sourceTitle}“ verbunden und käme erst mit einem Ereignis";

        void PullData(int node, Signal signal, int depth)
        {
            if (depth > 16) return;  // data cycles are rejected by the importer; this is a seatbelt
            var n = m_Nodes[node];
            foreach (var l in graph.links)
            {
                if (l.kind != KitGraph.LinkKind.Data || l.to != node) continue;
                var target = NodeSchema.Of(n.GetType()).DataIn(l.toField);
                if (target == null) continue;
                var v = ReadData(l.from, l.fromPort, signal, depth + 1);
                target.Set(n, ConvertData(v, target.Type));
            }
        }

        object ReadData(int node, string port, Signal signal, int depth)
        {
            var src = NodeSchema.Of(m_Nodes[node].GetType()).DataOut(port);
            if (src == null) return null;
            if (src.EventLocal && signal != null && signal.TryGet(node, port, out var v)) return v;
            PullData(node, signal, depth);
            return src.Get(m_Nodes[node]);
        }

        static object ConvertData(object v, Type t)
        {
            if (v == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t.IsInstanceOfType(v)) return v;
            if (t == typeof(string)) return v is float f ? f.ToString("0.##") : v.ToString();
            try { return System.Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return t.IsValueType ? Activator.CreateInstance(t) : null; }
        }

        /// <summary>Reads a node's ● inputs again outside of an event (for nodes that show live values).</summary>
        public void RefreshData(KitNode node)
        {
            if (node == null || node.runner != this) return;
            PullData(node.index, null, 0);
        }

        internal string DescribeSource(int node, string field)
        {
            foreach (var l in graph.links)
            {
                if (l.kind != KitGraph.LinkKind.Data || l.to != node || l.toField != field) continue;
                var src = m_Nodes[l.from];
                return src.Readout() ?? src.title;
            }
            return "fester Wert";
        }

        // ---------------------------------------------------------------------------------- reporting
        internal void Report(int node, NodeStatus status, string text, float progress)
        {
            if (m_States == null) return;
            ref var st = ref m_States[node];
            // "läuft" is reported every frame; the trace only needs it when it starts and a few times a second.
            bool quietUpdate = status == NodeStatus.Running && st.status == NodeStatus.Running && Time.time - st.time < 0.2f;
            st.status = status; st.text = text; st.progress = progress;
            if (quietUpdate) return;
            st.time = Time.time;
            if (status == NodeStatus.Failed)
            {
                st.failedFrame = Time.frameCount;
                m_Pending.RemoveAll(p => p.node == node);   // nothing it fired this step counts
                Debug.LogError($"[Kit] {name} › {graph.nodes[node].title}: {text}", this);
            }
            Trace(TraceKind.Status, node, -1, status, text, progress, 0);
        }

        void Trace(TraceKind kind, int node, int wire, NodeStatus status, string text, float progress, int chain)
        {
            var e = new TraceEntry { runner = this, kind = kind, node = node, wire = wire, status = status, text = text, progress = progress, time = Time.time, frame = Time.frameCount, chain = chain };
            m_Recent.Add(e);
            if (m_Recent.Count > 128) m_Recent.RemoveAt(0);
            KitTrace.Emit(e);
        }

        string Label(int node, string port) => graph.nodes[node].title + "." + NodeSchema.Of(graph.nodes[node].GetType()).LabelOf(port);

        // ---------------------------------------------------------------------------------- coroutines
        internal Coroutine StartNodeRoutine(int node, IEnumerator routine)
        {
            if (!m_Started || !isActiveAndEnabled) return null;
            var c = StartCoroutine(routine);
            (m_Routines[node] ??= new List<Coroutine>()).Add(c);
            return c;
        }

        internal void StopNodeRoutine(int node, Coroutine c)
        {
            if (c == null) return;
            StopCoroutine(c);
            m_Routines[node]?.Remove(c);
        }

        // ---------------------------------------------------------------------------------- validation
        /// <summary>
        /// Everything that would fail in Play, found now: empty or deleted slots, wrong component types,
        /// node-specific checks. Works in edit mode.
        /// </summary>
        public List<Problem> Validate()
        {
            var list = new List<Problem>();
            if (!graph) { list.Add(new Problem { node = -1, text = "Kein Graph zugewiesen." }); return list; }
            EnsureBuilt();
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                if (!ResolveRefs(i, null, staticOnly: true, out var problem))
                {
                    var r = NodeSchema.Of(m_Nodes[i].GetType()).Refs.Find(x => problem.Contains(x.Label));
                    list.Add(new Problem { node = i, slot = r != null ? graph.SlotFor(i, r.Id) : null, text = problem });
                    continue;
                }
                var extra = new List<string>();
                try { m_Nodes[i].Validate(extra); } catch (Exception e) { extra.Add("Prüfung fehlgeschlagen: " + e.Message); }
                foreach (var t in extra) list.Add(new Problem { node = i, text = t });
            }
            return list;
        }
    }
}
