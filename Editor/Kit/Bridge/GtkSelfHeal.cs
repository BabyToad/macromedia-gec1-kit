using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Kit.Editor
{
    /// <summary>
    /// Repairs Graph Toolkit on editors newer than the course pin. Unity 6000.3.16 added a second
    /// CallbackEventHandler.RegisterCallback&lt;TEvent,TArgs&gt; overload; Graph Toolkit 0.4.0-exp.2 (frozen, no newer
    /// release) looks it up with SingleOrDefault in ConstantField.GetRegisterCallback, so every node with settings fails
    /// to draw. On such an editor the kit offers once per editor session to copy Graph Toolkit into the project as an
    /// embedded package (Packages/besteck-graphtoolkit, which wins over the registry copy) and to patch that one
    /// lookup to pick the overload by its parameters. To undo: delete that folder; Unity restores the registry copy.
    /// </summary>
    [InitializeOnLoad]
    public static class GtkSelfHeal
    {
        public const string Package = "com.unity.graphtoolkit";
        // Not "Packages/com.unity.graphtoolkit": inside the editor any path ending in Packages/<package name> is redirected to
        // the package cache, so the embedded copy gets its own folder name. Unity finds embedded packages by package.json.
        public const string Folder = "Packages/besteck-graphtoolkit";
        public const string MarkerFile = "BESTECK-PATCH.md";
        const string PatchedFile = "GraphToolkitEditor/UI/ModelView/Inspector/UIElements/ConstantField.cs";
        const string AskedKey = "Besteck.GtkSelfHeal.Asked";

        /// <summary>The exact line in Graph Toolkit 0.4.0-exp.2 (whitespace-insensitive match).</summary>
        public const string Original =
            ".SingleOrDefault(m => m.Name == nameof(RegisterCallback) && m.GetGenericArguments().Length == 2);";

        /// <summary>Picks RegisterCallback&lt;TEvent,TArgs&gt;(EventCallback&lt;TEvent,TArgs&gt;, TArgs, TrickleDown): the overload GTK invokes.</summary>
        public const string Replacement =
            ".Where(m => m.Name == nameof(RegisterCallback) && m.GetGenericArguments().Length == 2)\n" +
            "                .OrderByDescending(m => { var p = m.GetParameters(); return p.Length == 3 && p[0].ParameterType.IsGenericType\n" +
            "                    && p[0].ParameterType.GetGenericTypeDefinition() == typeof(EventCallback<,>)\n" +
            "                    && p[2].ParameterType == typeof(TrickleDown) ? 1 : 0; })\n" +
            "                .FirstOrDefault(); // Besteck patch: Unity 6000.3.16+ has two such overloads";

        static GtkSelfHeal()
        {
            // The package check waits for delayCall (after packages are registered); a new registration clears the cache.
            Events.registeredPackages += _ => s_CheckedAt = -1;
            if (!KitGtkBridge.GtkFieldsBroken || Application.isBatchMode) return;
            EditorApplication.delayCall += Offer;
        }

        // Inside the editor a relative "Packages/<name>" path is virtual and resolves to wherever the package lives (the
        // package cache for a registry package), so the real folder is built from the project root.
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string RealFolder => Path.Combine(ProjectRoot, "Packages", "besteck-graphtoolkit");

        static bool s_Healed;
        static double s_CheckedAt = -1;

        /// <summary>True when the ACTIVE Graph Toolkit is the patched embedded copy: the marker alone is not enough
        /// (a failed resolve, or a checkout that restored the original file, would otherwise report "repaired").
        /// Cached per domain reload; the graph window asks often.</summary>
        public static bool Healed
        {
            get
            {
                // Re-checked at most every 2 s and whenever packages change; a deleted folder is noticed at once.
                double now = EditorApplication.timeSinceStartup;
                if (s_CheckedAt < 0 || now - s_CheckedAt > 2 || (s_Healed && !File.Exists(Path.Combine(RealFolder, MarkerFile))))
                { s_Healed = CheckHealed(); s_CheckedAt = now; }
                return s_Healed;
            }
        }

        static bool CheckHealed()
        {
            if (!File.Exists(Path.Combine(RealFolder, MarkerFile))) return false;
            var info = PackageInfo.FindForPackageName(Package);
            if (info == null || info.source != PackageSource.Embedded || !SamePath(info.resolvedPath, RealFolder)) return false;
            var file = Path.Combine(RealFolder, PatchedFile);
            return File.Exists(file) && IsPatched(File.ReadAllText(file));
        }

        static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the graph editor still needs the repair (a newer editor without the patch).</summary>
        public static bool Needed => KitGtkBridge.GtkFieldsBroken && !Healed;

        static void Offer()
        {
            if (!Needed || SessionState.GetBool(AskedKey, false)) return;
            SessionState.SetBool(AskedKey, true);
            bool yes = EditorUtility.DisplayDialog(
                KitBrand.Name + ": Graph-Werkzeug reparieren",
                "Diese Unity-Version (" + Application.unityVersion + ") ist neuer als die Kursversion 6000.3.15f1. " +
                "Das Graph-Werkzeug zeichnet hier keine Knoten mit Einstellungen.\n\n" +
                "Besteck kann es reparieren: Eine Kopie des Graph-Werkzeugs kommt mit einer kleinen Korrektur in den Ordner " +
                "Packages/besteck-graphtoolkit dieses Projekts (etwa 10 MB). Danach die Änderung committen.\n\n" +
                "Rückgängig: den Ordner löschen.",
                "Reparieren", "Später");
            if (yes) Heal();
            else Debug.LogWarning(KitBrand.Log + "Graph-Werkzeug nicht repariert. Nachholen: " + KitBrand.ToolsPath + "Graph-Werkzeug reparieren (neuere Unity-Version).");
        }

        [MenuItem(KitBrand.ToolsMenu + "Graph-Werkzeug reparieren (neuere Unity-Version)", false, 210)]
        static void HealMenu()
        {
            if (!KitGtkBridge.GtkFieldsBroken) { EditorUtility.DisplayDialog(KitBrand.Name, "Diese Unity-Version braucht keine Reparatur.", "OK"); return; }
            if (Healed) { EditorUtility.DisplayDialog(KitBrand.Name, "Das Graph-Werkzeug ist in diesem Projekt schon repariert.", "OK"); return; }
            Heal();
        }

        /// <summary>Copies Graph Toolkit into Packages/ and patches the lookup. Returns false (with a German console line) if it cannot.</summary>
        public static bool Heal()
        {
            if (Healed) return true;
            var info = PackageInfo.FindForPackageName(Package);
            if (info == null || string.IsNullOrEmpty(info.resolvedPath) || !Directory.Exists(info.resolvedPath))
            { Debug.LogError(KitBrand.Log + "Graph-Werkzeug nicht gefunden: Reparatur nicht möglich."); return false; }
            if (info.version != "0.4.0-exp.2")
            { Debug.LogWarning(KitBrand.Log + $"Graph-Werkzeug {info.version} ist nicht die Version, die Besteck reparieren kann (0.4.0-exp.2)."); return false; }

            // A Graph Toolkit the student embedded themselves is theirs: never touched, only pointed at.
            if (info.source == PackageSource.Embedded)
            {
                Debug.LogWarning(KitBrand.Log + "Das Graph-Werkzeug liegt schon als eigene Kopie im Projekt (" + info.resolvedPath +
                                 "). Besteck ändert sie nicht. Für den Kurs Unity 6000.3.15f1 verwenden, oder die Kopie entfernen und dann reparieren.");
                return false;
            }
            string dest = RealFolder;
            // Never overwrite: an existing folder of that name is left exactly as it is.
            if (Directory.Exists(dest) || File.Exists(dest))
            {
                Debug.LogWarning(KitBrand.Log + "Der Ordner Packages/besteck-graphtoolkit gibt es schon, Besteck lässt ihn unverändert. " +
                                 "Ist er von einer früheren Reparatur, Unity neu starten; sonst umbenennen und neu reparieren.");
                return false;
            }
            const bool copied = true;
            // Build the copy outside Packages/ and move it in at the end: as soon as a package.json appears under
            // Packages/, Unity switches to it and deletes the registry copy that is still being read.
            string work = Path.Combine(ProjectRoot, "Temp", "BesteckGraphToolkit");
            try
            {
                Copy(info.resolvedPath, work);
                string file = Path.Combine(work, PatchedFile);
                string text = File.ReadAllText(file);
                string patched = Patch(text);
                if (patched == null)
                {
                    if (copied) Directory.Delete(work, true);
                    Debug.LogError(KitBrand.Log + "Graph-Werkzeug: die Stelle für die Korrektur wurde nicht gefunden. Bitte Unity 6000.3.15f1 verwenden.");
                    return false;
                }
                File.WriteAllText(file, patched);
                File.WriteAllText(Path.Combine(work, MarkerFile),
                    "# Besteck patch\n\nGraph Toolkit 0.4.0-exp.2 with one change in `" + PatchedFile + "`: GetRegisterCallback picks the\n" +
                    "RegisterCallback overload by its parameters, because Unity 6000.3.16 and newer have two of them.\n" +
                    "Made by Besteck on Unity " + Application.unityVersion + ". To undo, delete this folder.\n");
                Directory.Move(work, dest);   // one rename on the same drive: Unity only ever sees a complete package
                s_CheckedAt = -1;
            }
            catch (Exception e)
            {
                if (copied && Directory.Exists(work)) { try { Directory.Delete(work, true); } catch { } }
                Debug.LogError(KitBrand.Log + "Graph-Werkzeug reparieren ging nicht: " + e.Message);
                return false;
            }
            Client.Resolve();
            AssetDatabase.Refresh();
            Debug.Log(KitBrand.Log + "Graph-Werkzeug repariert (Packages/besteck-graphtoolkit). Unity lädt es neu; danach die Änderung committen.");
            return true;
        }

        /// <summary>The patched text, or null when the original line is not there (whitespace-insensitive).</summary>
        public static string Patch(string source)
        {
            if (IsPatched(source)) return source;
            string squeezed = Squeeze(Original);
            // Find the original call ignoring whitespace and line breaks inside it.
            int start = source.IndexOf(".SingleOrDefault(m => m.Name == nameof(RegisterCallback)", StringComparison.Ordinal);
            if (start < 0) return null;
            int end = source.IndexOf(';', start);
            if (end < 0 || Squeeze(source.Substring(start, end - start + 1)) != squeezed) return null;
            return source.Substring(0, start) + Replacement + source.Substring(end + 1);
        }

        /// <summary>The replacement is there and the original lookup is gone (a comment alone does not count).</summary>
        public static bool IsPatched(string source) =>
            source.Contains("Besteck patch: Unity 6000.3.16+") && source.Contains(".OrderByDescending(m => { var p = m.GetParameters();")
            && !source.Contains(".SingleOrDefault(m => m.Name == nameof(RegisterCallback)");

        static string Squeeze(string s) => string.Concat(s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

        static void Copy(string from, string to)
        {
            if (Directory.Exists(to)) Directory.Delete(to, true);
            foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(from, dir);
                if (Skip(rel)) continue;
                Directory.CreateDirectory(Path.Combine(to, rel));
            }
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(from, f);
                if (Skip(rel) || Path.GetFileName(f) == ".signature") continue;   // a modified copy carries no registry signature
                File.Copy(f, Path.Combine(to, rel), true);
                File.SetAttributes(Path.Combine(to, rel), FileAttributes.Normal);   // the package cache is read-only
            }
        }

        // Documentation and the package's own tests are not needed to run it (and roughly halve the size).
        static bool Skip(string rel)
        {
            var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            return first == "Documentation~" || first == "Tests";   // Samples~ stays: package.json lists them
        }
    }
}
