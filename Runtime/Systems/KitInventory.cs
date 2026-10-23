using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Inventar: zählt Gegenstände nach Namen ("Schlüssel", "Münze"). Liegt meist am Spieler.
    // Was beim Start schon drin ist, steht in der Liste. Jedes Play beginnt damit.
    // Bereich Spiel: der Inhalt bleibt beim Laden einer anderen Szene erhalten (dort am Inventar mit demselben Namen).
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu(KitBrand.ComponentMenu + "Inventar")]
    public class KitInventory : MonoBehaviour
    {
        [Serializable] public struct Entry
        {
            [Hint("Name des Gegenstands, z.B. „Schlüssel“. Groß/klein zählt.", label: "Gegenstand")] public string item;
            [Hint("Wie viele davon.", label: "Anzahl")] [Min(0)] public int count;
        }

        [Hint("Name des Inventars. Im Bereich Spiel findet die nächste Szene den Inhalt über diesen Namen.", label: "Name")]
        [SerializeField] string bagName = "Inventar";
        [Hint("Szene: der Inhalt gilt, bis eine andere Szene geladen wird. Spiel: er reist mit in die nächste Szene (z.B. der Schlüssel aus Level 1).", label: "Bereich")]
        [SerializeField] ObjectScope scope = ObjectScope.Szene;
        [Hint("Inhalt beim Start (Bereich Spiel: nur beim ersten Mal im Spiel).", label: "Anfangsinhalt")] public List<Entry> startWith = new List<Entry>();

        KitBag m_Bag;
        int m_Session;      // the Play session m_Bag belongs to (see KitValues.Session)

        public string BagName => bagName;
        public ObjectScope Bereich => scope;

        /// <summary>The live contents (made on first use, so code and tests may use it before Awake).</summary>
        public KitBag Bag
        {
            get
            {
                // Enter Play Mode without domain reload keeps this object, but the stores start empty: start over too.
                if (m_Session != KitValues.Session) { if (m_Bag != null) m_Bag.Changed -= Relay; m_Bag = null; m_Session = KitValues.Session; }
                if (m_Bag != null) return m_Bag;
                bool created = true;
                m_Bag = scope == ObjectScope.Spiel ? KitBags.Get(bagName, Scope.Spiel, null, gameObject.scene, out created) : KitBags.Own(bagName);
                if (created)
                {
                    var start = new List<KeyValuePair<string, int>>();
                    foreach (var e in startWith) start.Add(new KeyValuePair<string, int>(e.item, e.count));
                    m_Bag.SetStart(start);
                }
                m_Bag.Changed += Relay;
                return m_Bag;
            }
        }

        /// <summary>(Gegenstand, neue Anzahl) nach jeder Änderung.</summary>
        public event Action<string, int> Changed;
        void Relay(string item, int n) => Changed?.Invoke(item, n);

        void Awake() => _ = Bag;
        void OnDestroy() { if (m_Bag != null) m_Bag.Changed -= Relay; }

        /// <summary>For code and tests: name and scope before the contents are first used.</summary>
        public void Configure(string name, ObjectScope scope)
        {
            if (m_Bag != null) m_Bag.Changed -= Relay;
            bagName = name; this.scope = scope; m_Bag = null;
        }

        public int Count(string item) => Bag.Count(item);
        /// <summary>Legt n Stück dazu. n muss mindestens 1 sein.</summary>
        public void Add(string item, int n) => Bag.Add(item, n);
        /// <summary>Nimmt bis zu n heraus; gibt zurück, wie viele es wirklich waren.</summary>
        public int Remove(string item, int n) => Bag.Remove(item, n);
        public IEnumerable<KeyValuePair<string, int>> All => Bag.All;
    }
}
