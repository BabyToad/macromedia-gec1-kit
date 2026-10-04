using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Gemeinsamer Zustand: Merker, die mehrere Interaktionen teilen (z.B. "Strom an": der Sicherungskasten
    // schreibt, der Fahrstuhl liest). Die Regeln stehen hier, an einem sichtbaren Objekt in der Szene:
    //  - Besitzer: das Objekt mit dieser Komponente. Ohne es gibt es den Merker nicht.
    //  - Start: jeder Merker beginnt bei jedem Play mit seinem Startwert (kein Speichern zwischen Plays).
    //  - Schreiben: "alle" oder nur die eingetragenen Interaktionen. Andere werden blockiert (mit Grund).
    //  - Verschwindet der Besitzer, melden alle Knoten, die ihn benutzen, einen Fehler.
    [AddComponentMenu("Kit/Gemeinsamer Zustand")]
    public class KitSharedState : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string name = "Strom an";
            public bool startValue;
            [Tooltip("Leer = alle Interaktionen dürfen schreiben")]
            public List<Interaction> writers = new List<Interaction>();
            [NonSerialized] public bool value;
        }

        public List<Entry> entries = new List<Entry> { new Entry() };

        /// <summary>(Name, neuer Wert, wer geschrieben hat) nach jeder echten Änderung.</summary>
        public event Action<string, bool, Interaction> Changed;

        void Awake() { foreach (var e in entries) e.value = e.startValue; }

        public Entry Find(string name) => entries.Find(e => e.name == name);

        /// <summary>Schreibt, wenn erlaubt. Gibt null zurück oder den Grund, warum nicht.</summary>
        public string TrySet(string name, bool value, Interaction writer, out bool changed)
        {
            changed = false;
            var e = Find(name);
            if (e == null) return $"„{this.name}“ hat keinen Merker „{name}“";
            if (e.writers.Count > 0 && !e.writers.Contains(writer))
                return $"nur {string.Join(", ", e.writers.ConvertAll(w => w ? $"„{w.name}“" : "?"))} darf „{name}“ ändern";
            if (e.value == value) return null;
            e.value = value; changed = true;
            Changed?.Invoke(name, value, writer);
            return null;
        }
    }
}
