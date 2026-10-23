using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kit.Editor
{
    /// <summary>
    /// v0.3.2 → v0.4 for the state nodes: Merker, Zähler, Gemeinsamer Merker, Lebenspunkte and HUD-Anzeige become the
    /// Variable nodes (Ja-Nein, Zahl) and Wert anzeigen; the components Lebenspunkte and Gemeinsamer Zustand become
    /// Variable components. Node ids stay the same, so every binding in every scene keeps pointing at its object.
    /// Order matters: scenes first (bindings to prefab components still find their object), then prefabs, then graphs
    /// (scenes and prefabs still read the old graphs, e.g. a HUD-Anzeige's text).
    /// </summary>
    public static class WerteMigration
    {
        // ---- the mapping -------------------------------------------------------------------------------------
        /// <summary>How one old editor node becomes a new one.</summary>
        public sealed class NodeMap
        {
            public Type From, To;
            public Dictionary<string, string> Ports = new Dictionary<string, string>();     // old port id → new (missing = dropped)
            public Dictionary<string, string> Options = new Dictionary<string, string>();   // old option id → new
            public Action<Node, Node> Extra;                                                 // (old, new): settings that need more than a rename
            public string Note;                                                              // what changes in behaviour (for the report)
        }

        static Dictionary<string, string> D(params string[] pairs)
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) d[pairs[i]] = pairs[i + 1];
            return d;
        }

        public static readonly NodeMap[] Maps =
        {
            new NodeMap
            {
                From = typeof(FlagNode), To = typeof(FlagVariableNode),
                Ports = D("SetTrue", "SetTrue", "SetFalse", "SetFalse", "Toggle", "Toggle", "becameTrue", "becameTrue", "becameFalse", "becameFalse", "value", "Value"),
                Options = D("label", "label", "startValue", "startValue"),
                Extra = (o, n) => KitGraphEditing.SetOption(n, "scope", Scope.Logik),
            },
            new NodeMap
            {
                From = typeof(CounterNode), To = typeof(NumberVariableNode),
                Ports = D("Add", "Increase", "Subtract", "Decrease", "ResetCounter", "ResetValue", "reached", "reachedTarget", "Value", "Value"),
                Extra = (o, n) =>
                {
                    KitGraphEditing.SetOption(n, "scope", Scope.Logik);
                    KitGraphEditing.SetOption(n, "label", Read<string>(o, "label") ?? "Zähler");
                    KitGraphEditing.SetOption(n, "start", (float)(Read<int?>(o, "start") ?? 0));
                    KitGraphEditing.SetOption(n, "goal", (float)(Read<int?>(o, "target") ?? 3));
                },
                Note = "Zähler → Zahl: „Ziel erreicht“ heißt „Wert erreicht“ und feuert auch, wenn die Zahl über das Ziel springt.",
            },
            new NodeMap
            {
                From = typeof(SharedFlagNode), To = typeof(FlagVariableNode),
                Ports = D("owner", "target", "SetTrue", "SetTrue", "SetFalse", "SetFalse", "Toggle", "Toggle", "becameTrue", "becameTrue", "becameFalse", "becameFalse", "Value", "Value"),
                Options = D("label", "label"),
                Extra = (o, n) => KitGraphEditing.SetOption(n, "scope", Scope.Szene),
                Note = "Gemeinsamer Merker → Ja-Nein (Bereich Szene) am Objekt des Besitzers. Die Schreibrechte („nur diese Interaktionen“) gibt es nicht mehr.",
            },
            new NodeMap
            {
                From = typeof(HealthNode), To = typeof(NumberVariableNode),
                Ports = D("target", "target", "amount", "step", "Damage", "Decrease", "Heal", "Increase", "Revive", "ResetValue",
                          "hurt", "changed", "healed", "changed", "died", "reachedMin", "points", "Value"),
                Extra = (o, n) =>
                {
                    KitGraphEditing.SetOption(n, "label", HealthName);
                    KitGraphEditing.SetOption(n, "scope", Scope.Szene);
                    KitGraphEditing.SetOption(n, "limited", true);
                },
                Note = "Lebenspunkte → Zahl „Lebenspunkte“ am Objekt: Schaden = Verringern, Heilen = Erhöhen, Wiederbeleben = Zurücksetzen, Tot = Minimum erreicht. „Getroffen“ und „Geheilt“ sind jetzt beide „Geändert“; Heilen bei 0 ist nicht mehr gesperrt.",
            },
            new NodeMap
            {
                From = typeof(HudDisplayNode), To = typeof(ShowValueNode),
                Ports = D("field", "view", "number", "value"),
                Note = "HUD-Anzeige → Wert anzeigen: das Textfeld bekommt eine Zahl-Anzeige mit dem alten Text als Format.",
            },
        };

        public const string HealthName = "Lebenspunkte";

        static T Read<T>(Node n, string option)
        {
            var o = n.GetNodeOptionByName(option);
            if (o == null) return default;
            var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            var v = KitValues.Read(o, t);
            return v == null ? default : (T)v;
        }

        // ---- scenes and prefabs ------------------------------------------------------------------------------
        /// <summary>Report of one run: what changed, what to look at.</summary>
        public sealed class Report
        {
            public readonly List<string> Changed = new List<string>(), Notes = new List<string>();
            public override string ToString() => string.Join("\n", Changed.Concat(Notes.Select(n => "Hinweis: " + n)));
        }

        /// <summary>Old components under <paramref name="root"/> → Variable components; bindings follow. True if anything changed.</summary>
        public static bool MigrateObjects(IEnumerable<GameObject> roots, Report report, string where)
        {
            bool any = false;
            var replaced = new Dictionary<Object, GameObject>();   // old component → its object (bindings retarget)
            foreach (var root in roots)
            {
                foreach (var hp in root.GetComponentsInChildren<KitHealth>(true))
                {
                    replaced[hp] = hp.gameObject;                    // bindings to it point at the object from now on
                    if (FromPrefab(hp)) continue;                    // the prefab asset itself is migrated on its own (after the scenes)
                    var v = Undo.AddComponent<KitVariable>(hp.gameObject);
                    v.Configure(UniqueName(hp.gameObject, HealthName), ValueKind.Zahl, ObjectScope.Szene,
                                startNumber: hp.start, limited: true, min: 0, max: hp.max);
                    EditorUtility.SetDirty(v);
                    report.Changed.Add($"{where}: „{hp.name}“ Lebenspunkte → Variable „{v.ValueName}“ (0 … {KitValue.FormatNumber(hp.max)}, Start {KitValue.FormatNumber(hp.start)})");
                    any = true;
                }
                foreach (var st in root.GetComponentsInChildren<KitSharedState>(true))
                {
                    replaced[st] = st.gameObject;
                    if (FromPrefab(st)) continue;
                    foreach (var e in st.entries)
                    {
                        var v = Undo.AddComponent<KitVariable>(st.gameObject);
                        v.Configure(UniqueName(st.gameObject, e.name), ValueKind.JaNein, ObjectScope.Szene, startFlag: e.startValue);
                        EditorUtility.SetDirty(v);
                        report.Changed.Add($"{where}: „{st.name}“ Gemeinsamer Merker „{e.name}“ → Variable (Ja-Nein, Start {(e.startValue ? "ja" : "nein")})");
                        if (e.writers.Count > 0) report.Notes.Add($"{where}: „{e.name}“ an „{st.name}“ hatte Schreibrechte für {e.writers.Count} Interaktion(en); jetzt darf jede schreiben.");
                    }
                    any = true;
                }
            }
            foreach (var root in roots)
                foreach (var logic in root.GetComponentsInChildren<Logic>(true))
                    any |= MigrateBindings(logic, replaced, report, where);
            foreach (var old in replaced.Keys) if (!FromPrefab((Component)old)) Undo.DestroyObjectImmediate(old);
            return any;
        }

        static bool FromPrefab(Component c) => PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c);

        static string UniqueName(GameObject go, string wanted)
        {
            string name = wanted; int i = 2;
            while (go.GetComponents<KitVariable>().Any(v => v.ValueName == name)) name = $"{wanted} {i++}";
            return name;
        }

        /// <summary>Rewrites one Logik's bindings: renamed ◆ fields, components that are gone, HUD text fields.</summary>
        static bool MigrateBindings(Logic logic, Dictionary<Object, GameObject> replaced, Report report, string where)
        {
            var graph = logic.Graph;
            if (graph == null) return false;
            var so = new SerializedObject(logic);
            var list = so.FindProperty("bindings");
            bool changed = false;
            SerializedProperty Target(string slotId)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    var b = list.GetArrayElementAtIndex(i);
                    if (b.FindPropertyRelative("slot").stringValue == slotId) return b.FindPropertyRelative("target");
                }
                return null;
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                var b = list.GetArrayElementAtIndex(i);
                var slot = b.FindPropertyRelative("slot");
                var target = b.FindPropertyRelative("target");
                var t = target.objectReferenceValue;
                if (t != null && replaced.TryGetValue(t, out var go)) { target.objectReferenceValue = go; changed = true; }
                // Inline chips are named after the field; two fields were renamed.
                int cut = slot.stringValue.LastIndexOf('/');
                if (cut < 0) continue;
                string nodeId = slot.stringValue.Substring(0, cut), field = slot.stringValue.Substring(cut + 1);
                var node = graph.nodes.Find(n => n != null && n.id == nodeId);
                if (node is SharedFlag && field == "owner") { slot.stringValue = nodeId + "/target"; changed = true; }
                else if (node is HudDisplay && field == "field") { slot.stringValue = nodeId + "/view"; changed = true; }
            }
            // Each HUD-Anzeige's text field gets a Zahl-Anzeige, however it was bound: inline chip or named Rolle.
            for (int n = 0; n < graph.nodes.Count; n++)
            {
                if (!(graph.nodes[n] is HudDisplay hud)) continue;
                string slotId = graph.SlotFor(n, "field");
                if (slotId == LogicGraph.InlineSlotId(hud.id, "field")) slotId = LogicGraph.InlineSlotId(hud.id, "view");
                var target = Target(slotId);
                var bound = target?.objectReferenceValue;
                var tmp = bound is GameObject g ? g.GetComponent<TMPro.TMP_Text>() : bound is Component c ? c.GetComponent<TMPro.TMP_Text>() : null;
                if (tmp == null) continue;
                var view = tmp.GetComponent<KitNumberView>() ?? Undo.AddComponent<KitNumberView>(tmp.gameObject);
                view.field = tmp; view.format = hud.format;
                EditorUtility.SetDirty(view);
                // A named Rolle may feed other nodes too: they convert the object to what they need.
                target.objectReferenceValue = view;
                report.Changed.Add($"{where}: Textfeld „{tmp.name}“ → Zahl-Anzeige (Format „{hud.format}“)");
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ---- overrides of prefab instances and variants ------------------------------------------------------
        // A scene instance (or a variant) may override a Lebenspunkte start/max or a Gemeinsamer-Merker entry of its
        // prefab. The prefab's own component becomes a Variable in the prefab pass; these overrides are collected
        // before anything is deleted and put on that Variable afterwards, again as overrides.
        sealed class Override
        {
            public string container;            // scene or prefab path
            public int[] path;                  // sibling indices from the root
            public float? start, max;           // Lebenspunkte
            // Gemeinsamer Zustand: the prefab's entry names, and the instance's whole list (it may add or remove entries)
            public List<string> sourceNames;
            public List<(string name, bool start)> entries;
        }

        static int[] PathOf(Transform t)
        {
            var p = new List<int>();
            for (; t != null; t = t.parent) p.Insert(0, t.GetSiblingIndex());
            return p.ToArray();
        }

        static GameObject At(IList<GameObject> roots, int[] path)
        {
            var root = roots.FirstOrDefault(r => r.transform.GetSiblingIndex() == path[0]);
            if (root == null) return null;
            var t = root.transform;
            for (int i = 1; i < path.Length; i++) { if (path[i] >= t.childCount) return null; t = t.GetChild(path[i]); }
            return t.gameObject;
        }

        static void CollectOverrides(IEnumerable<GameObject> roots, string container, List<Override> into)
        {
            foreach (var root in roots)
            {
                foreach (var hp in root.GetComponentsInChildren<KitHealth>(true))
                {
                    if (!FromPrefab(hp) || !(PrefabUtility.GetCorrespondingObjectFromSource(hp) is KitHealth src)) continue;
                    if (Mathf.Approximately(src.start, hp.start) && Mathf.Approximately(src.max, hp.max)) continue;
                    into.Add(new Override { container = container, path = PathOf(hp.transform), start = hp.start, max = hp.max });
                }
                foreach (var st in root.GetComponentsInChildren<KitSharedState>(true))
                {
                    if (!FromPrefab(st) || !(PrefabUtility.GetCorrespondingObjectFromSource(st) is KitSharedState src)) continue;
                    bool same = st.entries.Count == src.entries.Count;
                    for (int i = 0; same && i < st.entries.Count; i++)
                        same = st.entries[i].name == src.entries[i].name && st.entries[i].startValue == src.entries[i].startValue;
                    if (same) continue;
                    into.Add(new Override
                    {
                        container = container, path = PathOf(st.transform),
                        sourceNames = src.entries.Select(e => e.name).ToList(),
                        entries = st.entries.Select(e => (e.name, e.startValue)).ToList(),
                    });
                }
            }
        }

        // Entry i of the instance belongs to entry i of the prefab (that is how Unity overrides list elements):
        // changed ones are overridden, extra ones become added Variables, missing ones are removed from the instance.
        static bool ApplyEntries(GameObject go, Override o, Report report)
        {
            bool any = false;
            var fromPrefab = o.sourceNames.Select(n => go.GetComponents<KitVariable>().FirstOrDefault(x => x.ValueName == n && x.Kind == ValueKind.JaNein)).ToList();
            for (int i = 0; i < o.entries.Count; i++)
            {
                var (name, start) = o.entries[i];
                var v = i < fromPrefab.Count ? fromPrefab[i] : null;
                if (v == null)
                {
                    v = Undo.AddComponent<KitVariable>(go);
                    v.Configure(UniqueName(go, name), ValueKind.JaNein, ObjectScope.Szene, startFlag: start);
                    EditorUtility.SetDirty(v);
                    report.Changed.Add($"{o.container}: „{go.name}“ behält seinen zusätzlichen Merker „{name}“ (Start {(start ? "ja" : "nein")})");
                    any = true;
                    continue;
                }
                var so = new SerializedObject(v);
                if (so.FindProperty("valueName").stringValue == name && so.FindProperty("startFlag").boolValue == start) continue;
                so.FindProperty("valueName").stringValue = name;
                so.FindProperty("startFlag").boolValue = start;
                so.ApplyModifiedPropertiesWithoutUndo();
                report.Changed.Add($"{o.container}: „{go.name}“ behält seinen Merker „{name}“ (Start {(start ? "ja" : "nein")})");
                any = true;
            }
            for (int i = o.entries.Count; i < fromPrefab.Count; i++)
            {
                if (fromPrefab[i] == null) continue;
                try { Undo.DestroyObjectImmediate(fromPrefab[i]); report.Changed.Add($"{o.container}: „{go.name}“ hatte den Merker „{o.sourceNames[i]}“ entfernt; das bleibt so"); any = true; }
                catch (Exception e) { report.Notes.Add($"{o.container}: „{go.name}“: der Merker „{o.sourceNames[i]}“ ließ sich an der Instanz nicht entfernen ({e.Message}); bitte von Hand entfernen"); }
            }
            return any;
        }

        /// <summary>0 for a plain prefab, 1 for a variant of it, 2 for a variant of that …</summary>
        static int VariantDepth(string path)
        {
            int depth = 0;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant && depth < 32)
            {
                asset = PrefabUtility.GetCorrespondingObjectFromSource(asset);
                depth++;
            }
            return depth;
        }

        static bool ApplyOverrides(IList<GameObject> roots, IEnumerable<Override> overrides, Report report)
        {
            bool any = false;
            foreach (var o in overrides)
            {
                var go = At(roots, o.path);
                if (go == null) { report.Notes.Add($"{o.container}: ein überschriebener Wert ließ sich nicht übertragen (Objekt nicht gefunden)"); continue; }
                if (o.start.HasValue)
                {
                    var v = go.GetComponents<KitVariable>().FirstOrDefault(x => x.ValueName == HealthName);
                    if (v == null) continue;
                    var so = new SerializedObject(v);
                    so.FindProperty("startNumber").floatValue = o.start.Value;
                    so.FindProperty("max").floatValue = o.max.Value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    report.Changed.Add($"{o.container}: „{go.name}“ behält seine eigenen Lebenspunkte (Start {KitValue.FormatNumber(o.start.Value)}, höchstens {KitValue.FormatNumber(o.max.Value)})");
                    any = true;
                }
                if (o.entries != null) any |= ApplyEntries(go, o, report);
            }
            return any;
        }

        // ---- graphs ------------------------------------------------------------------------------------------
        struct Link { public bool output; public string port; public Node other; public string otherPort; public int order; }

        static readonly BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Replaces every old state node in one graph. Returns how many nodes changed.</summary>
        public static int MigrateGraph(KitEditorGraph g, Report report, string where)
        {
            int count = 0;
            var names = LogikNames(g, report, where);
            foreach (var old in g.GetNodes().OfType<KitEditorNode>().ToList())
            {
                var map = Array.Find(Maps, m => m.From == old.GetType());
                if (map == null) continue;
                Replace(g, old, map, report, where);
                if (names.TryGetValue(KitIds.Of(old), out var name))
                {
                    var made = g.GetNodes().OfType<KitEditorNode>().First(n => KitIds.Of(n) == KitIds.Of(old));
                    KitGraphEditing.SetOption(made, "label", name);
                }
                count++;
            }
            if (count > 0) KitGraphEditing.Save(g);
            return count;
        }

        // Up to v0.3.2 every Merker and Zähler kept its own value; the name was only a label. In v0.4 the same name in
        // the same Logik means the same value. So old nodes that share a name get distinct ones ("Zähler", "Zähler 2"),
        // in graph order, avoiding the names Logik variables already use. Node id → new name, only for renamed nodes.
        static Dictionary<string, string> LogikNames(KitEditorGraph g, Report report, string where)
        {
            var renamed = new Dictionary<string, string>();
            var nodes = g.GetNodes().OfType<KitEditorNode>().ToList();
            var taken = new HashSet<string>();
            foreach (var n in nodes)
                if (n is NumberVariableNode || n is FlagVariableNode || n is TextVariableNode)
                {
                    var scope = n.GetSetting(n.Schema.Settings.Find(x => x.Id == "scope"));
                    if (scope is Scope sc && sc == Scope.Logik && n.GetSetting(n.Schema.Settings.Find(x => x.Id == "label")) is string l) taken.Add(l);
                }
            foreach (var n in nodes)
            {
                if (!(n is FlagNode || n is CounterNode)) continue;
                string wanted = n.GetSetting(n.Schema.Settings.Find(x => x.Id == "label")) as string;
                if (string.IsNullOrWhiteSpace(wanted)) wanted = n is FlagNode ? "Merker" : "Zähler";
                string name = wanted; int i = 2;
                while (taken.Contains(name)) name = $"{wanted} {i++}";
                taken.Add(name);
                if (name != wanted)
                {
                    renamed[KitIds.Of(n)] = name;
                    report.Notes.Add($"{where}: zwei Knoten hießen „{wanted}“ und hatten bisher getrennte Werte; einer heißt jetzt „{name}“, damit das so bleibt");
                }
            }
            return renamed;
        }

        static void Replace(KitEditorGraph g, KitEditorNode old, NodeMap map, Report report, string where)
        {
            string id = KitIds.Of(old);
            var model = KitIds.ModelOf(old);
            var position = (Vector2)model.GetType().GetProperty("Position", k_Any).GetValue(model);

            // What the old node knew: wires (with their run order), options, typed-in ● constants.
            var links = new List<Link>();
            var connected = new List<IPort>();
            foreach (var p in old.GetInputPorts())
            {
                connected.Clear(); p.GetConnectedPorts(connected);
                foreach (var c in connected)
                {
                    var other = (Node)c.GetNode();
                    links.Add(new Link { output = false, port = p.name, other = other, otherPort = c.name, order = g.OrderOf(KitIds.Of(other), c.name, id, p.name) });
                }
            }
            foreach (var p in old.GetOutputPorts())
            {
                connected.Clear(); p.GetConnectedPorts(connected);
                foreach (var c in connected)
                {
                    var other = (Node)c.GetNode();
                    links.Add(new Link { output = true, port = p.name, other = other, otherPort = c.name, order = g.OrderOf(id, p.name, KitIds.Of(other), c.name) });
                }
            }
            var options = new Dictionary<string, object>();
            foreach (var s in old.Schema.Settings) options[s.Id] = old.GetSetting(s);
            var constants = new Dictionary<string, object>();
            foreach (var d in old.Schema.DataIns)
            {
                var port = old.GetInputPortByName(d.Id);
                if (port != null && !port.isConnected) constants[d.Id] = KitValues.Read(port, d.Type);
            }

            // Out with the old, in with the new – same id, so slot ids ("id/target") and bindings stay valid.
            var graphModel = typeof(Graph).GetField("m_Implementation", k_Any).GetValue(g);
            var delete = graphModel.GetType().GetMethods(k_Any).First(m => m.Name == "DeleteNodes" && m.GetParameters().Length >= 2);
            var listType = typeof(List<>).MakeGenericType(delete.GetParameters()[0].ParameterType.GetGenericArguments()[0]);
            var toDelete = (System.Collections.IList)Activator.CreateInstance(listType);
            toDelete.Add(model);
            var args = new object[delete.GetParameters().Length];
            args[0] = toDelete; args[1] = true;
            for (int i = 2; i < args.Length; i++) args[i] = delete.GetParameters()[i].DefaultValue;
            delete.Invoke(graphModel, args);

            var made = (KitEditorNode)KitGraphEditing.AddNode(g, map.To, position, Hash128.Parse(id));
            foreach (var kv in map.Options)
                if (options.TryGetValue(kv.Key, out var v) && v != null) KitGraphEditing.SetOption(made, kv.Value, v);
            map.Extra?.Invoke(old, made);   // reads the old node's options (the C# object outlives its model)
            foreach (var kv in constants)
            {
                if (!map.Ports.TryGetValue(kv.Key, out var to) || kv.Value == null) continue;
                var target = made.Schema.DataIn(to);
                if (target == null) continue;
                KitGraphEditing.SetInputValue(made, to, ConvertValue(kv.Value, target.Type));
            }

            var dropped = new HashSet<string>();
            var done = new HashSet<(bool, string, Node, string)>();
            foreach (var l in links.OrderBy(l => l.order))
            {
                if (!map.Ports.TryGetValue(l.port, out var port)) { dropped.Add(old.Schema.LabelOf(l.port)); continue; }
                if (!done.Add((l.output, port, l.other, l.otherPort))) continue;   // Getroffen + Geheilt → one Geändert wire
                try
                {
                    if (l.output) KitGraphEditing.Connect(g, made, port, l.other, l.otherPort);
                    else KitGraphEditing.Connect(g, l.other, l.otherPort, made, port);
                }
                catch (ArgumentException e) { report.Notes.Add($"{where}: Draht „{old.Schema.Title}.{old.Schema.LabelOf(l.port)}“ ließ sich nicht neu verbinden ({e.Message})"); }
            }
            g.SyncWireOrders();
            foreach (var l in links.Where(l => l.order > 0).OrderBy(l => l.order))
            {
                if (!map.Ports.TryGetValue(l.port, out var port)) continue;
                if (l.output) g.SetOrder(id, port, KitIds.Of(l.other), l.otherPort, l.order);
                else g.SetOrder(KitIds.Of(l.other), l.otherPort, id, port, l.order);
            }

            string label = options.TryGetValue("label", out var lab) && lab is string s1 ? $" „{s1}“" : "";
            report.Changed.Add($"{where}: {old.Schema.Title}{label} → {made.Schema.Title}");
            if (dropped.Count > 0) report.Notes.Add($"{where}: {old.Schema.Title}{label}: Drähte an {string.Join(", ", dropped)} gibt es nicht mehr");
            if (map.Note != null && !report.Notes.Contains(map.Note)) report.Notes.Add(map.Note);
        }

        static object ConvertValue(object v, Type t)
        {
            if (t.IsInstanceOfType(v)) return v;
            try { return System.Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return t.IsValueType ? Activator.CreateInstance(t) : null; }
        }

        // ---- the whole project -------------------------------------------------------------------------------
        /// <summary>
        /// Everything under Assets/: prefabs, scenes, then graphs. The open scene setup is restored afterwards.
        /// Safe to run twice (nothing old left = nothing to do).
        /// </summary>
        [KitMigrationStep(10, "Merker, Zähler, Gemeinsamer Merker, Lebenspunkte und HUD-Anzeige werden zu Variablen und Wert anzeigen")]
        static void Step(string folder)
        {
            var report = RunProject(folder);
            if (report.Changed.Count > 0) Debug.Log(KitBrand.Log + "Werte:" + System.Environment.NewLine + report);
        }

        /// <summary>Batch entry for the studio project: migrates everything under Assets/Kit.</summary>
        public static void RunStudio() => Debug.Log(KitBrand.Log + "Werte:" + System.Environment.NewLine + RunProject("Assets/Kit"));

        public static Report RunProject(string folder = "Assets")
        {
            var report = new Report();
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => { var a = AssetDatabase.LoadAssetAtPath<GameObject>(p); return a != null && (a.GetComponentInChildren<KitHealth>(true) != null || a.GetComponentInChildren<KitSharedState>(true) != null || a.GetComponentInChildren<Logic>(true) != null); })
                .ToList();
            var overrides = new List<Override>();

            // 1. Prefabs, read only: what do variants and nested instances override?
            foreach (var path in prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { CollectOverrides(new[] { root }, path, overrides); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // 2. Scenes: overrides, then bindings point at objects and the scene's own components are replaced.
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    CollectOverrides(roots, path, overrides);
                    if (MigrateObjects(roots, report, path)) EditorSceneManager.SaveScene(scene);
                }
                // 3. Prefabs: their components and bindings.
                foreach (var path in prefabs)
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { if (MigrateObjects(new[] { root }, report, path)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                // 4. The overrides go onto the new Variables, as overrides again: base prefabs before the variants built
                // on them, scenes last. A variant's overrides name its parent's entries as the parent ends up.
                foreach (var group in overrides.GroupBy(o => o.container).OrderBy(g => g.Key.EndsWith(".unity") ? int.MaxValue : VariantDepth(g.Key)).ThenBy(g => g.Key))
                {
                    if (group.Key.EndsWith(".unity"))
                    {
                        var scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Single);
                        if (ApplyOverrides(scene.GetRootGameObjects(), group.ToList(), report)) EditorSceneManager.SaveScene(scene);
                    }
                    else
                    {
                        var root = PrefabUtility.LoadPrefabContents(group.Key);
                        try { if (ApplyOverrides(new[] { root }, group.ToList(), report)) PrefabUtility.SaveAsPrefabAsset(root, group.Key); }
                        finally { PrefabUtility.UnloadPrefabContents(root); }
                    }
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }

            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(folder + "/") && p.EndsWith("." + KitEditorGraph.Extension)).ToList())
            {
                var g = GraphDatabase.LoadGraph<KitEditorGraph>(path);
                if (g != null) MigrateGraph(g, report, path);
            }
            return report;
        }
    }
}
