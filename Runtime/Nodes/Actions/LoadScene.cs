using System;
using System.Collections.Generic;

namespace Kit
{
    // Szene laden: wechselt zu einer anderen Szene. Die Szene-Datei wird hineingezogen (kein Name zum Abtippen); das
    // Kit trägt jede Szene selbst in die Build Settings ein. Mit „Eingang“ kommt der Spieler an dem Eingang an, der in
    // der neuen Szene so heißt. Ohne Namen gilt: der Eingang, der wie die alte Szene heißt, sonst der Standard-Eingang,
    // sonst bleibt der Spieler dort, wo er in der neuen Szene steht.
    // „Wird geladen“ feuert sofort – alles, was daran hängt, läuft noch in der alten Szene und verschwindet mit ihr.
    [Serializable]
    [NodeInfo("Szene laden", KitGroups.Szene, "Zu einer anderen Szene wechseln, auf Wunsch an einem Eingang")]
    [Hint("Wechselt zu einer anderen Szene. Der Spieler kommt an einem Eingang an: am genannten, sonst an dem, der so heißt wie die alte Szene, sonst am Standard-Eingang.")]
    public class LoadScene : KitNode
    {
        [Setting("Szene")]
        [Hint("Die Szene, in die es geht: Szene-Datei aus dem Project-Fenster hineinziehen. Sie kommt von selbst in die Build Settings.",
              edges: "Leer: der Knoten meldet einen Fehler.")]
        public SceneRef target;
        [Setting("Eingang")]
        [Hint("Name des Eingangs in der neuen Szene, also der Name seines Objekts in der Hierarchy.",
              edges: "Leer: der Eingang, der so heißt wie die Szene, aus der der Spieler kommt; sonst der Standard-Eingang.")]
        public string entrance = "";
        [Setting("Ganzes Spiel zurücksetzen")]
        [Hint("Alle Variablen mit dem Bereich Spiel beginnen wieder bei ihrem Startwert, wie bei einem neuen Spiel. Für einen Knopf „Neues Spiel“.",
              edges: "Aus: Spiel-Variablen bleiben, wie sie sind.")]
        public bool resetGame = false;

        // Kit v0.3: the scene was a typed name. Still honoured when no scene file is set; the migration step turns it
        // into a scene file. Only in the Inspector, not on the node.
        [Setting("Alter Szenenname (v0.3)")]
        [Hint("Aus Kit v0.3: der getippte Name der Szene. Gilt nur, solange oben keine Szene-Datei steht; " + KitBrand.ToolsPath + "Projekt auf den neuen Stand bringen ersetzt ihn durch die Datei.")]
        public string scene = "";

        [Output("Wird geladen")]
        [Hint("Feuert sofort beim Start des Wechsels. Was daran hängt, läuft noch in der alten Szene und verschwindet mit ihr.")]
        [NonSerialized] public Output loading;

        /// <summary>Path of the scene to load: the scene file, or (v0.3 graphs) the scene with the typed name.</summary>
        public string TargetPath
        {
            get
            {
                if (target != null && !target.IsEmpty) return target.Path;
                if (string.IsNullOrEmpty(scene)) return "";
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
                {
                    var p = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                    if (SceneRef.NameOf(p) == scene) return p;
                }
                return scene;   // not in the build: the request fails with a clear reason
            }
        }

        string TargetName => SceneRef.NameOf(TargetPath);

        [Input("Laden")]
        [Hint("Startet den Wechsel: zudecken, neue Szene laden, Spieler an den Eingang stellen, aufdecken.")]
        public void Load(Signal s)
        {
            var refused = KitScenes.Load(TargetPath, entrance, resetGame);
            if (refused != null) { Fail(refused); return; }
            Done(string.IsNullOrEmpty(entrance) ? $"lädt „{TargetName}“" : $"lädt „{TargetName}“, Eingang „{entrance}“");
            loading.Fire(s);
        }

        public override void Validate(List<string> problems)
        {
            if (string.IsNullOrEmpty(TargetPath)) { problems.Add("keine Szene eingetragen: Szene-Datei aus dem Project-Fenster hineinziehen"); return; }
#if UNITY_EDITOR
            foreach (var p in KitSceneChecks.Problems(TargetPath, entrance)) problems.Add(p);
#endif
        }

        public override string Readout() => string.IsNullOrEmpty(TargetPath) ? "keine Szene" :
            string.IsNullOrEmpty(entrance) ? $"→ {TargetName}" : $"→ {TargetName} › {entrance}";
    }
}
