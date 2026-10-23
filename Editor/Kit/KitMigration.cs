using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Marks a migration step that brings a student project from an older kit version to this one. Two shapes:
    /// <code>
    /// [KitMigrationStep(10, "Merker werden zu Variablen")]                 static void Graphs(string folder) { … }   // once; folder = "Assets" (a test passes its own); () works too
    /// [KitMigrationStep(20, "Rollen anlegen", PerContainer = true)]        static void Roles(GameObject[] roots) { … }   // every scene and prefab
    /// </code>
    /// Steps run in <see cref="Order"/>; Kern's own steps use 0–9 and 100+. Each step must be idempotent: it may
    /// run again on an already migrated project (Tools › Brand › Projekt auf den neuen Stand bringen).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class KitMigrationStepAttribute : Attribute
    {
        public readonly int Order;
        public readonly string What;
        /// <summary>True: called once for every scene and prefab under Assets, with its root objects; the runner saves them.</summary>
        public bool PerContainer;
        public KitMigrationStepAttribute(int order, string what) { Order = order; What = what; }
    }

    /// <summary>What the project was last migrated to (ProjectSettings/KitMigration.asset, part of the student's repository).</summary>
    [FilePath("ProjectSettings/KitMigration.asset", FilePathAttribute.Location.ProjectFolder)]
    public class KitMigrationState : ScriptableSingleton<KitMigrationState>
    {
        public string version = "";
        public void Set(string v) { version = v; Save(true); }
    }

    /// <summary>
    /// Runs the migration steps. After an update the editor asks once (never in batch mode); the menu runs it any time.
    /// Open scenes are offered for saving first; every scene and prefab under Assets is opened, changed and saved.
    /// </summary>
    [InitializeOnLoad]
    public static class KitMigration
    {
        /// <summary>The kit version this code migrates to. Bump with every release that registers new steps.</summary>
        public const string Current = "0.5";   // v0.5.0 is the first release with these steps (no 0.4 shipped)

        static KitMigration() { EditorApplication.delayCall += OfferIfNeeded; }

        /// <summary>
        /// Every step of the kit, in order. Steps in test assemblies are left out (pass <paramref name="includeTests"/> in
        /// tests), so running the menu in the studio project never runs a test fixture's step.
        /// </summary>
        public static IEnumerable<(KitMigrationStepAttribute step, MethodInfo method)> Steps(bool includeTests = false) =>
            TypeCache.GetMethodsWithAttribute<KitMigrationStepAttribute>()
                .Where(m => includeTests || !IsTestAssembly(m.DeclaringType?.Assembly))
                .Select(m => (step: m.GetCustomAttribute<KitMigrationStepAttribute>(), method: m))
                .Where(x => x.method.IsStatic)
                .OrderBy(x => x.step.Order).ThenBy(x => x.method.DeclaringType?.FullName).ThenBy(x => x.method.Name);

        static bool IsTestAssembly(Assembly a) =>
            a != null && (a.GetName().Name.Contains(".Tests") || a.GetReferencedAssemblies().Any(r => r.Name == "nunit.framework"));

        /// <summary>Why a step's method cannot be called, or null. Checked for all steps before anything is changed.</summary>
        public static string SignatureProblem(KitMigrationStepAttribute step, MethodInfo m)
        {
            var ps = m.GetParameters();
            if (m.ReturnType != typeof(void)) return "muss void zurückgeben";
            if (step.PerContainer) return ps.Length == 1 && ps[0].ParameterType == typeof(GameObject[]) ? null : "braucht (GameObject[] roots)";
            return ps.Length == 0 || (ps.Length == 1 && ps[0].ParameterType == typeof(string)) ? null : "braucht () oder (string folder)";
        }

        static void OfferIfNeeded()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!KitPaths.FromPackage) return;                         // the studio project migrates by hand
            var state = KitMigrationState.instance;
            if (state.version == Current) return;
            if (!HasKitContent()) { state.Set(Current); return; }     // nothing built with the kit yet: nothing to change
            bool go = EditorUtility.DisplayDialog($"{KitBrand.Name} aktualisiert",
                $"Das Kurs-Paket ist jetzt auf Stand {Current}. Szenen, Prefabs und Logik-Graphen dieses Projekts sollten einmal angepasst werden " +
                "(Interaktion heißt jetzt Logik, gebundene Objekte bekommen ihre Rolle).\n\nVorher am besten committen. Jetzt anpassen?",
                "Jetzt anpassen", "Später");
            if (go) RunAll();
            else Debug.Log(KitBrand.Log + $"Projekt noch nicht angepasst: später über {KitBrand.ToolsPath}Projekt auf den neuen Stand bringen.");
        }

        /// <summary>Anything built with the kit: a graph file, or a scene or prefab with a Logik (found by the script's GUID in the file).</summary>
        static bool HasKitContent() =>
            AssetDatabase.FindAssets("t:LogicGraph", new[] { "Assets" }).Length > 0
            || AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                   .Any(p => System.IO.File.Exists(p) && System.IO.File.ReadAllText(p).Contains(LogicScriptGuid));

        /// <summary>Script GUID of the Logik component (unchanged since v0.3, when it was called Interaction).</summary>
        public const string LogicScriptGuid = "df960fb410764cb40a687190c48f9c90";

        [MenuItem(KitBrand.ToolsMenu + "Projekt auf den neuen Stand bringen", false, 200)]
        static void Menu()
        {
            if (!EditorUtility.DisplayDialog("Projekt anpassen",
                    "Alle Szenen, Prefabs und Logik-Graphen unter Assets werden auf den Stand des Kurs-Pakets gebracht und gespeichert. Vorher am besten committen.",
                    "Anpassen", "Abbrechen")) return;
            RunAll();
        }

        /// <summary>
        /// Runs the steps in their global order: consecutive per-container steps share one pass over every scene and
        /// prefab under <paramref name="folder"/>. A container whose step failed is not saved, and the project counts as
        /// migrated only when everything succeeded, so the offer comes back and a retry is safe (steps are idempotent).
        /// Refuses to start with unsaved scenes or an open Prefab Mode. Returns a German report (also logged); lines
        /// starting with "!" are failures.
        /// </summary>
        public static List<string> RunAll(bool interactive = true, string folder = "Assets", Func<MethodInfo, bool> only = null, bool includeTests = false)
        {
            var report = new List<string>();
            var steps = Steps(includeTests).Where(s => only == null || only(s.method)).ToList();
            foreach (var (step, m) in steps)
                if (SignatureProblem(step, m) is string why) report.Add($"! {step.What} ({m.DeclaringType?.Name}.{m.Name}) {why}");
            if (report.Count > 0) return Log(report);
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            { report.Add("! abgebrochen: zuerst den Prefab-Modus schließen"); return Log(report); }
            bool safe = interactive ? KitPaths.OpenScenesSafe() : !AnyDirtyScene();   // never a dialog when not interactive
            if (!safe) { report.Add("! abgebrochen: eine geöffnete Szene hat ungespeicherte Änderungen"); return Log(report); }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var g in AssetDatabase.FindAssets("t:LogicGraph", new[] { folder }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
                for (int k = 0; k < steps.Count;)
                {
                    if (!steps[k].step.PerContainer)
                    {
                        var (step, m) = steps[k++];
                        var error = Invoke(m, new object[] { folder });
                        report.Add(error == null ? "✓ " + step.What : $"! {step.What}: {error}");
                        continue;
                    }
                    var pass = new List<(KitMigrationStepAttribute step, MethodInfo method)>();
                    while (k < steps.Count && steps[k].step.PerContainer) pass.Add(steps[k++]);
                    report.AddRange(ContainerPass(pass, folder, interactive));
                }
                if (folder == "Assets" && !report.Any(l => l.StartsWith("!"))) KitMigrationState.instance.Set(Current);
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
                if (setup.Length > 0 && setup.All(x => !string.IsNullOrEmpty(x.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            return Log(report);
        }

        static bool AnyDirtyScene()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        static List<string> ContainerPass(List<(KitMigrationStepAttribute step, MethodInfo method)> pass, string folder, bool interactive)
        {
            var lines = new List<string>();
            var failed = new HashSet<string>();
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Where(KitPaths.IsWritable).ToList();
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Where(KitPaths.IsWritable).ToList();
            int done = 0, total = scenes.Count + prefabs.Count;
            foreach (var path in prefabs)
            {
                Progress("Prefab " + path, done++, total, interactive);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var errors = pass.Select(s => (s.step, error: Invoke(s.method, new object[] { new[] { root } }))).Where(x => x.error != null).ToList();
                    foreach (var e in errors) { lines.Add($"! {e.step.What} in {path}: {e.error}"); failed.Add(e.step.What); }
                    if (errors.Count == 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                        if (!saved) { lines.Add($"! {path} nicht gespeichert"); failed.Add(path); }
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var path in scenes)
            {
                Progress("Szene " + path, done++, total, interactive);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var errors = pass.Select(s => (s.step, error: Invoke(s.method, new object[] { scene.GetRootGameObjects() }))).Where(x => x.error != null).ToList();
                foreach (var e in errors) { lines.Add($"! {e.step.What} in {path}: {e.error}"); failed.Add(e.step.What); }
                // Always saved: a step that sets values by script does not mark the scene dirty. Unchanged scenes come out identical.
                if (errors.Count == 0 && !EditorSceneManager.SaveScene(scene)) { lines.Add($"! {path} nicht gespeichert"); failed.Add(path); }
            }
            foreach (var (step, _) in pass)
                if (!failed.Contains(step.What)) lines.Add($"✓ {step.What} ({scenes.Count} Szenen, {prefabs.Count} Prefabs)");
            return lines;
        }

        /// <summary>Calls a step; returns null on success, else the error (also logged with its stack).</summary>
        static string Invoke(MethodInfo m, object[] args)
        {
            try
            {
                m.Invoke(null, m.GetParameters().Length == 0 ? null : args);
                return null;
            }
            catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); return (e.InnerException ?? e).Message; }
            catch (Exception e) { Debug.LogException(e); return e.Message; }
        }

        static List<string> Log(List<string> report)
        {
            foreach (var line in report)
                if (line.StartsWith("!")) Debug.LogWarning(KitBrand.Log + "Anpassen: " + line);
                else Debug.Log(KitBrand.Log + "Anpassen: " + line);
            return report;
        }

        static void Progress(string what, int done, int total, bool interactive)
        {
            if (interactive) EditorUtility.DisplayProgressBar("Projekt anpassen", what, total == 0 ? 1 : (float)done / total);
        }
    }
}
