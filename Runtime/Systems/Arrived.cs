using System;
using UnityEngine;

namespace Kit
{
    // Angekommen: feuert, wenn der Spieler aus einer anderen Szene an diesem Eingang ankommt (nicht beim ersten
    // Start der Szene im Editor). Zum Beispiel: Tür hinter ihm zufallen lassen, Ortsnamen einblenden.
    [Serializable]
    [NodeInfo("Angekommen", KitGroups.Ausloeser, "Spieler kam aus einer anderen Szene an diesem Eingang an")]
    [Hint("Feuert, wenn der Spieler aus einer anderen Szene an diesem Eingang ankommt. Nicht beim ersten Start der Szene im Editor.")]
    public class Arrived : KitNode
    {
        [Ref("Eingang", Permanent = true)]
        [Hint("Der Eingang, an dem gewartet wird: ein Objekt mit der Komponente Eingang.")]
        public KitEntrance entrance;

        [Output("Angekommen")]
        [Hint("Feuert einmal pro Ankunft, nachdem der Spieler hingestellt ist.")]
        [NonSerialized] public Output arrived;

        [RefOut("Spieler")]
        [Hint("Der Spieler, der angekommen ist.")]
        [NonSerialized] public GameObject who;

        KitEntrance listening;

        public override void OnStart()
        {
            listening = entrance;
            listening.PlayerArrived += OnArrived;
        }

        void OnArrived(KitPlayer p)
        {
            who = p ? p.gameObject : null;
            Done($"✓ „{(p ? p.name : "?")}“ ist da");
            arrived.Fire(new Signal());
        }

        public override void OnStop()
        {
            if (listening != null) listening.PlayerArrived -= OnArrived;
            listening = null;
        }
    }
}
