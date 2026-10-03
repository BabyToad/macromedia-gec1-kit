using UnityEditor;
using UnityEngine;

namespace Gec1.SetupCheck
{
    /// <summary>
    /// Command-line entry point, for agents and CI:
    /// Unity -batchmode -projectPath &lt;project&gt; -executeMethod Gec1.SetupCheck.SetupCheckBatch.Run -logFile -
    /// Exit code 0 = no errors, 1 = at least one error (warnings do not fail).
    /// </summary>
    public static class SetupCheckBatch
    {
        public static void Run()
        {
            var results = SetupCheckRunner.Run();
            Debug.Log("GEC1-SETUPCHECK-BEGIN\n" + CheckResult.Report(results, SetupCheckRunner.Header()) + "GEC1-SETUPCHECK-END");
            bool failed = results.Exists(r => r.Status == CheckStatus.Fail);
            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
