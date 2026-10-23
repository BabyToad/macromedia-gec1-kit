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
            {
                var option = context.AddOption(s.Id, s.Type).WithDisplayName(s.Shown).WithDefaultValue(s.Get(defaults)).Delayed();
                if (s.Tooltip != null) option = option.WithTooltip(s.Tooltip);
                option.Build();
            }
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            var defaults = Activator.CreateInstance(RuntimeType);
            foreach (var p in Schema.Inputs)
                context.AddInputPort<EventPort>(p.Id).WithDisplayName(p.Label).WithConnectorUI(PortConnectorUI.Arrowhead).Build();
            foreach (var p in Schema.DataIns)
                context.AddInputPort(p.Id).WithDataType(p.Type).WithDisplayName(p.Shown).WithDefaultValue(p.Get(defaults)).Build();
            foreach (var p in Schema.Refs)
                context.AddInputPort<RefPort>(p.Id).WithDisplayName("◆ " + p.Label).Build();

            foreach (var p in Schema.Outputs)
                context.AddOutputPort<EventPort>(p.Id).WithDisplayName(p.Label).WithConnectorUI(PortConnectorUI.Arrowhead).Build();
            foreach (var p in Schema.DataOuts)
                context.AddOutputPort(p.Id).WithDataType(p.Type).WithDisplayName(p.Shown).Build();
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
    /// A named Rolle: one scene object that several nodes use ("Tür"). Its ◆ output goes to each of them; the
    /// Logik's role table says which object plays it. (A node that alone uses an object has its own ◆ chip and
    /// needs no Rolle node.) C# name kept from v0.3 ("Objekt") so existing graph files load unchanged.
    /// </summary>
    [Serializable]
    public class ObjectNode : Node
    {
        public const string Out = "object", NameOption = "label", DefaultLabel = "Rolle";
        public const string Tip = "◆ Eine Rolle: ein Objekt aus der Szene, das mehrere Knoten benutzen.\n" +
                                  "Welches Objekt die Rolle spielt, steht in der Logik-Komponente (Inspector) oder auf dem Chip.";

        protected override void OnDefineOptions(IOptionDefinitionContext context) =>
            context.AddOption<string>(NameOption).WithDisplayName("Name").WithDefaultValue(DefaultLabel)
                .WithTooltip("Name der Rolle, z.B. „Tür“ oder „Schlüssel“. So heißt sie im Inspector und auf dem Objekt.").Delayed().Build();

        protected override void OnDefinePorts(IPortDefinitionContext context) =>
            context.AddOutputPort<RefPort>(Out).WithDisplayName("◆").Build();

        public string Label
        {
            get
            {
                var o = GetNodeOptionByName(NameOption);
                // v0.3 graphs stored the old default "Objekt": show it as the new default.
                return o != null && o.TryGetValue(out string s) && !string.IsNullOrEmpty(s) && s != "Objekt" ? s : DefaultLabel;
            }
        }
    }

    /// <summary>
    /// "Dieses Objekt": the GameObject the Logik sits on, like <c>gameObject</c> in a script. Nothing to bind: the
    /// same graph on two doors means each door itself. New graphs start with one.
    /// </summary>
    [Serializable]
    public class SelfNode : Node
    {
        public const string Out = "self";
        public const string Tip = "◆ Dieses Objekt: das Objekt, auf dem die Logik-Komponente sitzt (im Code: gameObject).\n" +
                                  "Muss nicht gebunden werden. Liegt derselbe Graph auf zwei Türen, ist es jeweils die eigene Tür.";

        protected override void OnDefinePorts(IPortDefinitionContext context) =>
            context.AddOutputPort<RefPort>(Out).WithDisplayName("◆").Build();
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
    [Serializable] public class CompareNode : KitEditorNode { public override Type RuntimeType => typeof(Compare); }
    [Serializable] public class SetActiveNode : KitEditorNode { public override Type RuntimeType => typeof(SetActive); }
    [Serializable] public class MoveNode : KitEditorNode { public override Type RuntimeType => typeof(Move); }
    [Serializable] public class PlaySoundNode : KitEditorNode { public override Type RuntimeType => typeof(PlaySound); }
    [Serializable] public class PushNode : KitEditorNode { public override Type RuntimeType => typeof(Push); }
    [Serializable] public class SpawnNode : KitEditorNode { public override Type RuntimeType => typeof(Spawn); }
    [Serializable] public class SetTextNode : KitEditorNode { public override Type RuntimeType => typeof(SetText); }
    [Serializable] public class NoteNode : KitEditorNode { public override Type RuntimeType => typeof(Note); }
    [Serializable] public class HitstopNode : KitEditorNode { public override Type RuntimeType => typeof(Hitstop); }

    // ---- Spielsysteme -------------------------------------------------------------------------------
    [Serializable] public class InventoryNode : KitEditorNode { public override Type RuntimeType => typeof(Inventory); }
    [Serializable] public class DialogNode : KitEditorNode { public override Type RuntimeType => typeof(Dialog); }
    [Serializable] public class CheckpointNode : KitEditorNode { public override Type RuntimeType => typeof(Checkpoint); }
    [Serializable] public class WalkToNode : KitEditorNode { public override Type RuntimeType => typeof(WalkTo); }
    [Serializable] public class PlayerShakeNode : KitEditorNode { public override Type RuntimeType => typeof(PlayerShake); }
    [Serializable] public class UiButtonNode : KitEditorNode { public override Type RuntimeType => typeof(UiButton); }
    [Serializable] public class MenuPauseNode : KitEditorNode { public override Type RuntimeType => typeof(MenuPause); }
    [Serializable] public class MenuResumeNode : KitEditorNode { public override Type RuntimeType => typeof(MenuResume); }

    // ---- Erweiterung --------------------------------------------------------------------------------
    [Serializable] public class AnimatorValueNode : KitEditorNode { public override Type RuntimeType => typeof(AnimatorValue); }
    [Serializable] public class SwapMaterialNode : KitEditorNode { public override Type RuntimeType => typeof(SwapMaterial); }
}
