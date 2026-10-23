using System;
using UnityEngine;

namespace Kit
{
    // An/Aus: schaltet ein GameObject ein oder aus (wie das Häkchen oben im Inspector).
    // Fehlt das Objekt, ist das ein Fehler – es wird nicht still "ich selbst" geschaltet.
    [Serializable]
    [NodeInfo("An/Aus", KitGroups.Aktion, "GameObject ein- oder ausschalten")]
    public class SetActive : KitNode
    {
        [Hint("Das Objekt, das ein- oder ausgeschaltet wird, samt seinen Kindobjekten. Ist es das Objekt mit dieser Logik, hört die Logik beim Ausschalten auf.")]
        [Ref("Objekt")] public GameObject target;

        [Hint("Feuert direkt nach dem Schalten.")]
        [Output("Erledigt")] [NonSerialized] public Output done;

        [Hint("Schaltet das Objekt ein.")]
        [Input("Einschalten")] public void On(Signal s) => Apply(true, s);
        [Hint("Schaltet das Objekt aus.")]
        [Input("Ausschalten")] public void Off(Signal s) => Apply(false, s);
        [Hint("Schaltet um: an wird aus, aus wird an.")]
        [Input("Umschalten")] public void Toggle(Signal s) => Apply(!target.activeSelf, s);

        void Apply(bool active, Signal s)
        {
            target.SetActive(active);
            Done($"✓ {target.name} {(active ? "an" : "aus")}");
            done.Fire(s);
        }
    }
}
