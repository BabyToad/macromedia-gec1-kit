#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kit
{
    /// <summary>
    /// Editor checks for nodes that point at another scene: does the file exist, is it in the build, does it
    /// have the Eingang the node names. The entrance index lives in the editor assembly and plugs in here.
    /// </summary>
    public static class KitSceneChecks
    {
        /// <summary>Set by the editor: the Eingang names in a scene file, or null if unknown.</summary>
        public static Func<string, IReadOnlyCollection<string>> EntrancesOf;

        public static IEnumerable<string> Problems(string scenePath, string entrance)
        {
            var name = SceneRef.NameOf(scenePath);
            if (!System.IO.File.Exists(scenePath)) { yield return $"Szene „{name}“ gibt es nicht (mehr) als Datei"; yield break; }
            var inList = Array.Find(UnityEditor.EditorBuildSettings.scenes, s => s.path == scenePath);
            if (inList == null)
                yield return $"Szene „{name}“ steht nicht in den Build Settings (" + KitBrand.ToolsPath + "Szenen: eintragen)";
            else if (!inList.enabled)
                yield return $"Szene „{name}“ ist in den Build Settings ausgeschaltet (" + KitBrand.ToolsPath + "Szenen: Haken setzen)";
            if (string.IsNullOrEmpty(entrance)) yield break;
            var names = EntrancesOf?.Invoke(scenePath);
            if (names == null || names.Contains(entrance)) yield break;
            yield return names.Count == 0
                ? $"Szene „{name}“ hat noch keinen Eingang: dort GameObject › {KitBrand.Name} › Eingang anlegen und „{entrance}“ nennen"
                : $"Szene „{name}“ hat keinen Eingang „{entrance}“ (vorhanden: {string.Join(", ", names.Select(n => $"„{n}“"))})";
        }
    }
}
#endif
