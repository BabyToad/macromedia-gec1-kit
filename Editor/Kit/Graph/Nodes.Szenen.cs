using System;
using Unity.GraphToolkit.Editor;

namespace Kit.Editor
{
    // The editor faces of the scene nodes.
    [Serializable] public class RestartSceneNode : KitEditorNode { public override Type RuntimeType => typeof(RestartScene); }
    [Serializable] public class NextSceneNode : KitEditorNode { public override Type RuntimeType => typeof(NextScene); }
    [Serializable] public class TimeStopNode : KitEditorNode { public override Type RuntimeType => typeof(TimeStop); }
    [Serializable] public class TimeResumeNode : KitEditorNode { public override Type RuntimeType => typeof(TimeResume); }
    [Serializable] public class ArrivedNode : KitEditorNode { public override Type RuntimeType => typeof(Arrived); }
    [Serializable] public class QuitGameNode : KitEditorNode { public override Type RuntimeType => typeof(QuitGame); }

    /// <summary>
    /// Szene laden. Its v0.3 setting "scene" (a typed name) stays defined, so old graphs keep their value, but only in
    /// the Inspector: the node itself shows the scene file, the Eingang and the reset switch.
    /// </summary>
    [Serializable]
    public class LoadSceneNode : KitEditorNode
    {
        public override Type RuntimeType => typeof(LoadScene);

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            var defaults = Activator.CreateInstance(RuntimeType);
            foreach (var s in Schema.Settings)
            {
                var option = context.AddOption(s.Id, s.Type).WithDisplayName(s.Shown).WithDefaultValue(s.Get(defaults)).Delayed();
                if (s.Tooltip != null) option = option.WithTooltip(s.Tooltip);
                if (s.Id == nameof(LoadScene.scene)) option = option.ShowInInspectorOnly();
                option.Build();
            }
        }
    }
}
