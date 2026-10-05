using System;
using UnityEngine.UI;

namespace Kit
{
    // Knopf gedrückt: feuert, wenn ein UI-Knopf (Button im Canvas) angeklickt wird. Menüs bauen ohne Skript:
    // Knopf „Weiter“ → Weiter, Knopf „Beenden“ → Szene laden. Damit man klicken kann, muss der Mauszeiger frei
    // sein: dafür den Knoten Pause benutzen.
    [Serializable]
    [NodeInfo("Knopf gedrückt", "Auslöser", "UI-Knopf wurde angeklickt")]
    public class UiButton : KitNode
    {
        [Ref("Knopf", Permanent = true)] public Button button;

        [Output("Gedrückt")] [NonSerialized] public Output pressed;

        Button listening;

        public override void OnStart()
        {
            listening = button;
            listening.onClick.AddListener(Clicked);
        }

        void Clicked()
        {
            Done("✓ Gedrückt");
            pressed.Fire(new Signal());
        }

        public override void OnStop()
        {
            if (listening != null) listening.onClick.RemoveListener(Clicked);
            listening = null;
        }
    }
}
