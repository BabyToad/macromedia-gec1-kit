using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gec1.SetupCheck
{
    /// <summary>Collects Editor state and runs all checks in a fixed, student-readable order.</summary>
    internal static class SetupCheckRunner
    {
        public static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        public static List<CheckResult> Run()
        {
            string root = ProjectRoot;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var results = new List<CheckResult>
            {
                Checks.UnityVersion(Application.unityVersion),
                Checks.RenderPipeline(pipeline != null ? pipeline.GetType().FullName : null),
                Checks.Packages(root, StackConfig.RequiredPackages),
                Checks.ProjectPath(root, Application.platform == RuntimePlatform.WindowsEditor),
            };
            results.Add(Checks.GitOnPath(GitVersion(), Application.platform == RuntimePlatform.WindowsEditor));
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SetupCheckRunner).Assembly);
            results.Add(Checks.KitPackage(pkg != null ? pkg.version : null, pkg != null ? pkg.source.ToString() : null));
            results.AddRange(Checks.Git(root));
            results.Add(Checks.WindowsBuild(
                UnityEditor.BuildPipeline.IsBuildTargetSupported(UnityEditor.BuildTargetGroup.Standalone, UnityEditor.BuildTarget.StandaloneWindows64),
                Application.platform == RuntimePlatform.WindowsEditor, System.DateTime.Today));
            results.Add(Checks.LargeFiles(root));
            results.Add(Checks.AgentInstructions(root));
            results.Add(Checks.AiRegister(root, System.DateTime.Today));
            results.Add(Checks.Documentation(root));
            results.Add(Checks.CodeEditor(Unity.CodeEditor.CodeEditor.CurrentEditorInstallation));
            return results;
        }

        /// <summary>Runs "git --version" with this Unity process's PATH (the one the Package Manager uses).
        /// Returns null if git cannot be started or does not answer within 5 seconds.</summary>
        static string GitVersion()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) return null;
                    string output = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch (System.Exception) { } return null; }
                    // macOS without Command Line Tools: /usr/bin/git exists but exits non-zero.
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch (System.Exception)
            {
                return null; // not found on PATH
            }
        }

        public static string Header()
        {
            return "Setup-Check " + StackConfig.UnityLabel + " | Projekt: " + ProjectRoot + " | Unity " + Application.unityVersion
                   + " | " + SystemInfo.operatingSystem;
        }
    }
}
