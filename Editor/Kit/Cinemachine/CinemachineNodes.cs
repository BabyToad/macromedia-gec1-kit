using System;
using Unity.GraphToolkit.Editor;

namespace Kit.Editor
{
    // Editor faces of the Cinemachine nodes. They live in their own assembly (only built when Cinemachine is
    // installed), so they declare which graph they belong to.
    [Serializable, UseWithGraph(typeof(KitEditorGraph))] public class CameraSwitchNode : KitEditorNode { public override Type RuntimeType => typeof(CameraSwitch); }
    [Serializable, UseWithGraph(typeof(KitEditorGraph))] public class CameraShakeNode : KitEditorNode { public override Type RuntimeType => typeof(CameraShake); }
}
