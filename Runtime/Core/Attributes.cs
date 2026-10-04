using System;

namespace Kit
{
    // Attributes describe a node to the graph editor. The editor reads them to build ports and
    // settings; the runtime reads them to wire everything up. A node is a plain C# class with
    // fields and methods carrying these attributes.

    /// <summary>German title, palette group and one-line summary shown on the node.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeInfoAttribute : Attribute
    {
        public readonly string Title, Group, Summary;
        public NodeInfoAttribute(string title, string group, string summary = "") { Title = title; Group = group; Summary = summary; }
    }

    /// <summary>▶ Event input. Put it on a method <c>void Name(Signal signal)</c>.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InputAttribute : Attribute
    {
        public readonly string Label;
        public InputAttribute(string label) { Label = label; }
    }

    /// <summary>▶ Event output. Put it on a field of type <see cref="Output"/>.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OutputAttribute : Attribute
    {
        public readonly string Label;
        public OutputAttribute(string label) { Label = label; }
    }

    /// <summary>
    /// ◆ Reference input: a scene object. Put it on a GameObject or Component field. The value comes
    /// from the interaction's binding table (or from a ◆ wire). Missing and not optional = the node fails.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RefAttribute : Attribute
    {
        public readonly string Label;
        public bool Optional;
        /// <summary>
        /// The node needs this object for its whole life (it listens to it or checks it every frame), so it must be
        /// a fixed binding, not a value that only arrives with an event (like ◆ Wer).
        /// </summary>
        public bool Permanent;
        public RefAttribute(string label) { Label = label; }
    }

    /// <summary>◆ Reference output that belongs to one event (e.g. "Wer" kam in die Zone).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RefOutAttribute : Attribute
    {
        public readonly string Label;
        public RefOutAttribute(string label) { Label = label; }
    }

    /// <summary>● Data input. The field's value is the constant typed on the node, or the connected ● wire.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DataInAttribute : Attribute
    {
        public readonly string Label;
        public DataInAttribute(string label) { Label = label; }
    }

    /// <summary>
    /// ● Data output. On a field or property. <see cref="EventLocal"/>: the value travels with the event
    /// (later events cannot overwrite it while an earlier one is still waiting).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DataOutAttribute : Attribute
    {
        public readonly string Label;
        public bool EventLocal;
        public DataOutAttribute(string label) { Label = label; }
    }

    /// <summary>A setting shown on the node and stored in the graph (float, int, bool, string, enum, Vector3, assets).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SettingAttribute : Attribute
    {
        public readonly string Label;
        public SettingAttribute(string label) { Label = label; }
    }
}
