using System;

namespace Kit.Editor
{
    // Editor faces of the Atmosphäre nodes (one line each, like KitEditorNodes.cs).
    [Serializable] public class FadeNode : KitEditorNode { public override Type RuntimeType => typeof(Fade); }
    [Serializable] public class SlowMotionNode : KitEditorNode { public override Type RuntimeType => typeof(SlowMotion); }
    [Serializable] public class LightChangeNode : KitEditorNode { public override Type RuntimeType => typeof(LightChange); }
    [Serializable] public class FogChangeNode : KitEditorNode { public override Type RuntimeType => typeof(FogChange); }
    [Serializable] public class ParticlesNode : KitEditorNode { public override Type RuntimeType => typeof(Particles); }
    [Serializable] public class ColorChangeNode : KitEditorNode { public override Type RuntimeType => typeof(ColorChange); }
    [Serializable] public class MusicChangeNode : KitEditorNode { public override Type RuntimeType => typeof(MusicChange); }
    [Serializable] public class TimeOfDayNode : KitEditorNode { public override Type RuntimeType => typeof(TimeOfDay); }
}
