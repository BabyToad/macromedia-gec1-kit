using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Stable id of a GTK node (its GUID). GTK 0.4 keeps it internal, so we read it by reflection.</summary>
    public static class KitIds
    {
        static readonly FieldInfo s_Impl = typeof(Node).GetField("m_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);
        public static string Of(Node n)
        {
            var impl = s_Impl?.GetValue(n);
            return impl?.GetType().GetProperty("Guid", BindingFlags.Instance | BindingFlags.Public)?.GetValue(impl)?.ToString();
        }
        public static object ModelOf(Node n) => s_Impl?.GetValue(n);
    }

    /// <summary>
    /// The editor side of a kit node. It builds ports and settings from the runtime class's attributes,
    /// so each node is one line below. Port ids are the C# member names; labels are German.
    /// </summary>
    [Serializable]
    public abstract class KitEditorNode : Node
    {
        public abstract Type RuntimeType { get; }
        public NodeSchema Schema => NodeSchema.Of(RuntimeType);

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            var defaults = Activator.CreateInstance(RuntimeType);
            foreach (var s in Schema.Settings)
                context.AddOption(s.Id, s.Type).WithDisplayName(s.Label).WithDefaultValue(s.Get(defaults)).Delayed().Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            var defaults = Activator.CreateInstance(RuntimeType);
            foreach (var p in Schema.Inputs)
                context.AddInputPort<EventPort>(p.Id).WithDisplayName(p.Label).WithConnectorUI(PortConnectorUI.Arrowhead).Build();
            foreach (var p in Schema.DataIns)
                context.AddInputPort(p.Id).WithDataType(p.Type).WithDisplayName(p.Label).WithDefaultValue(p.Get(defaults)).Build();
            foreach (var p in Schema.Refs)
                context.AddInputPort<RefPort>(p.Id).WithDisplayName("◆ " + p.Label).Build();

            foreach (var p in Schema.Outputs)
                context.AddOutputPort<EventPort>(p.Id).WithDisplayName(p.Label).WithConnectorUI(PortConnectorUI.Arrowhead).Build();
            foreach (var p in Schema.DataOuts)
                context.AddOutputPort(p.Id).WithDataType(p.Type).WithDisplayName(p.Label).Build();
            foreach (var p in Schema.RefOuts)
                context.AddOutputPort<RefPort>(p.Id).WithDisplayName("◆ " + p.Label).Build();
        }

        /// <summary>A setting's value as stored in the graph.</summary>
        public object GetSetting(NodeSchema.Port s)
        {
            var o = GetNodeOptionByName(s.Id);
            return o == null ? null : KitValues.Read(o, s.Type);
        }
    }

    /// <summary>
    /// "Objekt": one scene object that several nodes use. Its ◆ output goes to each of them; the
    /// interaction's binding table says which object it is. (A node that alone uses an object binds it
    /// directly on its own ◆ chip and needs no Objekt node.)
    /// </summary>
    [Serializable]
    public class ObjectNode : Node
    {
        public const string Out = "object", NameOption = "label";

        protected override void OnDefineOptions(IOptionDefinitionContext context) =>
            context.AddOption<string>(NameOption).WithDisplayName("Name").WithDefaultValue("Objekt").Delayed().Build();

        protected override void OnDefinePorts(IPortDefinitionContext context) =>
            context.AddOutputPort<RefPort>(Out).WithDisplayName("◆ Objekt").Build();

        public string Label
        {
            get { var o = GetNodeOptionByName(NameOption); return o != null && o.TryGetValue(out string s) && !string.IsNullOrEmpty(s) ? s : "Objekt"; }
        }
    }

    /// <summary>Reads typed values from GTK options and ports without knowing the type at compile time.</summary>
    public static class KitValues
    {
        static readonly MethodInfo s_OptionGet = typeof(INodeOption).GetMethod(nameof(INodeOption.TryGetValue));
        static readonly MethodInfo s_PortGet = typeof(IPort).GetMethod(nameof(IPort.TryGetValue));

        public static object Read(INodeOption o, Type t) => Invoke(s_OptionGet, o, t);
        public static object Read(IPort p, Type t) => Invoke(s_PortGet, p, t);

        static object Invoke(MethodInfo generic, object target, Type t)
        {
            var args = new object[] { null };
            bool ok = (bool)generic.MakeGenericMethod(t).Invoke(target, args);
            return ok ? args[0] : null;
        }
    }

    // ---- one line per node: the editor face of each runtime node ---------------------------------
    [Serializable] public class ZoneNode : KitEditorNode { public override Type RuntimeType => typeof(Zone); }
    [Serializable] public class InteractNode : KitEditorNode { public override Type RuntimeType => typeof(Interact); }
    [Serializable] public class KeyNode : KitEditorNode { public override Type RuntimeType => typeof(Key); }
    [Serializable] public class CollideNode : KitEditorNode { public override Type RuntimeType => typeof(Collide); }
    [Serializable] public class ProximityNode : KitEditorNode { public override Type RuntimeType => typeof(Proximity); }
    [Serializable] public class BeginNode : KitEditorNode { public override Type RuntimeType => typeof(Begin); }
    [Serializable] public class TimerNode : KitEditorNode { public override Type RuntimeType => typeof(Timer); }
    [Serializable] public class SequenceNode : KitEditorNode { public override Type RuntimeType => typeof(Sequence); }
    [Serializable] public class WaitNode : KitEditorNode { public override Type RuntimeType => typeof(Wait); }
    [Serializable] public class OnceNode : KitEditorNode { public override Type RuntimeType => typeof(Once); }
    [Serializable] public class CooldownNode : KitEditorNode { public override Type RuntimeType => typeof(Cooldown); }
    [Serializable] public class RandomPickNode : KitEditorNode { public override Type RuntimeType => typeof(RandomPick); }
    [Serializable] public class BranchNode : KitEditorNode { public override Type RuntimeType => typeof(Branch); }
    [Serializable] public class FlagNode : KitEditorNode { public override Type RuntimeType => typeof(Flag); }
    [Serializable] public class CounterNode : KitEditorNode { public override Type RuntimeType => typeof(Counter); }
    [Serializable] public class CompareNode : KitEditorNode { public override Type RuntimeType => typeof(Compare); }
    [Serializable] public class SetActiveNode : KitEditorNode { public override Type RuntimeType => typeof(SetActive); }
    [Serializable] public class MoveNode : KitEditorNode { public override Type RuntimeType => typeof(Move); }
    [Serializable] public class PlaySoundNode : KitEditorNode { public override Type RuntimeType => typeof(PlaySound); }
    [Serializable] public class PushNode : KitEditorNode { public override Type RuntimeType => typeof(Push); }
    [Serializable] public class SpawnNode : KitEditorNode { public override Type RuntimeType => typeof(Spawn); }
    [Serializable] public class SetTextNode : KitEditorNode { public override Type RuntimeType => typeof(SetText); }
    [Serializable] public class LoadSceneNode : KitEditorNode { public override Type RuntimeType => typeof(LoadScene); }
    [Serializable] public class NoteNode : KitEditorNode { public override Type RuntimeType => typeof(Note); }

    // ---- Spielsysteme -------------------------------------------------------------------------------
    [Serializable] public class HealthNode : KitEditorNode { public override Type RuntimeType => typeof(Health); }
    [Serializable] public class InventoryNode : KitEditorNode { public override Type RuntimeType => typeof(Inventory); }
    [Serializable] public class DialogNode : KitEditorNode { public override Type RuntimeType => typeof(Dialog); }
    [Serializable] public class CheckpointNode : KitEditorNode { public override Type RuntimeType => typeof(Checkpoint); }
    [Serializable] public class HudDisplayNode : KitEditorNode { public override Type RuntimeType => typeof(HudDisplay); }
    [Serializable] public class WalkToNode : KitEditorNode { public override Type RuntimeType => typeof(WalkTo); }
    [Serializable] public class SharedFlagNode : KitEditorNode { public override Type RuntimeType => typeof(SharedFlag); }

    // ---- Erweiterung --------------------------------------------------------------------------------
    [Serializable] public class AnimatorValueNode : KitEditorNode { public override Type RuntimeType => typeof(AnimatorValue); }
    [Serializable] public class SwapMaterialNode : KitEditorNode { public override Type RuntimeType => typeof(SwapMaterial); }
}
