using System;

namespace Kit.Editor
{
    // ---- Werte: the editor face of each runtime node (one line each, as in KitEditorNodes.cs) ----------
    [Serializable] public class NumberVariableNode : KitEditorNode { public override Type RuntimeType => typeof(NumberVariable); }
    [Serializable] public class FlagVariableNode : KitEditorNode { public override Type RuntimeType => typeof(FlagVariable); }
    [Serializable] public class TextVariableNode : KitEditorNode { public override Type RuntimeType => typeof(TextVariable); }
    [Serializable] public class CalculateNode : KitEditorNode { public override Type RuntimeType => typeof(Calculate); }
    [Serializable] public class RandomNumberNode : KitEditorNode { public override Type RuntimeType => typeof(RandomNumber); }
    [Serializable] public class ShowValueNode : KitEditorNode { public override Type RuntimeType => typeof(ShowValue); }
    [Serializable] public class BrieflyActivateNode : KitEditorNode { public override Type RuntimeType => typeof(BrieflyActivate); }
    [Serializable] public class SpawnWithVelocityNode : KitEditorNode { public override Type RuntimeType => typeof(SpawnWithVelocity); }
}
