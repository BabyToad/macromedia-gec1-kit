using System;
using UnityEngine.SceneManagement;

namespace Kit
{
    // Szene laden: wechselt die Szene. "Wird geladen" feuert VORHER – alles, was daran hängt,
    // läuft noch in der alten Szene und verschwindet mit ihr. Die Szene muss in den Build Settings stehen.
    [Serializable]
    [NodeInfo("Szene laden", "Aktion", "Zu einer anderen Szene wechseln")]
    public class LoadScene : KitNode
    {
        [Setting("Szene (Name)")] public string scene = "";

        [Output("Wird geladen")] [NonSerialized] public Output loading;

        [Input("Laden")]
        public void Load(Signal s)
        {
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(scene))
            {
                Fail($"Szene „{scene}“ steht nicht in den Build Settings");
                return;
            }
            Done($"lädt „{scene}“");
            loading.Fire(s);
            SceneManager.LoadScene(scene);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (string.IsNullOrEmpty(scene)) problems.Add("keine Szene eingetragen");
#if UNITY_EDITOR
            else if (Array.FindIndex(UnityEditor.EditorBuildSettings.scenes, x => x.enabled && System.IO.Path.GetFileNameWithoutExtension(x.path) == scene) < 0)
                problems.Add($"Szene „{scene}“ steht nicht in den Build Settings");
#endif
        }
    }
}
