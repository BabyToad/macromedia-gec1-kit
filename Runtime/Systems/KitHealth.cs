using System;
using UnityEngine;

namespace Kit
{
    // Lebenspunkte eines Objekts (Spieler, Gegner, Kiste). Die Komponente hält den Wert; Knoten
    // („Lebenspunkte“) ändern ihn. So können mehrere Interaktionen (Lava, Heiltrank, Gegner)
    // dieselben Lebenspunkte benutzen. Jedes Play beginnt mit dem Startwert.
    [AddComponentMenu("Kit/Lebenspunkte")]
    public class KitHealth : MonoBehaviour
    {
        [Min(1)] public float max = 100f;
        [Min(0)] public float start = 100f;

        public float Current { get; private set; }
        public bool Dead => Current <= 0f;

        /// <summary>(alter Wert, neuer Wert) nach jeder Änderung.</summary>
        public event Action<float, float> Changed;

        void Awake() => Current = Mathf.Clamp(start, 0f, max);

        /// <summary>Ändert die Lebenspunkte um <paramref name="delta"/>, begrenzt auf 0 … max.</summary>
        public float Change(float delta)
        {
            float old = Current;
            Current = Mathf.Clamp(Current + delta, 0f, max);
            if (!Mathf.Approximately(old, Current)) Changed?.Invoke(old, Current);
            return Current - old;   // was wirklich passiert ist
        }

        public void ResetToStart()
        {
            float old = Current;
            Current = Mathf.Clamp(start, 0f, max);
            if (!Mathf.Approximately(old, Current)) Changed?.Invoke(old, Current);
        }
    }
}
