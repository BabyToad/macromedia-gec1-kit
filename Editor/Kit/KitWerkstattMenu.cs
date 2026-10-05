using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Tools › Kit › Beispiel „Werkstatt (kaputt)“ bauen (Day 3): copies the shipped, deliberately broken Werkstatt
    /// into Assets/Kit Beispiel/Werkstatt. The source is the package's Samples~/Werkstatt (studio project:
    /// Assets/Kit/Werkstatt). Every copied asset gets a new GUID (references inside the copy follow), so a second
    /// copy never clashes with the first. Windrad.cs is copied only if the project has no Windrad script yet:
    /// two classes of that name would be a compile error. Never overwrites: an existing folder gets a new copy.
    /// </summary>
    public static class KitWerkstattMenu
    {
        public const string Target = KitPaths.StudentFolder + "/Werkstatt";
        const string StudioSource = "Assets/Kit/Werkstatt";
        static readonly Regex k_Guid = new Regex(@"guid: ([0-9a-f]{32})");
        static readonly HashSet<string> k_Text = new HashSet<string> { ".unity", ".mat", ".kit", ".prefab", ".asset", ".meta" };

        /// <summary>Full path of the shipped Werkstatt, or null.</summary>
        public static string Source
        {
            get
            {
                var pkg = KitPaths.Package;
                if (pkg != null)
                {
                    var p = Path.Combine(pkg.resolvedPath, "Samples~", "Werkstatt");
                    return Directory.Exists(p) ? p : null;
                }
                return Directory.Exists(StudioSource) ? Path.GetFullPath(StudioSource) : null;
            }
        }

        [MenuItem("Tools/Kit/Beispiel „Werkstatt (kaputt)“ bauen")]
        static void Menu()
        {
            if (!KitPaths.OpenScenesSafe()) return;
            string target = Target;
            if (Directory.Exists(target))
            {
                if (!EditorUtility.DisplayDialog("Schon vorhanden",
                        $"Die Werkstatt gibt es schon:\n{target}\n\nEine neue Kopie daneben anlegen?", "Neue Kopie anlegen", "Abbrechen"))
                    return;
            }
            var scene = Build(target);
            if (scene != null) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
        }

        /// <summary>Copies the Werkstatt to <paramref name="target"/> (a free folder name if it exists). Returns the scene path.</summary>
        public static string Build(string target)
        {
            var src = Source;
            if (src == null) { Debug.LogError("[Kit] Werkstatt: die Vorlage fehlt im Kurs-Paket (Samples~/Werkstatt)"); return null; }
            if (!KitPaths.IsWritable(target)) throw new ArgumentException($"„{target}“ liegt nicht unter Assets");
            KitPaths.EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));
            if (AssetDatabase.IsValidFolder(target) || Directory.Exists(target)) target = AssetDatabase.GenerateUniqueAssetPath(target);

            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).ToList();
            var guids = new Dictionary<string, string>();          // old → new
            var skip = new HashSet<string>();
            foreach (var f in files)
            {
                var meta = f + ".meta";
                if (!File.Exists(meta)) continue;
                var m = k_Guid.Match(File.ReadAllText(meta));
                if (!m.Success) continue;
                if (Path.GetFileName(f) == "Windrad.cs" && ExistingWindrad() is string have)
                {
                    guids[m.Groups[1].Value] = have;                // the project's Windrad: reuse it, no second class
                    skip.Add(f);
                }
                else guids[m.Groups[1].Value] = Guid.NewGuid().ToString("N");
            }

            foreach (var f in files.Where(f => !skip.Contains(f)))
            {
                var rel = f.Substring(src.Length).TrimStart('\\', '/');
                var dst = Path.Combine(target, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                CopyRemapped(f, dst, guids);
                if (File.Exists(f + ".meta")) CopyRemapped(f + ".meta", dst + ".meta", guids);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = target + "/Werkstatt.unity";
            Debug.Log($"[Kit] Werkstatt (kaputt) angelegt: {scene}");
            return File.Exists(scene) ? scene : null;
        }

        static void CopyRemapped(string from, string to, Dictionary<string, string> guids)
        {
            if (!k_Text.Contains(Path.GetExtension(from).ToLowerInvariant())) { File.Copy(from, to); return; }
            var text = File.ReadAllText(from);
            text = k_Guid.Replace(text, m => guids.TryGetValue(m.Groups[1].Value, out var n) ? "guid: " + n : m.Value);
            File.WriteAllText(to, text);
        }

        /// <summary>GUID of a Windrad script already in the project (Assets), or null.</summary>
        static string ExistingWindrad()
        {
            foreach (var g in AssetDatabase.FindAssets("Windrad t:MonoScript", new[] { "Assets" }))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                if (script && Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == "Windrad") return g;
            }
            return null;
        }
    }
}
