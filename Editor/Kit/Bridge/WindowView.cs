using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kit.Editor
{
    public static partial class KitGtkBridge
    {
        /// <summary>Everything the kit adds to one open graph window.</summary>
        internal class WindowView
        {
            public readonly EditorWindow Window;
            public readonly VisualElement Root;
            public string Path;

            KitEditorGraph m_Graph;
            KitGraph m_Runtime;
            Interaction m_Instance;
            readonly VisualElement m_Bar;
            readonly Button m_InstanceButton;
            readonly Label m_Summary, m_Info;
            readonly VisualElement m_InfoButtons, m_OrderLayer;
            readonly Dictionary<object, VisualElement> m_PortViews = new Dictionary<object, VisualElement>();
            readonly Dictionary<string, Label> m_OrderLabels = new Dictionary<string, Label>();
            readonly HashSet<string> m_OrderSeen = new HashSet<string>();
            Label m_Notice;
            readonly Dictionary<VisualElement, NodeDeco> m_Decos = new Dictionary<VisualElement, NodeDeco>();
            HashSet<string> m_HighlightSlots = new HashSet<string>();
            string m_FlashSlot; double m_FlashUntil;
            object m_InfoFor;
            Dictionary<int, List<string>> m_Problems = new Dictionary<int, List<string>>();

            class NodeDeco
            {
                public VisualElement Extras, Chips, Glow, Progress;
                public Label State;
                public string ChipSignature;
                public readonly List<(ObjectField field, string slot)> Fields = new List<(ObjectField, string)>();
            }

            public WindowView(EditorWindow w, VisualElement root)
            {
                Window = w; Root = root;
                if (Style) Root.styleSheets.Add(Style);
                m_Bar = new VisualElement(); m_Bar.AddToClassList("kit-bar");
                var row = new VisualElement(); row.AddToClassList("kit-bar__row");
                m_InstanceButton = new Button(PickInstance) { tooltip = "Welche Interaktion in der Szene zeigt und bearbeitet dieses Fenster?" };
                m_Summary = new Label(); m_Summary.AddToClassList("kit-bar__summary");
                var add = new Button(ShowPalette) { text = "+ Knoten", tooltip = "Knoten hinzufügen (deutsche Liste). Leertaste öffnet die GTK-Liste mit englischen Namen." };
                row.Add(m_InstanceButton); row.Add(add); row.Add(m_Summary);
                m_Info = new Label(); m_Info.AddToClassList("kit-bar__info");
                m_InfoButtons = new VisualElement(); m_InfoButtons.AddToClassList("kit-bar__row");
                m_Bar.Add(row); m_Bar.Add(m_Info); m_Bar.Add(m_InfoButtons);
                Root.Add(m_Bar);
                m_OrderLayer = new VisualElement { pickingMode = PickingMode.Ignore };
                m_OrderLayer.style.position = Position.Absolute; m_OrderLayer.style.left = m_OrderLayer.style.top = m_OrderLayer.style.right = m_OrderLayer.style.bottom = 0;
                Root.Add(m_OrderLayer);
                Root.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
                // Objects from the Hierarchy dropped on empty canvas become Objekt nodes. (Dropping on a ◆ field
                // is handled by the field itself, which stops the event before it bubbles up here.)
                Root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
                Root.RegisterCallback<DragPerformEvent>(OnDragPerform);
                // Variables (Blackboard) are not part of the kit; its panel would only cover the canvas.
                if (w.TryGetOverlay("gtf-blackboard", out var blackboard)) blackboard.displayed = false;
                if (GtkFieldsBroken) ShowNotice(GtkBrokenMessage);
                else if (!ReflectionOk) ShowNotice("Beobachtung im Graph nicht verfügbar (GTK-Interna fehlen). Zustand und Objekte: Inspector der Interaktion.");
            }

            public void ShowNotice(string text)
            {
                if (m_Notice == null) { m_Notice = new Label(); m_Notice.AddToClassList("kit-notice"); Root.Add(m_Notice); }
                m_Notice.text = text;
            }

            Interaction Instance => KitInstances.Get(Path);

            public void InstanceChanged() { foreach (var d in m_Decos.Values) d.ChipSignature = null; }

            // ---------------------------------------------------------------------------- per tick
            public void Refresh(KitEditorGraph graph)
            {
                m_Graph = graph;
                Path = GraphDatabase.GetGraphAssetPath(graph);
                m_Runtime = AssetDatabase.LoadAssetAtPath<KitGraph>(Path);
                if (m_Bar.parent != Root) Root.Add(m_Bar);
                m_Bar.BringToFront();

                var instances = m_Runtime ? KitBinding.InstancesOf(m_Runtime) : Array.Empty<Interaction>();
                if (!KitInstances.HasChoice(Path) && instances.Length == 1) KitInstances.Select(Path, instances[0]);
                var inst = Instance;
                if (inst != m_Instance) { m_Instance = inst; InstanceChanged(); }
                m_InstanceButton.text = inst ? $"Instanz: {inst.name} ▾" + (instances.Length == 1 ? " (einzige)" : $" · {instances.Length} in der Szene")
                    : instances.Length == 0 ? "Keine Interaktion nutzt diesen Graph ▾" : $"Instanz wählen ▾ · {instances.Length} in der Szene";

                m_Problems.Clear();
                if (inst && !Application.isPlaying)
                    foreach (var p in inst.Validate())
                    {
                        if (p.node < 0) continue;
                        if (!m_Problems.TryGetValue(p.node, out var l)) m_Problems[p.node] = l = new List<string>();
                        l.Add(p.text);
                    }
                Summarize(inst);
                if (!ReflectionOk) return;

                if (m_FlashSlot != null && EditorApplication.timeSinceStartup > m_FlashUntil) m_FlashSlot = null;
                object selected = null; VisualElement selectedView = null;
                m_PortViews.Clear();
                Root.Query<VisualElement>().ForEach(ve => { var m = ModelOf(ve); if (m is IPort) m_PortViews[m] = ve; });
                m_OrderSeen.Clear();
                Root.Query<VisualElement>().ForEach(ve =>
                {
                    var model = ModelOf(ve);
                    if (model == null) return;
                    if (Get(model, "Node") is Node node && (node is KitEditorNode || node is ObjectNode))
                    {
                        DecorateNode(ve, node, inst);
                        if (IsSelected(ve)) { selected = node; selectedView = ve; }
                    }
                    else if (IsWire(ve))
                    {
                        DecorateWire(ve, model, inst);
                        if (IsSelected(ve) && selected == null) { selected = model; selectedView = ve; }
                    }
                });
                foreach (var key in m_OrderLabels.Keys.Where(k => !m_OrderSeen.Contains(k)).ToList()) { m_OrderLabels[key].RemoveFromHierarchy(); m_OrderLabels.Remove(key); }
                m_OrderLayer.BringToFront(); m_Bar.BringToFront();
                ShowInfo(selected, inst);
                if (Application.isPlaying) Window.Repaint();   // an unfocused window does not redraw by itself
            }

            void Summarize(Interaction inst)
            {
                if (!inst) { m_Summary.text = m_Runtime ? "Instanz wählen (Knopf links), dann Objekte binden und Play beobachten." : "Graph noch nicht importiert (speichern)."; return; }
                if (!Application.isPlaying)
                {
                    int n = m_Problems.Values.Sum(l => l.Count);
                    m_Summary.text = n == 0 ? "✓ alles gebunden, bereit für Play" : $"! {n} Problem(e) vor Play – rot markiert";
                    return;
                }
                var counts = new Dictionary<NodeStatus, int>();
                for (int i = 0; i < inst.NodeCount; i++) { var s = inst.StateOf(i).status; counts[s] = counts.TryGetValue(s, out var c) ? c + 1 : 1; }
                string Count(NodeStatus s, string label) => counts.TryGetValue(s, out var c) ? $"{c} {label}" : null;
                m_Summary.text = "Play · " + string.Join(" · ", new[] { Count(NodeStatus.Running, "läuft"), Count(NodeStatus.Completed, "fertig"),
                    Count(NodeStatus.Blocked, "blockiert"), Count(NodeStatus.Failed, "Fehler"), Count(NodeStatus.Idle, "nicht erreicht") }.Where(s => s != null));
            }

            // ---------------------------------------------------------------------------- nodes
            int RuntimeIndex(Node n) => m_Runtime ? m_Runtime.IndexOf(KitIds.Of(n)) : -1;

            /// <summary>The slot a ◆ input reads: the Objekt node it is wired to, or its own chip.</summary>
            static string SlotOf(Node n, string refId)
            {
                var port = n.GetInputPortByName(refId);
                var list = new List<IPort>();
                port?.GetConnectedPorts(list);
                foreach (var p in list) if (p.GetNode() is ObjectNode on) return KitIds.Of(on);
                return KitGraph.InlineSlotId(KitIds.Of(n), refId);
            }

            IEnumerable<string> SlotsOf(Node n)
            {
                if (n is ObjectNode) { yield return KitIds.Of(n); yield break; }
                if (n is KitEditorNode k) foreach (var r in k.Schema.Refs) yield return SlotOf(n, r.Id);
            }

            void DecorateNode(VisualElement view, Node node, Interaction inst)
            {
                if (!m_Decos.TryGetValue(view, out var d))
                {
                    d = new NodeDeco { Extras = new VisualElement(), Chips = new VisualElement(), State = new Label(), Glow = new VisualElement() };
                    d.Extras.AddToClassList("kit-extras"); d.State.AddToClassList("kit-state");
                    d.Progress = new VisualElement();
                    d.Progress.AddToClassList("kit-progress");
                    d.Extras.Add(d.Chips); d.Extras.Add(d.Progress); d.Extras.Add(d.State);
                    d.Extras.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());   // chips are for clicking, not dragging the node
                    d.Glow.AddToClassList("kit-glow"); d.Glow.pickingMode = PickingMode.Ignore;
                    if (node is KitEditorNode kn)
                    {
                        var code = new Button(() => KitCodeView.Show(kn.RuntimeType)) { text = "</>", tooltip = "Code ansehen" };
                        code.AddToClassList("kit-code");
                        view.Add(code);
                    }
                    view.Add(d.Extras); view.Add(d.Glow);
                    m_Decos[view] = d;
                }
                if (d.Extras.parent != view) { view.Add(d.Extras); view.Add(d.Glow); }

                // Marker types (▶/◆) would show GTK's generic "01/10" type icon: hide it.
                view.Query(className: "ge-port").ForEach(pv =>
                {
                    if (ModelOf(pv) is IPort ip && (ip.dataType == typeof(EventPort) || ip.dataType == typeof(RefPort)))
                        pv.Query(className: "ge-port__icon").ForEach(icon => icon.style.display = DisplayStyle.None);
                });

                // German title (GTK 0.4 always shows the class name).
                string typeName = node.GetType().Name, title = node is KitEditorNode k2 ? k2.Schema.Title : "Objekt: " + ((ObjectNode)node).Label;
                view.Query<Label>().ForEach(l => { if (l.text == typeName) l.text = title; });

                Chips(d, node, inst);

                // State line and glow.
                Color glow = Color.clear; string line = null; Color lineColor = new Color(0.6f, 0.6f, 0.6f);
                int idx = RuntimeIndex(node);
                if (node is KitEditorNode && idx < 0 && m_Runtime) line = "neu – speichern, dann aktiv";
                else if (node is KitEditorNode && Application.isPlaying && inst && idx >= 0 && idx < inst.NodeCount)
                {
                    var st = inst.StateOf(idx);
                    var readout = inst.NodeAt(idx).Readout();
                    float age = Time.time - st.time;
                    line = st.status == NodeStatus.Idle ? (st.text ?? "noch nicht erreicht")
                         : st.text != null && st.text.StartsWith(KitStyle.Icon(st.status)) ? st.text : $"{KitStyle.Icon(st.status)} {st.text}";
                    if (readout != null) line += (line.Length > 0 ? "\n" : "") + readout;
                    lineColor = st.status == NodeStatus.Idle ? new Color(0.55f, 0.55f, 0.55f) : KitStyle.ColorOf(st.status);
                    bool sticky = st.status == NodeStatus.Running || st.status == NodeStatus.Failed || st.status == NodeStatus.Blocked;
                    if (sticky || age < 1.0f) glow = KitStyle.ColorOf(st.status);
                }
                else if (node is KitEditorNode && idx >= 0 && m_Problems.TryGetValue(idx, out var probs))
                {
                    line = "! " + string.Join("\n! ", probs); lineColor = KitStyle.Failed; glow = KitStyle.Failed;
                }
                bool highlighted = SlotsOf(node).Any(s => m_HighlightSlots.Contains(s) || s == m_FlashSlot);
                if (highlighted) glow = KitStyle.Ref;
                // progress bar for running work (Warten, Bewegen, Ton, Gehen zu)
                float progress = -1f;
                if (node is KitEditorNode && Application.isPlaying && inst && idx >= 0 && idx < inst.NodeCount && inst.StateOf(idx).status == NodeStatus.Running)
                    progress = inst.StateOf(idx).progress;
                d.Progress.style.display = progress >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (progress >= 0) d.Progress.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
                // a finished step fades out over one second instead of switching off
                if (!highlighted && glow.a > 0 && Application.isPlaying && inst && idx >= 0 && idx < inst.NodeCount)
                {
                    var st2 = inst.StateOf(idx);
                    if (st2.status == NodeStatus.Completed || st2.status == NodeStatus.Received)
                        glow.a = Mathf.Clamp01(1f - (Time.time - st2.time));
                }
                d.State.text = line ?? "";
                d.State.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
                d.State.style.color = lineColor;
                d.Glow.style.display = glow.a > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                d.Glow.style.borderTopColor = d.Glow.style.borderBottomColor = d.Glow.style.borderLeftColor = d.Glow.style.borderRightColor = glow;
                d.Glow.style.backgroundColor = new Color(glow.r, glow.g, glow.b, highlighted ? 0.08f : 0.12f);
            }

            void Chips(NodeDeco d, Node node, Interaction inst)
            {
                // Which ◆ inputs need a chip: those not wired to an Objekt node or an event value.
                var wanted = new List<(string slot, string label, Type type)>();
                if (node is ObjectNode on) wanted.Add((KitIds.Of(on), on.Label, typeof(GameObject)));
                else if (node is KitEditorNode k)
                    foreach (var r in k.Schema.Refs)
                    {
                        var port = node.GetInputPortByName(r.Id);
                        if (port == null || port.isConnected) continue;
                        wanted.Add((KitGraph.InlineSlotId(KitIds.Of(node), r.Id), r.Label + (r.Optional ? " (optional)" : ""), r.Type));
                    }
                string sig = (inst ? inst.GetInstanceID() : 0) + "|" + string.Join(";", wanted.Select(w => w.slot));
                if (sig != d.ChipSignature)
                {
                    d.ChipSignature = sig;
                    d.Chips.Clear(); d.Fields.Clear();
                    foreach (var w in wanted)
                    {
                        var field = new ObjectField("◆ " + w.label) { objectType = w.type == typeof(GameObject) ? typeof(GameObject) : w.type, allowSceneObjects = true };
                        field.AddToClassList("kit-chip");
                        string slot = w.slot;
                        field.SetEnabled(inst != null);
                        field.tooltip = inst ? "Objekt aus der Hierarchy hierher ziehen" : "Zuerst eine Instanz wählen";
                        field.RegisterValueChangedCallback(e => { var i = Instance; if (i) KitBinding.Set(i, slot, e.newValue); });
                        d.Chips.Add(field); d.Fields.Add((field, slot));
                    }
                }
                foreach (var (field, slot) in d.Fields)
                {
                    if (!inst) continue;
                    var bound = inst.GetBinding(slot);
                    var shown = Interaction.Convert(bound, field.objectType) ?? (bound != null ? bound : null);
                    if (field.value != shown) field.SetValueWithoutNotify(shown);
                    bool empty = shown == null && !field.label.EndsWith("(optional)");
                    field.EnableInClassList("kit-chip--empty", empty);
                }
            }

            // ---------------------------------------------------------------------------- wires
            void DecorateWire(VisualElement view, object wire, Interaction inst)
            {
                var control = Get(view, "WireControl") as VisualElement;
                if (control == null) return;
                var fromPort = Get(wire, "FromPort") as IPort;
                var kind = fromPort?.dataType;
                Color c = kind == typeof(EventPort) ? KitStyle.Event : kind == typeof(RefPort) ? KitStyle.Ref : KitStyle.Data;
                string fromNode = Get(wire, "FromNodeGuid")?.ToString(), toNode = Get(wire, "ToNodeGuid")?.ToString();
                string fromId = Get(wire, "FromPortId") as string, toId = Get(wire, "ToPortId") as string;

                // Did this wire just carry an event in the chosen instance?
                if (Application.isPlaying && inst && m_Runtime && kind == typeof(EventPort))
                {
                    int f = m_Runtime.IndexOf(fromNode), t = m_Runtime.IndexOf(toNode);
                    var recent = inst.Recent;
                    for (int i = recent.Count - 1; i >= 0; i--)
                    {
                        var e = recent[i];
                        if (Time.time - e.time > 0.8f) break;
                        if (e.kind != TraceKind.Wire) continue;
                        var w = m_Runtime.wires[e.wire];
                        if (w.from == f && w.to == t && w.fromPort == fromId && w.toPort == toId) { c = KitStyle.Running; break; }
                    }
                }
                control.GetType().GetMethod("SetColor", k_Any)?.Invoke(control, new object[] { c, c });

                // Run-order number when an output has several ▶ wires, drawn on the wire itself.
                int siblings = kind == typeof(EventPort) ? m_Graph.WireOrders.Count(o => o.fromNode == fromNode && o.fromPort == fromId) : 0;
                var toPortModel = Get(wire, "ToPort");
                if (siblings > 1 && fromPort != null && toPortModel != null
                    && m_PortViews.TryGetValue(fromPort, out var fromView) && m_PortViews.TryGetValue(toPortModel, out var toView))
                {
                    string key = $"{fromNode}/{fromId}>{toNode}/{toId}";
                    m_OrderSeen.Add(key);
                    if (!m_OrderLabels.TryGetValue(key, out var label))
                    {
                        label = new Label(); label.AddToClassList("kit-order"); label.pickingMode = PickingMode.Ignore;
                        m_OrderLayer.Add(label); m_OrderLabels[key] = label;
                    }
                    label.text = m_Graph.OrderOf(fromNode, fromId, toNode, toId).ToString();
                    var a = m_OrderLayer.WorldToLocal(new Vector2(fromView.worldBound.xMax, fromView.worldBound.center.y));
                    var b = m_OrderLayer.WorldToLocal(new Vector2(toView.worldBound.xMin, toView.worldBound.center.y));
                    float dx = Mathf.Max(40, Mathf.Abs(b.x - a.x) * 0.5f);
                    var p = Bezier(new[] { a, a + new Vector2(dx, 0), b - new Vector2(dx, 0), b }, 0.3f);
                    label.style.left = p.x - 8; label.style.top = p.y - 9;
                }
            }

            static Vector2 Bezier(Vector2[] p, float t)
            {
                float u = 1 - t;
                return u * u * u * p[0] + 3 * u * u * t * p[1] + 3 * u * t * t * p[2] + t * t * t * p[3];
            }

            // ---------------------------------------------------------------------------- info bar
            void ShowInfo(object selected, Interaction inst)
            {
                if (selected == null) { if (m_InfoFor != null) { m_Info.text = ""; m_InfoButtons.Clear(); m_InfoFor = null; } return; }
                bool rebuildButtons = !ReferenceEquals(selected, m_InfoFor);
                m_InfoFor = selected;
                if (rebuildButtons) m_InfoButtons.Clear();
                if (selected is Node node) NodeInfo(node, inst, rebuildButtons);
                else WireInfo(selected, inst, rebuildButtons);
            }

            void NodeInfo(Node node, Interaction inst, bool buttons)
            {
                if (node is ObjectNode on)
                {
                    var bound = inst ? inst.GetBinding(KitIds.Of(on)) : null;
                    m_Info.text = $"◆ Objekt „{on.Label}“ = {(bound ? bound.name : "leer")} · Doppelklick: in der Hierarchy zeigen";
                    if (buttons) Btn("In der Hierarchy zeigen", () => Reveal(node, false));
                    return;
                }
                var k = (KitEditorNode)node;
                int idx = RuntimeIndex(node);
                string text = $"{k.Schema.Title}: {k.Schema.Summary}";
                bool helper = k.Schema.Refs.Count == 0;
                if (inst && idx >= 0 && idx < inst.NodeCount)
                {
                    var st = inst.StateOf(idx);
                    if (Application.isPlaying)
                    {
                        text += $"\nin „{inst.name}“: {KitStyle.German(st.status)}{(st.text != null ? " – " + st.text : "")}";
                        var readout = inst.NodeAt(idx).Readout();
                        if (readout != null) text += $" · {readout}";
                        if (st.count > 0) text += $" · {st.count}× erreicht, zuletzt bei {st.time:0.0} s";
                    }
                    else if (helper) text += $"\nläuft in „{inst.name}“ (jede Instanz hat ihren eigenen Zustand)";
                }
                m_Info.text = text;
                if (!buttons) return;
                if (!helper) Btn("In der Hierarchy zeigen", () => Reveal(node, false));
                if (!helper) Btn("Komponente öffnen", () => Reveal(node, true));
                if (helper && inst) Btn("Interaktion zeigen", () => { Selection.activeGameObject = inst.gameObject; EditorGUIUtility.PingObject(inst.gameObject); });
                Btn("Code ansehen", () => KitCodeView.Show(k.RuntimeType));
            }

            void WireInfo(object wire, Interaction inst, bool buttons)
            {
                var fromPort = Get(wire, "FromPort") as IPort; var toPort = Get(wire, "ToPort") as IPort;
                if (fromPort == null || toPort == null) { m_Info.text = ""; return; }
                Node fromNode = fromPort.GetNode() as Node, toNode = toPort.GetNode() as Node;
                string Title(Node n) => n is KitEditorNode k ? k.Schema.Title : n is ObjectNode o ? "Objekt „" + o.Label + "“" : n?.GetType().Name;
                string kind = fromPort.dataType == typeof(EventPort) ? "▶ Event" : fromPort.dataType == typeof(RefPort) ? "◆ Referenz" : "● Daten";
                string text = $"Draht {kind}: {Title(fromNode)} · {fromPort.displayName}  →  {Title(toNode)} · {toPort.displayName}";
                string fn = KitIds.Of(fromNode), tn = KitIds.Of(toNode);
                int siblings = fromPort.dataType == typeof(EventPort) ? m_Graph.WireOrders.Count(o => o.fromNode == fn && o.fromPort == fromPort.name) : 0;
                int order = m_Graph.OrderOf(fn, fromPort.name, tn, toPort.name);
                if (siblings > 1) text += $"\nläuft als Nr. {order} von {siblings} (alles, was an Nr. {order} hängt und nicht wartet, ist fertig, bevor Nr. {order + 1} startet)";
                if (Application.isPlaying && inst && m_Runtime && fromPort.dataType == typeof(EventPort))
                {
                    int f = m_Runtime.IndexOf(fn), t = m_Runtime.IndexOf(tn);
                    var hits = inst.Recent.Where(e => e.kind == TraceKind.Wire && m_Runtime.wires[e.wire].from == f && m_Runtime.wires[e.wire].to == t
                                                      && m_Runtime.wires[e.wire].fromPort == fromPort.name && m_Runtime.wires[e.wire].toPort == toPort.name).ToList();
                    text += hits.Count == 0 ? "\nin dieser Instanz noch nicht benutzt" : $"\n{hits.Count}× benutzt, zuletzt bei {string.Join(", ", hits.Skip(Math.Max(0, hits.Count - 4)).Select(h => h.time.ToString("0.0") + " s"))}";
                }
                m_Info.text = text;
                if (!buttons) return;
                if (siblings > 1)
                {
                    Btn("◀ früher", () => Reorder(fn, fromPort.name, tn, toPort.name, order - 1));
                    Btn("später ▶", () => Reorder(fn, fromPort.name, tn, toPort.name, order + 1));
                }
                Btn("Start zeigen", () => Flash(fromNode));
                Btn("Ziel zeigen", () => Flash(toNode));
            }

            void Btn(string text, Action a) => m_InfoButtons.Add(new Button(a) { text = text });

            void Reorder(string fn, string fp, string tn, string tp, int newOrder)
            {
                var graphObject = KitGraphEditing.GraphObjectOf(m_Graph);
                if (graphObject) Undo.RecordObject(graphObject, "Kit: Reihenfolge ändern");
                m_Graph.SetOrder(fn, fp, tn, tp, newOrder);
                KitGraphEditing.Save(m_Graph);
                m_InfoFor = null;
            }

            void Flash(Node n)
            {
                var s = SlotsOf(n).FirstOrDefault();
                m_FlashSlot = s ?? "node:" + KitIds.Of(n);
                m_FlashUntil = EditorApplication.timeSinceStartup + 1.5;
                if (s == null) // nodes without objects: glow via a pseudo slot
                    foreach (var kv in m_Decos)
                        if (Get(ModelOf(kv.Key), "Node") == n) { kv.Value.Glow.style.display = DisplayStyle.Flex; }
            }

            public void FlashSlot(string slot) { m_FlashSlot = slot; m_FlashUntil = EditorApplication.timeSinceStartup + 2.0; }

            // ---------------------------------------------------------------------------- navigation
            void OnMouseDown(MouseDownEvent evt)
            {
                if (evt.clickCount != 2 || evt.button != 0) return;
                for (var ve = evt.target as VisualElement; ve != null; ve = ve.parent)
                {
                    if (Get(ModelOf(ve), "Node") is Node n && (n is KitEditorNode || n is ObjectNode))
                    {
                        if (Reveal(n, evt.shiftKey)) evt.StopPropagation();
                        return;
                    }
                }
            }

            /// <summary>Node → its object in the Hierarchy, the matching component unfolded in the Inspector.</summary>
            public bool Reveal(Node n, bool ownInspector)
            {
                var inst = Instance;
                if (!inst) { m_Info.text = "Zuerst eine Instanz wählen (Knopf unten links): Welche Interaktion in der Szene ist gemeint?"; return false; }
                var k = n as KitEditorNode;
                if (k != null && k.Schema.Refs.Count == 0)
                {
                    Selection.activeGameObject = inst.gameObject;     // helper nodes live in the interaction
                    EditorGUIUtility.PingObject(inst.gameObject);
                    return true;
                }
                foreach (var (slot, type) in SlotsWithTypes(n))
                {
                    var bound = inst.GetBinding(slot);
                    var go = bound as GameObject ?? (bound as Component)?.gameObject;
                    if (!go) continue;
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                    var comp = typeof(Component).IsAssignableFrom(type) ? go.GetComponent(type) : null;
                    if (comp)
                    {
                        foreach (var c in go.GetComponents<Component>()) InternalEditorUtility.SetIsInspectorExpanded(c, c == comp);
                        ActiveEditorTracker.sharedTracker.ForceRebuild();
                        if (ownInspector) EditorUtility.OpenPropertyEditor(comp);
                    }
                    return true;
                }
                m_Info.text = "Kein Objekt gebunden: Objekt aus der Hierarchy auf das ◆-Feld ziehen.";
                return false;
            }

            IEnumerable<(string slot, Type type)> SlotsWithTypes(Node n)
            {
                if (n is ObjectNode) { yield return (KitIds.Of(n), typeof(GameObject)); yield break; }
                if (n is KitEditorNode k) foreach (var r in k.Schema.Refs) yield return (SlotOf(n, r.Id), r.Type);
            }

            /// <summary>Hierarchy → graph: choosing an Interaction picks the instance; any other object lights up the nodes that use it.</summary>
            public void HierarchySelectionChanged()
            {
                var go = Selection.activeGameObject;
                m_HighlightSlots = new HashSet<string>();
                if (!go || !m_Runtime) return;
                var it = go.GetComponents<Interaction>().FirstOrDefault(i => i.Graph == m_Runtime);
                if (it) { KitInstances.Select(Path, it); return; }
                var inst = Instance;
                if (inst) m_HighlightSlots = new HashSet<string>(KitBinding.SlotsUsing(inst, go));
                if (KitTrace.LogToConsole || KitGtkBridge.Verbose) Debug.Log($"[Kit] Auswahl {go.name}: inst={(inst ? inst.name : "-")} slots={string.Join(",", m_HighlightSlots)}");
            }

            // ---------------------------------------------------------------------------- creating nodes
            static readonly string[] k_GroupOrder = { "Auslöser", "Ablauf", "Zustand", "Aktion", "Spielsysteme", "Erweiterung" };

            void ShowPalette()
            {
                var menu = new GenericMenu();
                var types = TypeCache.GetTypesDerivedFrom<KitEditorNode>()   // also faces from optional assemblies (Cinemachine)
                    .Where(t => !t.IsAbstract)
                    .Select(t => (type: t, schema: ((KitEditorNode)Activator.CreateInstance(t)).Schema))
                    .OrderBy(x => Array.IndexOf(k_GroupOrder, x.schema.Group) is int i && i >= 0 ? i : 99).ThenBy(x => x.schema.Title);
                var at = KitGtkCommands.CanvasCentre(Window);
                menu.AddItem(new GUIContent("Objekt (◆ für mehrere Knoten)"), false, () => CreateNode(typeof(ObjectNode), at));
                menu.AddSeparator("");
                foreach (var t in types)
                {
                    var type = t.type;
                    menu.AddItem(new GUIContent($"{t.schema.Group}/{t.schema.Title}"), false, () => CreateNode(type, at));
                }
                if (!KitGtkCommands.Available) m_Info.text = "Knoten anlegen über diese Liste ist hier nicht verfügbar: Leertaste im Graph benutzen.";
                menu.ShowAsContext();
            }

            public Node CreateNode(Type type, Vector2 canvasPos)
            {
                var node = KitGtkCommands.Create(Window, m_Graph, type, canvasPos, out var error);
                if (node == null) m_Info.text = error + " – Leertaste im Graph öffnet die GTK-Liste.";
                return node;
            }

            static GameObject DraggedSceneObject()
            {
                foreach (var o in DragAndDrop.objectReferences)
                {
                    var go = o as GameObject ?? (o as Component)?.gameObject;
                    if (go && go.scene.IsValid()) return go;      // only scene objects, not prefab assets
                }
                return null;
            }

            void OnDragUpdated(DragUpdatedEvent e)
            {
                if (DraggedSceneObject() == null) return;
                DragAndDrop.visualMode = KitGtkCommands.Available ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (!KitGtkCommands.Available) m_Info.text = "Hineinziehen geht hier nicht: Knoten über „+ Knoten“ oder Leertaste holen, dann das Objekt auf sein ◆-Feld ziehen.";
                e.StopPropagation();
            }

            void OnDragPerform(DragPerformEvent e)
            {
                var go = DraggedSceneObject();
                if (go == null) return;
                DragAndDrop.AcceptDrag();
                e.StopPropagation();
                DropObject(go, KitGtkCommands.ToCanvas(Window, e.mousePosition));
            }

            /// <summary>Hierarchy object → new Objekt node named like it, bound in the chosen instance.</summary>
            public Node DropObject(GameObject go, Vector2 canvasPos)
            {
                var node = CreateNode(typeof(ObjectNode), canvasPos);
                if (node == null) return null;
                try { KitGraphEditing.SetOption(node, ObjectNode.NameOption, go.name); } catch { /* name stays "Objekt" */ }
                var inst = Instance;
                if (inst) KitBinding.Set(inst, KitIds.Of(node), go);
                m_Info.text = inst ? $"◆ Objekt „{go.name}“ angelegt und in „{inst.name}“ gebunden. Jetzt seinen Ausgang an ◆-Eingänge ziehen."
                                   : $"◆ Objekt „{go.name}“ angelegt. Zum Binden unten eine Instanz wählen.";
                InstanceChanged();
                return node;
            }

            void PickInstance()
            {
                var menu = new GenericMenu();
                var list = m_Runtime ? KitBinding.InstancesOf(m_Runtime) : Array.Empty<Interaction>();
                foreach (var it in list)
                {
                    var i = it;
                    menu.AddItem(new GUIContent(PathName(i.transform)), i == Instance, () => KitInstances.Select(Path, i));
                }
                if (list.Length > 0) menu.AddSeparator("");
                menu.AddItem(new GUIContent("Neue Interaktion in der Szene mit diesem Graph"), false, () =>
                {
                    var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(Path));
                    Undo.RegisterCreatedObjectUndo(go, "Kit: Interaktion anlegen");
                    var it = Undo.AddComponent<Interaction>(go);
                    KitBinding.AssignGraph(it, m_Runtime);
                    KitInstances.Select(Path, it);
                    Selection.activeGameObject = go;
                });
                menu.ShowAsContext();
            }

            static string PathName(Transform t) => t.parent ? PathName(t.parent) + "/" + t.name : t.name;
        }
    }
}
