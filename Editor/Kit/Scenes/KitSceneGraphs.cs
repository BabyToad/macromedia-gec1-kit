using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Scene references inside .kit graphs, kept right on the editor's main thread: the stored path follows a renamed
    /// or moved scene (before a build and after a scene moves), and v0.3's typed scene names become scene files.
    /// </summary>
    public static class KitSceneGraphs
    {
        /// <summary>Every .kit graph under the folder.</summary>
        static IEnumerable<string> Graphs(string folder) =>
            AssetDatabase.FindAssets("t:" + nameof(LogicGraph), new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("." + KitEditorGraph.Extension)).Distinct();

        /// <summary>
        /// Writes the current path of every scene file into the graphs that refer to it, where it differs. Returns how
        /// many graphs were saved. Runs before a build and after scenes move.
        /// </summary>
        public static int RefreshPaths(string folder = "Assets")
        {
            int saved = 0;
            foreach (var path in Graphs(folder))
            {
                var g = GraphDatabase.LoadGraph<KitEditorGraph>(path);
                if (g == null) continue;
                bool changed = false;
                foreach (var n in g.GetNodes().OfType<KitEditorNode>())
                    foreach (var s in n.Schema.Settings)
                    {
                        if (s.Type != typeof(SceneRef) || !(n.GetSetting(s) is SceneRef r) || !r.Asset) continue;
                        var now = AssetDatabase.GetAssetPath(r.Asset);
                        if (string.IsNullOrEmpty(now) || now == r.StoredPath) continue;
                        KitGraphEditing.SetOption(n, s.Id, SceneRef.To((SceneAsset)r.Asset));
                        changed = true;
                    }
                if (!changed) continue;
                KitGraphEditing.Save(g);
                saved++;
            }
            return saved;
        }

        /// <summary>
        /// v0.3 → v0.4: "Szene laden" with a typed scene name gets the scene file of that name (if exactly one exists);
        /// the name is then cleared. Names without a matching file stay, so nothing is lost.
        /// </summary>
        [KitMigrationStep(30, "Szene laden: getippte Szenennamen werden zu Szene-Dateien")]
        public static void MigrateNames(string folder)
        {
            foreach (var path in Graphs(folder))
            {
                var g = GraphDatabase.LoadGraph<KitEditorGraph>(path);
                if (g == null) continue;
                bool changed = false;
                foreach (var n in g.GetNodes().OfType<LoadSceneNode>())
                {
                    var nameSetting = n.Schema.Settings.Find(s => s.Id == nameof(LoadScene.scene));
                    var targetSetting = n.Schema.Settings.Find(s => s.Id == nameof(LoadScene.target));
                    var name = n.GetSetting(nameSetting) as string;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (n.GetSetting(targetSetting) is SceneRef existing && existing.Asset) continue;
                    var matches = AssetDatabase.FindAssets("t:Scene " + name, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Where(p => SceneRef.NameOf(p) == name).ToList();
                    if (matches.Count != 1)
                    {
                        Debug.LogWarning(KitBrand.Log + $"{path}: „Szene laden“ nennt „{name}“, " +
                            (matches.Count == 0 ? "aber keine Szene heißt so" : $"aber {matches.Count} Szenen heißen so") + ": bitte die Szene-Datei hineinziehen.");
                        continue;
                    }
                    KitGraphEditing.SetOption(n, targetSetting.Id, SceneRef.To(AssetDatabase.LoadAssetAtPath<SceneAsset>(matches[0])));
                    KitGraphEditing.SetOption(n, nameSetting.Id, "");
                    changed = true;
                }
                if (changed) KitGraphEditing.Save(g);
            }
        }
    }
}
