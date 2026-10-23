using System;
using System.Collections.Generic;

namespace Kit
{
    // Nächste Szene: wechselt zur Szene, die in den Build Settings nach der aktuellen steht (Level 1 → Level 2).
    // Die Reihenfolge steht im Fenster Szenen (Kit › Szenen); neue Szenen trägt das Kit hinten ein.
    // In der letzten Szene geht es nicht weiter: dann feuert „Keine weitere“, z.B. für einen Ende-Bildschirm.
    [Serializable]
    [NodeInfo("Nächste Szene", KitGroups.Szene, "Zur nächsten Szene in der Reihenfolge wechseln")]
    [Hint("Wechselt zur Szene, die in der Reihenfolge nach der aktuellen steht (" + KitBrand.ToolsPath + "Szenen). Der Spieler kommt am passenden Eingang an.")]
    public class NextScene : KitNode
    {
        [Output("Wird geladen")]
        [Hint("Feuert, wenn es eine nächste Szene gibt und der Wechsel beginnt.")]
        [NonSerialized] public Output loading;
        [Output("Keine weitere")]
        [Hint("Feuert in der letzten Szene statt eines Wechsels, z.B. für einen Ende-Bildschirm.")]
        [NonSerialized] public Output none;

        [Input("Weiter")]
        [Hint("Geht eine Szene weiter in der Reihenfolge.")]
        public void Next(Signal s)
        {
            var path = KitScenes.NextPath(out var reason);
            if (path == null)
            {
                Blocked(reason);
                none.Fire(s);
                return;
            }
            var refused = KitScenes.Next();
            if (refused != null) { Fail(refused); return; }
            Done($"lädt „{SceneRef.NameOf(path)}“");
            loading.Fire(s);
        }

        public override string Readout()
        {
            var path = KitScenes.NextPath(out _);
            return path == null ? "letzte Szene" : $"→ {SceneRef.NameOf(path)}";
        }
    }
}
