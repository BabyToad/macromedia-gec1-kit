using System;

namespace Kit
{
    // Spiel beenden: schließt das fertige Spiel (Build). Im Editor endet stattdessen der Play-Modus.
    [Serializable]
    [NodeInfo("Spiel beenden", KitGroups.Szene, "Das Spiel schließen (im Editor: Play beenden)")]
    [Hint("Schließt das fertige Spiel. Im Editor endet stattdessen der Play-Modus. Für einen Knopf „Beenden“.")]
    public class QuitGame : KitNode
    {
        [Input("Beenden")]
        [Hint("Beendet das Spiel sofort.")]
        public void Quit(Signal s)
        {
#if UNITY_EDITOR
            Done("✓ Play beendet (im fertigen Spiel: Fenster zu)");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Done("✓ beendet");
            UnityEngine.Application.Quit();
#endif
        }
    }
}
