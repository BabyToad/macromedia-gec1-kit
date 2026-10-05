using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Pause und Weiter: für Menüs. Pause hält den Spieler an und gibt den Mauszeiger frei, damit UI-Knöpfe
    // klickbar sind; auf Wunsch steht auch die Zeit still. Weiter hebt alle offenen Pausen wieder auf: der Spieler
    // läuft, der Mauszeiger ist wieder gefangen, die Zeit läuft im Tempo von vorher.
    // Jeder Pause-Knoten hält mit sich selbst als Besitzer an. Hört seine Interaktion auf (auch beim Laden einer
    // anderen Szene), gibt er seine Pause frei. Ein Dialog hält den Spieler davon unabhängig an, und ein Spieler,
    // der schon vorher aus war, bleibt aus.
    [Serializable]
    [NodeInfo("Pause", "Spielsysteme", "Spieler anhalten, Mauszeiger frei (für Menüs)")]
    public class MenuPause : KitNode
    {
        [Ref("Spieler", Optional = true)] public KitPlayer player;
        [Setting("Zeit anhalten")] public bool stopTime = false;

        [Output("Pausiert")] [NonSerialized] public Output paused;

        static readonly List<MenuPause> s_Open = new List<MenuPause>();   // offene Pausen (Knoten-Kopien)
        KitPlayer m_Held;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => s_Open.Clear();                     // auch ohne Domain-Reload beim Start von Play

        public static int OpenCount => s_Open.Count;

        [Input("Pausieren")]
        public void Pause(Signal s)
        {
            var p = player ? player : UnityEngine.Object.FindAnyObjectByType<KitPlayer>();
            if (m_Held && m_Held != p) m_Held.Resume(this);           // ein anderer Spieler als beim letzten Mal
            if (p) { p.Pause(this); m_Held = p; }
            KitCursor.Lock(false);                                    // Mauszeiger frei, auch ohne Spieler
            if (stopTime) KitTime.Hold(this, 0f); else KitTime.Release(this);
            if (!s_Open.Contains(this)) s_Open.Add(this);
            Done(p ? $"✓ „{p.name}“ angehalten, Mauszeiger frei" : "✓ Mauszeiger frei (kein Spieler in der Szene)");
            paused.Fire(s);
        }

        /// <summary>Gibt diese eine Pause frei (Weiter, oder die Interaktion hört auf).</summary>
        internal void ReleaseOwn()
        {
            if (m_Held) m_Held.Resume(this);                          // läuft er wieder, fängt er den Mauszeiger selbst
            m_Held = null;
            KitTime.Release(this);
            s_Open.Remove(this);
        }

        /// <summary>Weiter: alle offenen Pausen. Gibt zurück, wie viele es waren.</summary>
        internal static int ReleaseAllOpen()
        {
            var open = s_Open.ToArray();
            foreach (var m in open) m.ReleaseOwn();
            return open.Length;
        }

        public override void OnStop() => ReleaseOwn();
    }

    [Serializable]
    [NodeInfo("Weiter", "Spielsysteme", "Pause aufheben: Spieler läuft, Mauszeiger gefangen")]
    public class MenuResume : KitNode
    {
        [Output("Fortgesetzt")] [NonSerialized] public Output resumed;

        [Input("Fortsetzen")]
        public void Resume(Signal s)
        {
            int n = MenuPause.ReleaseAllOpen();
            Done(n > 0 ? "✓ Pause aufgehoben" : "✓ keine Pause offen");
            resumed.Fire(s);
        }
    }
}
