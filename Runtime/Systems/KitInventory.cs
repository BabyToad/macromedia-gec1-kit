using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Inventar: zählt Gegenstände nach Namen ("Schlüssel", "Münze"). Liegt meist am Spieler.
    // Was beim Start schon drin ist, steht in der Liste. Jedes Play beginnt damit.
    [AddComponentMenu("Kit/Inventar")]
    public class KitInventory : MonoBehaviour
    {
        [Serializable] public struct Entry { public string item; [Min(0)] public int count; }

        [Tooltip("Inhalt beim Start")] public List<Entry> startWith = new List<Entry>();

        readonly Dictionary<string, int> m_Counts = new Dictionary<string, int>();

        /// <summary>(Gegenstand, neue Anzahl) nach jeder Änderung.</summary>
        public event Action<string, int> Changed;

        void Awake()
        {
            m_Counts.Clear();
            foreach (var e in startWith) if (!string.IsNullOrEmpty(e.item) && e.count > 0) m_Counts[e.item] = Count(e.item) + e.count;
        }

        public int Count(string item) => item != null && m_Counts.TryGetValue(item, out var n) ? n : 0;

        /// <summary>Legt n Stück dazu. n muss mindestens 1 sein.</summary>
        public void Add(string item, int n)
        {
            Check(item, n, "Add");
            m_Counts[item] = Count(item) + n;
            Changed?.Invoke(item, m_Counts[item]);
        }

        /// <summary>Nimmt bis zu n heraus; gibt zurück, wie viele es wirklich waren.</summary>
        public int Remove(string item, int n)
        {
            Check(item, n, "Remove");
            int taken = Mathf.Min(n, Count(item));
            if (taken == 0) return 0;
            m_Counts[item] = Count(item) - taken;
            Changed?.Invoke(item, m_Counts[item]);
            return taken;
        }

        public IEnumerable<KeyValuePair<string, int>> All => m_Counts;

        // Negative Anzahlen würden beim Entfernen Gegenstände erzeugen und beim Hinzufügen welche wegnehmen.
        static void Check(string item, int n, string what)
        {
            if (string.IsNullOrEmpty(item)) throw new ArgumentException($"KitInventory.{what}: kein Gegenstand angegeben", nameof(item));
            if (n < 1) throw new ArgumentOutOfRangeException(nameof(n), n, $"KitInventory.{what}: die Anzahl muss mindestens 1 sein");
        }
    }
}
