using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kit
{
    // Dialogfenster: zeigt eine Zeile (Sprecher + Text) und bis zu drei Antworten. Weiter mit E, Leertaste
    // oder A/Kreuz; Antworten mit 1/2/3 oder X/Y/B am Gamepad. Es zeigt immer nur EINE Zeile:
    // Kommt eine neue, während eine offen ist, wird die alte abgebrochen.
    // Jede gezeigte Zeile bekommt eine Nummer (Token). Schließen kann nur, wer diese Nummer hat –
    // so kann eine Interaktion nie die Zeile einer anderen wegnehmen.
    // Anlegen: GameObject > Kit > Dialogfenster.
    [AddComponentMenu("Kit/Dialogfenster")]
    public class KitDialogBox : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text speaker, line;
        public TMP_Text[] answers = new TMP_Text[3];

        public const int Next = -1, Cancelled = -2, Gone = -3;

        /// <summary>Done callback: -1 = weiter, 0..2 = Antwort, -2 = von einer anderen Zeile verdrängt, -3 = Fenster ausgeschaltet oder gelöscht.</summary>
        Action<int> m_Done;
        int m_Token, m_AnswerCount;
        float m_OpenedAt;

        public bool IsOpen => m_Done != null;
        public int OpenToken => m_Done != null ? m_Token : 0;

        /// <summary>Für Tests und Demos: als hätte jemand diese Antwort gewählt (-1 = weiter).</summary>
        [NonSerialized] public int? scriptedChoice;

        /// <summary>Zeigt eine Zeile. Gibt die Nummer zurück, mit der nur der Aufrufer sie wieder schließen kann.</summary>
        public int Show(string who, string text, string[] choices, Action<int> done)
        {
            // Erst den Platz übernehmen, dann die alte Zeile benachrichtigen: deren Rückruf darf sofort selbst
            // eine neue Zeile zeigen (z.B. Abgebrochen → nächster Dialog). Die verdrängt dann diese hier sauber,
            // statt von ihr überschrieben zu werden.
            var old = m_Done;
            m_Done = done;
            int token = ++m_Token;
            m_AnswerCount = 0;
            if (speaker) speaker.text = who;
            if (line) line.text = text;
            for (int i = 0; i < answers.Length; i++)
            {
                bool used = i < choices.Length && !string.IsNullOrEmpty(choices[i]);
                if (used) m_AnswerCount = i + 1;
                if (answers[i]) { answers[i].gameObject.SetActive(used); answers[i].text = used ? $"{i + 1}  {choices[i]}" : ""; }
            }
            if (panel) panel.SetActive(true);
            m_OpenedAt = Time.unscaledTime;
            old?.Invoke(Cancelled);                                // die alte Zeile endet hier (darf neu öffnen)
            return token;
        }

        /// <summary>Schließt die Zeile, aber nur wenn es noch die mit dieser Nummer ist. Kein Rückruf.</summary>
        public bool Close(int token)
        {
            if (m_Done == null || token != m_Token) return false;
            m_Done = null;
            if (panel) panel.SetActive(false);
            return true;
        }

        // Fenster aus- oder abgeschaltet (Löschen schaltet vorher auch aus): die offene Zeile endet, und wer
        // wartet, erfährt es – so gibt er auch den Spieler frei. Sonst bliebe die Zeile für immer offen.
        void OnDisable()
        {
            if (m_Done == null) return;
            var done = m_Done; m_Done = null;
            if (panel) panel.SetActive(false);
            done(Gone);
        }

        void Update()
        {
            if (m_Done == null || Time.unscaledTime - m_OpenedAt < 0.15f) return;   // nicht mit dem Öffnen-Tastendruck weiterklicken
            int choice = scriptedChoice ?? Read();
            scriptedChoice = null;
            if (choice == int.MinValue) return;
            if (m_AnswerCount > 0 && choice < 0) return;          // es gibt Antworten: "weiter" reicht nicht
            if (m_AnswerCount == 0 && choice >= 0) choice = Next; // keine Antworten: jede Taste ist "weiter"
            if (choice >= m_AnswerCount) return;
            var done = m_Done;
            m_Done = null;
            if (panel) panel.SetActive(false);
            done(choice);
        }

        int Read()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame) return 2;
                if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) return Next;
            }
            if (pad != null)
            {
                if (pad.buttonWest.wasPressedThisFrame) return 0;
                if (pad.buttonNorth.wasPressedThisFrame) return 1;
                if (pad.buttonEast.wasPressedThisFrame) return 2;
                if (pad.buttonSouth.wasPressedThisFrame) return Next;
            }
            return int.MinValue;
        }
    }
}
