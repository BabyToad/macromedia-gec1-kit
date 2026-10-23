using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kit
{
    // Bildschirm: ein ganzer Bildschirm aus UI (Start, Pause, Gewonnen, Verloren …). Gezeigt wird er, indem man
    // sein Objekt einschaltet (An/Aus, oder das Häkchen im Inspector); ausschalten blendet ihn aus. Solange er zu
    // sehen ist, ist der Mauszeiger frei, damit man die Knöpfe klicken kann, und auf Wunsch steht das Spiel still.
    //
    // Die Knöpfe tun von sich aus nichts: was „Neustart“ heißt, entscheidet die eigene Logik
    // (Auslöser „Knopf gedrückt“ → Aktion „Neustart“). Ein Knopf, an dem noch nichts hängt, sagt das beim Klicken.
    [AddComponentMenu(KitBrand.ComponentMenu + "Bildschirm")]
    [DisallowMultipleComponent]
    public class KitScreen : MonoBehaviour
    {
        [Hint("Solange der Bildschirm zu sehen ist, steht das Spiel still: der Spieler läuft nicht, die Zeit steht " +
              "(Time.timeScale = 0). Knöpfe funktionieren trotzdem.", edges: "Aus: das Spiel läuft hinter dem Bildschirm weiter.", label: "Spiel anhalten")]
        public bool pauseGame = true;

        [Hint("Text, der kurz erscheint, wenn ein Knopf geklickt wird, an dem noch keine Logik hängt.", edges: "Leer: der Hinweis steht nur in der Console.", label: "Hinweis-Text")]
        public TMP_Text hint;

        [Hint("Wie lange der Hinweis zu sehen ist, in echter Zeit (also auch bei angehaltenem Spiel).", unit: "s", range: "2 bis 8", label: "Hinweis-Dauer")]
        [Min(0.5f)] public float hintSeconds = 4f;

        KitPlayer m_Held;
        readonly List<Button> m_Watched = new List<Button>();
        Coroutine m_Hint;

        /// <summary>The Bildschirm currently shown on top (the last one switched on).</summary>
        public static KitScreen Top { get; private set; }
        static readonly List<KitScreen> s_Shown = new List<KitScreen>();

        void OnEnable()
        {
            s_Shown.Remove(this); s_Shown.Add(this); Top = this;
            if (Application.isPlaying)
            {
                KitCursor.HoldFree(this);                     // also while the game runs on behind a non-pausing screen
                if (pauseGame)
                {
                    m_Held = FindAnyObjectByType<KitPlayer>();
                    if (m_Held) m_Held.Pause(this);
                    KitTime.Hold(this, 0f);
                }
                EnsureEventSystem();
                SelectFirstButton();
                Watch();
            }
            if (hint) hint.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            s_Shown.Remove(this);
            var below = s_Shown.Count > 0 ? s_Shown[s_Shown.Count - 1] : null;
            bool wasTop = Top == this;
            Top = below;
            foreach (var b in m_Watched) if (b) b.onClick.RemoveListener(Clicked(b));
            m_Watched.Clear();
            m_Clicks.Clear();
            KitTime.Release(this);
            KitCursor.ReleaseFree(this);
            if (m_Held) m_Held.Resume(this);                  // runs again → catches the cursor itself (unless another screen holds it)
            else if (Application.isPlaying && !KitCursor.HeldFree && FindAnyObjectByType<KitPlayer>() is KitPlayer p && p.isActiveAndEnabled)
                KitCursor.Lock(true);                         // the game ran on behind this screen: back to mouse look
            m_Held = null;
            m_Hint = null;
            if (Application.isPlaying && wasTop && below) below.SelectFirstButton();   // keyboard/gamepad focus goes to the screen below
        }

        // ---------------------------------------------------------------------------------- buttons
        readonly Dictionary<Button, UnityEngine.Events.UnityAction> m_Clicks = new Dictionary<Button, UnityEngine.Events.UnityAction>();

        UnityEngine.Events.UnityAction Clicked(Button b)
        {
            if (!m_Clicks.TryGetValue(b, out var a)) m_Clicks[b] = a = () => OnClicked(b);
            return a;
        }

        void Watch()
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                b.onClick.AddListener(Clicked(b));
                m_Watched.Add(b);
            }
        }

        /// <summary>
        /// True if something reacts to this button: a running "Knopf gedrückt" (or one that handled the click in this frame
        /// and then switched itself off), or an On Click entry in the Inspector that points at a real target.
        /// </summary>
        public static bool IsWired(Button b) => UiButton.IsListening(b) || UiButton.HandledThisFrame(b) || HasLiveOnClick(b);

        /// <summary>An On Click entry that is switched on and has a target object and a method.</summary>
        public static bool HasLiveOnClick(Button b)
        {
            var ev = b.onClick;
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                if (ev.GetPersistentTarget(i) && !string.IsNullOrEmpty(ev.GetPersistentMethodName(i))
                    && ev.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off) return true;
            return false;
        }

        void OnClicked(Button b)
        {
            // Our own listener runs with the others; if it is the only one, the click went nowhere. If the click
            // switched this screen off, it obviously did something.
            if (!this || !isActiveAndEnabled || IsWired(b)) return;
            var by = UiButton.BoundBy(b);
            var text = by ? NoActionText(b, by.name) : UnwiredText(b);
            Debug.LogWarning(KitBrand.Log + text, b);
            if (!hint) return;
            hint.text = text;
            hint.gameObject.SetActive(true);
            if (m_Hint != null) StopCoroutine(m_Hint);
            m_Hint = StartCoroutine(HideHint());
        }

        /// <summary>The one step that is missing, in German.</summary>
        public static string UnwiredText(Button b) =>
            $"Knopf „{b.name}“ ist noch nicht verbunden. Logik auf ein Objekt, Auslöser „Knopf gedrückt“ mit ◆ Knopf = „{b.name}“, " +
            "dann die Aktion dahinter: z.B. Neustart, oder An/Aus › Ausschalten mit diesem Bildschirm, um ihn zu schließen.";

        /// <summary>Bound to a "Knopf gedrückt", but its "Gedrückt" leads nowhere yet.</summary>
        public static string NoActionText(Button b, string logic) =>
            $"Knopf „{b.name}“ ist in der Logik „{logic}“ zugeordnet, aber „Gedrückt“ führt noch zu keiner Aktion: dort „Gedrückt“ mit einer Aktion verbinden.";

        IEnumerator HideHint()
        {
            yield return new WaitForSecondsRealtime(hintSeconds);
            if (hint) hint.gameObject.SetActive(false);
            m_Hint = null;
        }

        /// <summary>Gives keyboard/gamepad focus to this screen's first usable button.</summary>
        public void SelectFirstButton()
        {
            var es = EventSystem.current;
            if (!es) return;
            foreach (var b in GetComponentsInChildren<Button>(false))
                if (b.interactable) { es.SetSelectedGameObject(b.gameObject); return; }
        }

        /// <summary>
        /// UI clicks need exactly one EventSystem in the scene. A screen brings one along if there is none (with the
        /// Input System module, like the course template). Returns the new one, or null if there already was one.
        /// </summary>
        public static GameObject EnsureEventSystem()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var e in all) if (e.isActiveAndEnabled) return null;
            if (all.Length > 0)
                Debug.LogWarning(KitBrand.Log + $"„{all[0].name}“ ist ausgeschaltet; ohne eingeschaltetes EventSystem reagieren keine Knöpfe. Ein neues wird angelegt.", all[0]);
            return new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() { s_Shown.Clear(); Top = null; }
    }
}
