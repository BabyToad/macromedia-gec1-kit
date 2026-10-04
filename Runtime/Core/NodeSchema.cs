using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kit
{
    /// <summary>What a node type offers: its ports and settings, read once from its attributes.</summary>
    public sealed class NodeSchema
    {
        public sealed class Port
        {
            public string Id, Label;
            public Type Type;          // data or reference type; null for ▶ events
            public MemberInfo Member;
            public bool Optional, EventLocal, Permanent;
            public object Get(object node) => Member is FieldInfo f ? f.GetValue(node) : ((PropertyInfo)Member).GetValue(node);
            public void Set(object node, object v) { if (Member is FieldInfo f) f.SetValue(node, v); }
        }

        public readonly Type NodeType;
        public readonly string Title, Group, Summary;
        public readonly List<Port> Inputs = new List<Port>();     // ▶ methods
        public readonly List<Port> Outputs = new List<Port>();    // ▶ Output fields
        public readonly List<Port> Refs = new List<Port>();       // ◆ in
        public readonly List<Port> RefOuts = new List<Port>();    // ◆ out (event-local)
        public readonly List<Port> DataIns = new List<Port>();    // ● in
        public readonly List<Port> DataOuts = new List<Port>();   // ● out
        public readonly List<Port> Settings = new List<Port>();

        static readonly Dictionary<Type, NodeSchema> s_Cache = new Dictionary<Type, NodeSchema>();
        const BindingFlags k_Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static NodeSchema Of(Type t)
        {
            if (!s_Cache.TryGetValue(t, out var s)) s_Cache[t] = s = new NodeSchema(t);
            return s;
        }

        NodeSchema(Type t)
        {
            NodeType = t;
            var info = t.GetCustomAttribute<NodeInfoAttribute>();
            Title = info?.Title ?? t.Name; Group = info?.Group ?? "Eigene"; Summary = info?.Summary ?? "";
            // Declaration order matters (ports appear in that order), so walk base types first.
            var chain = new List<Type>();
            for (var x = t; x != null && x != typeof(object); x = x.BaseType) chain.Insert(0, x);
            foreach (var type in chain)
            {
                foreach (var m in type.GetMethods(k_Flags | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken))
                {
                    var a = m.GetCustomAttribute<InputAttribute>();
                    if (a != null) Inputs.Add(new Port { Id = m.Name, Label = a.Label, Member = m });
                }
                foreach (var f in type.GetFields(k_Flags | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken))
                {
                    if (f.GetCustomAttribute<OutputAttribute>() is OutputAttribute o) Outputs.Add(new Port { Id = f.Name, Label = o.Label, Member = f });
                    if (f.GetCustomAttribute<RefAttribute>() is RefAttribute r) Refs.Add(new Port { Id = f.Name, Label = r.Label, Type = f.FieldType, Member = f, Optional = r.Optional, Permanent = r.Permanent });
                    if (f.GetCustomAttribute<RefOutAttribute>() is RefOutAttribute ro) RefOuts.Add(new Port { Id = f.Name, Label = ro.Label, Type = f.FieldType, Member = f, EventLocal = true });
                    if (f.GetCustomAttribute<DataInAttribute>() is DataInAttribute di) DataIns.Add(new Port { Id = f.Name, Label = di.Label, Type = f.FieldType, Member = f });
                    if (f.GetCustomAttribute<DataOutAttribute>() is DataOutAttribute d) DataOuts.Add(new Port { Id = f.Name, Label = d.Label, Type = f.FieldType, Member = f, EventLocal = d.EventLocal });
                    if (f.GetCustomAttribute<SettingAttribute>() is SettingAttribute st) Settings.Add(new Port { Id = f.Name, Label = st.Label, Type = f.FieldType, Member = f });
                }
                foreach (var p in type.GetProperties(k_Flags | BindingFlags.DeclaredOnly).OrderBy(p => p.MetadataToken))
                    if (p.GetCustomAttribute<DataOutAttribute>() is DataOutAttribute d) DataOuts.Add(new Port { Id = p.Name, Label = d.Label, Type = p.PropertyType, Member = p, EventLocal = d.EventLocal });
            }
        }

        public Port Input(string id) => Inputs.Find(p => p.Id == id);
        public Port Output(string id) => Outputs.Find(p => p.Id == id);
        public Port Ref(string id) => Refs.Find(p => p.Id == id);
        public Port RefOut(string id) => RefOuts.Find(p => p.Id == id);
        public Port DataIn(string id) => DataIns.Find(p => p.Id == id);
        public Port DataOut(string id) => DataOuts.Find(p => p.Id == id);

        /// <summary>Label for any port id on this node (for traces and error messages).</summary>
        public string LabelOf(string id) =>
            (Input(id) ?? Output(id) ?? Ref(id) ?? RefOut(id) ?? DataIn(id) ?? DataOut(id))?.Label ?? id;
    }
}
