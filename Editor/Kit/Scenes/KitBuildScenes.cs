using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Keeps the Build Settings scene list in step with the project, so nobody has to hunt for it:
    /// every scene under Assets/ is in the list. New scenes go to the end, moved ones keep their place and their
    /// tick, deleted ones leave. A scene someone removed from the list on purpose (the file still exists) stays out;
    /// switching a scene off (untick) is the usual way to leave it out of the game. Each change is logged.
    /// Runs after scene files change, before Play and before a build.
    /// </summary>
    [InitializeOnLoad]
    public static class KitBuildScenes
    {
        /// <summary>
        /// Folders whose scenes are not added: the kit's examples (Kit Beispiel; they are not part of the student's game)
        /// and the kit's own development and test scenes. Scenes there can still be added in the window Szenen.
        /// </summary>
        public static readonly string[] Ignored = { KitPaths.StudentFolder + "/", "Assets/Kit/", "Assets/KitDev/", "Assets/BlockoutKit/", "Assets/KitTestTemp/" };

        /// <summary>True for scenes the sync never adds (see <see cref="Ignored"/>; also the test runner's own InitTestScene).</summary>
        public static bool IsIgnored(string scenePath) =>
            Ignored.Any(f => scenePath.StartsWith(f)) || SceneRef.NameOf(scenePath).StartsWith("InitTestScene");

        /// <summary>Remembers every scene the kit has put in the list (GUIDs), to tell "removed on purpose" from "new".</summary>
        public static string KnownFile = "ProjectSettings/KitSzenen.txt";

        /// <summary>Off in tests that need the Build Settings left alone.</summary>
        public static bool Enabled = true;

        static bool s_Scheduled;

        static KitBuildScenes()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Sync(); };
            KitSceneChecks.EntrancesOf = KitEntranceIndex.NamesIn;
        }

        internal static void Schedule()
        {
            if (s_Scheduled) return;
            s_Scheduled = true;
            EditorApplication.delayCall += () => { s_Scheduled = false; Sync(); };
        }

        /// <summary>Brings the list up to date. Returns the German log lines of what changed (empty = nothing).</summary>
        public static List<string> Sync()
        {
            var changes = new List<string>();
            if (!Enabled || EditorApplication.isPlaying) return changes;
            var list = EditorBuildSettings.scenes.ToList();
            var known = LoadKnown();
            bool dirty = false;

            // 1. Entries whose file moved: follow the GUID. Entries whose file is gone: remove.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (SceneRef.NameOf(e.path).StartsWith("InitTestScene")) continue;   // the test runner's, it cleans up itself
                var now = e.guid.Empty() ? "" : AssetDatabase.GUIDToAssetPath(e.guid);
                if (!string.IsNullOrEmpty(now) && File.Exists(now))
                {
                    if (now != e.path)
                    {
                        changes.Add($"Szene „{SceneRef.NameOf(e.path)}“ heißt jetzt „{SceneRef.NameOf(now)}“ (Platz {i} bleibt)");
                        list[i] = new EditorBuildSettingsScene(now, e.enabled);
                        dirty = true;
                    }
                }
                else if (!File.Exists(e.path))
                {
                    changes.Add($"Szene „{SceneRef.NameOf(e.path)}“ gibt es nicht mehr: aus der Liste genommen");
                    list.RemoveAt(i);
                    dirty = true;
                }
            }

            // 2. Remember what is in the list now.
            foreach (var e in list) { if (IsIgnored(e.path)) continue; var g = GuidOf(e); if (g != null) known.Add(g); }

            // 3. New scenes: add at the end, in path order (so one batch lands the same way on every machine).
            var inList = new HashSet<string>(list.Select(e => e.path));
            var found = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity") && !inList.Contains(p) && !IsIgnored(p))
                .OrderBy(p => p, StringComparer.Ordinal);
            foreach (var p in found)
            {
                var g = AssetDatabase.AssetPathToGUID(p);
                if (known.Contains(g)) continue;              // removed from the list on purpose earlier
                list.Add(new EditorBuildSettingsScene(p, true));
                known.Add(g);
                changes.Add($"Szene „{SceneRef.NameOf(p)}“ in die Build Settings eingetragen (Platz {list.Count - 1})");
                dirty = true;
            }

            if (dirty) EditorBuildSettings.scenes = list.ToArray();
            SaveKnown(known);
            foreach (var c in changes) Debug.Log(KitBrand.Log + "Build Settings: " + c + ". Reihenfolge: " + KitBrand.ToolsPath + "Szenen.");
            return changes;
        }

        static string GuidOf(EditorBuildSettingsScene e)
        {
            if (!e.guid.Empty()) return e.guid.ToString();
            var g = AssetDatabase.AssetPathToGUID(e.path);
            return string.IsNullOrEmpty(g) ? null : g;
        }

        static HashSet<string> LoadKnown()
        {
            var set = new HashSet<string>();
            if (File.Exists(KnownFile))
                foreach (var line in File.ReadAllLines(KnownFile))
                {
                    var t = line.Trim();
                    if (t.Length == 32 && !t.StartsWith("#")) set.Add(t);
                }
            return set;
        }

        static void SaveKnown(HashSet<string> known)
        {
            var text = "# " + KitBrand.Name + ": Szenen, die schon einmal in den Build Settings standen (nicht von Hand ändern)\n"
                     + string.Join("\n", known.OrderBy(g => g, StringComparer.Ordinal)) + "\n";
            if (!File.Exists(KnownFile) ? known.Count == 0 : File.ReadAllText(KnownFile) == text) return;
            File.WriteAllText(KnownFile, text);
        }

        /// <summary>Forgets that a scene was removed on purpose, so the next sync adds it again.</summary>
        public static void Forget(string scenePath)
        {
            var known = LoadKnown();
            if (known.Remove(AssetDatabase.AssetPathToGUID(scenePath))) SaveKnown(known);
        }

        // ---------------------------------------------------------------------------------- triggers
        sealed class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool Scene(string p) => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
                if (imported.Any(Scene) || deleted.Any(Scene) || moved.Any(Scene))
                {
                    KitEntranceIndex.Forget(imported.Concat(deleted).Concat(moved).Concat(movedFrom).Where(Scene));
                    Schedule();
                }
                if (moved.Any(Scene) && Enabled)    // graphs that point at a moved scene store its new path
                    EditorApplication.delayCall += () => KitSceneGraphs.RefreshPaths();
            }
        }

        /// <summary>Before a build: the list is complete, and every graph's scene paths match the files.</summary>
        sealed class BeforeBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => -100;
            public void OnPreprocessBuild(BuildReport report)
            {
                Sync();
                RefreshGraphPaths();
            }
        }

        /// <summary>Graphs whose stored scene path is stale (the scene was renamed or moved) get the current one.</summary>
        public static int RefreshGraphPaths() => KitSceneGraphs.RefreshPaths();
    }

    /// <summary>
    /// Which Eingänge each scene has. Open scenes are asked directly (exact, including prefab instances);
    /// closed scenes are read from their file, which finds every Eingang that is not inside a prefab instance.
    /// </summary>
    public static class KitEntranceIndex
    {
        static readonly Dictionary<string, (DateTime stamp, List<string> names, bool complete)> s_Cache =
            new Dictionary<string, (DateTime, List<string>, bool)>();
        static string s_ScriptGuid;

        internal static void Forget(IEnumerable<string> paths) { foreach (var p in paths) s_Cache.Remove(p); }

        /// <summary>Names of the Eingänge in a scene; null if they cannot be known for certain.</summary>
        public static IReadOnlyCollection<string> NamesIn(string scenePath)
        {
            var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (open.IsValid() && open.isLoaded)
                return open.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KitEntrance>(true)).Select(e => e.Name).Distinct().ToList();
            var (names, complete) = FromFile(scenePath);
            return complete ? names : null;
        }

        /// <summary>Eingänge found in the scene file, and whether that list is certainly complete.</summary>
        public static (List<string> names, bool complete) FromFile(string scenePath)
        {
            if (!File.Exists(scenePath)) return (new List<string>(), false);
            var stamp = File.GetLastWriteTimeUtc(scenePath);
            if (s_Cache.TryGetValue(scenePath, out var c) && c.stamp == stamp) return (c.names, c.complete);
            var guid = ScriptGuid();
            var text = File.ReadAllText(scenePath);
            var result = Parse(text, guid);
            s_Cache[scenePath] = (stamp, result.names, result.complete);
            return result;
        }

        static string ScriptGuid()
        {
            if (!string.IsNullOrEmpty(s_ScriptGuid)) return s_ScriptGuid;
            foreach (var g in AssetDatabase.FindAssets(nameof(KitEntrance) + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                if (script && script.GetClass() == typeof(KitEntrance)) return s_ScriptGuid = g;
            }
            return "";
        }

        static readonly Regex s_Doc = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Multiline);

        /// <summary>Reads Eingang names from scene YAML: each KitEntrance's GameObject's m_Name.</summary>
        internal static (List<string> names, bool complete) Parse(string yaml, string scriptGuid)
        {
            var docs = new Dictionary<string, (int cls, string body)>();
            var ms = s_Doc.Matches(yaml);
            for (int i = 0; i < ms.Count; i++)
            {
                int start = ms[i].Index, end = i + 1 < ms.Count ? ms[i + 1].Index : yaml.Length;
                docs[ms[i].Groups[2].Value] = (int.Parse(ms[i].Groups[1].Value), yaml.Substring(start, end - start));
            }
            var names = new List<string>();
            bool complete = true;
            foreach (var d in docs.Values)
            {
                if (d.cls == 1001) complete = false;                       // a prefab instance may hold Eingänge we cannot see
                if (d.cls != 114 || string.IsNullOrEmpty(scriptGuid) || !d.body.Contains("guid: " + scriptGuid)) continue;
                var go = Regex.Match(d.body, @"m_GameObject: \{fileID: (-?\d+)\}");
                if (!go.Success || !docs.TryGetValue(go.Groups[1].Value, out var g)) { complete = false; continue; }
                var n = Regex.Match(g.body, @"^\s*m_Name: (.*)$", RegexOptions.Multiline);
                if (!n.Success) { complete = false; continue; }
                var name = Unquote(n.Groups[1].Value.Trim());
                if (!names.Contains(name)) names.Add(name);
            }
            return (names, complete);
        }

        // Unity writes plain names as-is and quotes the others ("…", with \x / \u escapes for non-ASCII).
        static string Unquote(string v)
        {
            if (v.Length < 2 || v[0] != '"' || v[v.Length - 1] != '"') return v;
            v = v.Substring(1, v.Length - 2);
            return Regex.Replace(v, @"\\(x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|.)", m =>
            {
                var s = m.Groups[1].Value;
                if (s[0] == 'x' || s[0] == 'u') return ((char)Convert.ToInt32(s.Substring(1), 16)).ToString();
                return s == "n" ? "\n" : s == "t" ? "\t" : s;
            });
        }
    }
}
