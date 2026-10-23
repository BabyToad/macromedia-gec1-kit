using System;
using UnityEngine.Scripting.APIUpdating;

namespace Kit.Editor
{
    // v0.3.2 state nodes, kept only so old .kit files still load and „Projekt auf den neuen Stand bringen“ can turn
    // them into Variable nodes (WerteMigration). They live in their own assembly: Graph Toolkit lists only the nodes
    // of the graph's assembly in the palette, so nobody can add them any more. [MovedFrom] lets the graph files,
    // which name Kit.Editor as their assembly, find them here. Remove in v0.5.
    [Serializable, MovedFrom(true, "Kit.Editor", "Kit.Editor")] public class FlagNode : KitEditorNode { public override Type RuntimeType => typeof(Flag); }
    [Serializable, MovedFrom(true, "Kit.Editor", "Kit.Editor")] public class CounterNode : KitEditorNode { public override Type RuntimeType => typeof(Counter); }
    [Serializable, MovedFrom(true, "Kit.Editor", "Kit.Editor")] public class HealthNode : KitEditorNode { public override Type RuntimeType => typeof(Health); }
    [Serializable, MovedFrom(true, "Kit.Editor", "Kit.Editor")] public class HudDisplayNode : KitEditorNode { public override Type RuntimeType => typeof(HudDisplay); }
    [Serializable, MovedFrom(true, "Kit.Editor", "Kit.Editor")] public class SharedFlagNode : KitEditorNode { public override Type RuntimeType => typeof(SharedFlag); }
}
