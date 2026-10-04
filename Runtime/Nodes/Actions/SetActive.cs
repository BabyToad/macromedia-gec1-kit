using System;
using UnityEngine;

namespace Kit
{
    // An/Aus: schaltet ein GameObject ein oder aus (wie das Häkchen oben im Inspector).
    // Fehlt das Objekt, ist das ein Fehler – es wird nicht still "ich selbst" geschaltet.
    [Serializable]
    [NodeInfo("An/Aus", "Aktion", "GameObject ein- oder ausschalten")]
    public class SetActive : KitNode
    {
        [Ref("Objekt")] public GameObject target;

        [Output("Erledigt")] [NonSerialized] public Output done;

        [Input("Einschalten")] public void On(Signal s) => Apply(true, s);
        [Input("Ausschalten")] public void Off(Signal s) => Apply(false, s);
        [Input("Umschalten")] public void Toggle(Signal s) => Apply(!target.activeSelf, s);

        void Apply(bool active, Signal s)
        {
            target.SetActive(active);
            Done($"✓ {target.name} {(active ? "an" : "aus")}");
            done.Fire(s);
        }
    }
}
