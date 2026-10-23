using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Kit.Editor
{
    /// <summary>
    /// Routes the kit's [Hint] tooltips into Graph Toolkit. Settings get theirs publicly (IOptionBuilder.WithTooltip,
    /// in <see cref="KitEditorNode"/>). GTK 0.4 has no public tooltip for ports, so the port model's (unserialized)
    /// tooltip override is set by reflection; GTK's own port views then show it. The node tooltip is serialized by
    /// GTK, so it is not written into the model: the bridge puts it on the node's title view instead.
    /// If the internals are missing, nothing breaks: ports show GTK's default text.
    /// </summary>
    public static class KitTooltips
    {
        static readonly Dictionary<Type, FieldInfo> s_Fields = new Dictionary<Type, FieldInfo>();

        static FieldInfo OverrideField(Type t)
        {
            if (s_Fields.TryGetValue(t, out var f)) return f;
            for (var x = t; x != null && f == null; x = x.BaseType)
                f = x.GetField("m_TooltipOverride", BindingFlags.Instance | BindingFlags.NonPublic);
            return s_Fields[t] = f;
        }

        /// <summary>The tooltip text for one port of a node (its [Hint]), or null.</summary>
        public static string ForPort(Node n, IPort port)
        {
            if (n is KitEditorNode k)
            {
                foreach (var p in k.Schema.All)
                    if (p.Id == port.name) return p.Tooltip;
                return null;
            }
            if (n is SelfNode) return SelfNode.Tip;
            if (n is ObjectNode) return ObjectNode.Tip;
            return null;
        }

        /// <summary>Writes every port's tooltip into its model. True if all could be set.</summary>
        public static bool Apply(Node n)
        {
            bool ok = true;
            foreach (var port in Ports(n))
            {
                var text = ForPort(n, port);
                if (text == null) continue;
                var f = OverrideField(port.GetType());
                if (f == null) { ok = false; continue; }
                if (!Equals(f.GetValue(port), text)) f.SetValue(port, text);
            }
            return ok;
        }

        static IEnumerable<IPort> Ports(Node n)
        {
            foreach (var p in n.GetInputPorts()) yield return p;
            foreach (var p in n.GetOutputPorts()) yield return p;
        }
    }
}
