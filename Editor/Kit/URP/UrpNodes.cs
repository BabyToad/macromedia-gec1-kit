using System;
using Unity.GraphToolkit.Editor;

namespace Kit.Editor
{
    // Editor face of the URP node. Own assembly (only built with URP), so it names its graph.
    [Serializable, UseWithGraph(typeof(KitEditorGraph))] public class MoodChangeNode : KitEditorNode { public override Type RuntimeType => typeof(MoodChange); }
}
