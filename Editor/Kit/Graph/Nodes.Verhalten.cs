using System;

namespace Kit.Editor
{
    // The graph faces of the Verhalten nodes, one line each (see KitEditorNode).
    [Serializable] public class NpcEventsNode : KitEditorNode { public override Type RuntimeType => typeof(NpcEvents); }
    [Serializable] public class SwitchBehaviourNode : KitEditorNode { public override Type RuntimeType => typeof(SwitchBehaviour); }
    [Serializable] public class MakeNoiseNode : KitEditorNode { public override Type RuntimeType => typeof(MakeNoise); }
}
