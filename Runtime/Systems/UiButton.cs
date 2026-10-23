using System;
using UnityEngine.UI;

namespace Kit
{
    // Knopf gedrückt: feuert, wenn ein UI-Knopf (Button im Canvas) angeklickt wird. Menüs bauen ohne Skript:
    // Knopf „Weiter“ → Weiter, Knopf „Beenden“ → Szene laden. Damit man klicken kann, muss der Mauszeiger frei
    // sein: dafür den Knoten Pause benutzen.
    [Serializable]
    [NodeInfo("Knopf gedrückt", KitGroups.Ausloeser, "UI-Knopf wurde angeklickt")]
    public class UiButton : KitNode
    {
        [Hint("Der UI-Knopf (Button im Canvas). Klicken geht nur mit freiem Mauszeiger, z.B. nach „Pause“, und die Szene braucht ein EventSystem.")]
        [Ref("Knopf", Permanent = true)] public Button button;

        [Hint("Feuert bei jedem Klick auf den Knopf.")]
        [Output("Gedrückt")] [NonSerialized] public Output pressed;

        Button listening;

        // Running "Knopf gedrückt" nodes per button (a screen uses it to say what is still missing).
        static readonly System.Collections.Generic.Dictionary<Button, System.Collections.Generic.List<UiButton>> s_On =
            new System.Collections.Generic.Dictionary<Button, System.Collections.Generic.List<UiButton>>();

        /// <summary>True while a running "Knopf gedrückt" listens to this button and its "Gedrückt" leads to an action.</summary>
        public static bool IsListening(Button b)
        {
            if (b == null || !s_On.TryGetValue(b, out var list)) return false;
            foreach (var n in list) if (n.pressed != null && n.pressed.IsConnected) return true;
            return false;
        }

        /// <summary>The Logik whose running "Knopf gedrückt" has this button bound, or null.</summary>
        public static Logic BoundBy(Button b) => b != null && s_On.TryGetValue(b, out var list) && list.Count > 0 ? list[0].Runner : null;

        // The buttons whose click a "Knopf gedrückt" handled in the current frame (its action may switch the Logik off
        // in the middle of the click). Only one frame is kept, so no destroyed button stays referenced.
        static int s_HandledFrame = -1;
        static readonly System.Collections.Generic.HashSet<Button> s_Handled = new System.Collections.Generic.HashSet<Button>();

        /// <summary>True if a "Knopf gedrückt" handled a click of this button in the current frame.</summary>
        public static bool HandledThisFrame(Button b) => b != null && s_HandledFrame == UnityEngine.Time.frameCount && s_Handled.Contains(b);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() { s_On.Clear(); s_Handled.Clear(); s_HandledFrame = -1; }

        public override void OnStart()
        {
            listening = button;
            listening.onClick.AddListener(Clicked);
            if (!s_On.TryGetValue(listening, out var list)) s_On[listening] = list = new System.Collections.Generic.List<UiButton>();
            list.Add(this);
        }

        void Clicked()
        {
            if (s_HandledFrame != UnityEngine.Time.frameCount) { s_Handled.Clear(); s_HandledFrame = UnityEngine.Time.frameCount; }
            if (listening != null && pressed.IsConnected) s_Handled.Add(listening);
            Done("✓ Gedrückt");
            pressed.Fire(new Signal());
        }

        public override void OnStop()
        {
            if (!ReferenceEquals(listening, null) && s_On.TryGetValue(listening, out var list))
            {
                list.Remove(this);
                if (list.Count == 0) s_On.Remove(listening);
            }
            if (listening != null) listening.onClick.RemoveListener(Clicked);
            listening = null;
        }
    }
}
