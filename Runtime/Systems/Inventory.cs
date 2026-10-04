using System;

namespace Kit
{
    // Inventar: Gegenstand hinzufügen, entfernen oder prüfen – im „Inventar“ eines Objekts (meist Spieler).
    // Prüfen schickt das Ereignis zu "Hat es" oder "Hat es nicht". Entfernen, was nicht da ist, wird blockiert.
    [Serializable]
    [NodeInfo("Inventar", "Spielsysteme", "Gegenstände hinzufügen, entfernen, prüfen")]
    public class Inventory : KitNode
    {
        [Ref("Inventar")] public KitInventory inventory;
        [Setting("Gegenstand")] public string item = "Schlüssel";
        [Setting("Anzahl")] public int count = 1;

        [Output("Hinzugefügt")] [NonSerialized] public Output added;
        [Output("Entfernt")] [NonSerialized] public Output removed;
        [Output("Hat es")] [NonSerialized] public Output has;
        [Output("Hat es nicht")] [NonSerialized] public Output hasNot;
        [DataOut("Anzahl")] public float Amount => inventory != null ? inventory.Count(item) : 0;

        [Input("Hinzufügen")]
        public void Add(Signal s)
        {
            if (!Valid()) return;
            inventory.Add(item, count);
            Done($"{item}: {inventory.Count(item)}");
            added.Fire(s);
        }

        [Input("Entfernen")]
        public void Remove(Signal s)
        {
            if (!Valid()) return;
            if (inventory.Count(item) < count) { Blocked($"nur {inventory.Count(item)}× {item} da, {count} gebraucht"); return; }
            inventory.Remove(item, count);
            Done($"{item}: {inventory.Count(item)}");
            removed.Fire(s);
        }

        [Input("Prüfen")]
        public void Check(Signal s)
        {
            if (!Valid()) return;
            bool yes = inventory.Count(item) >= count;
            var chosen = yes ? has : hasNot;
            string why = $"{item}: {inventory.Count(item)} (gebraucht {count})";
            if (!chosen.IsConnected) { Blocked($"{(yes ? "Hat es" : "Hat es nicht")} ist nicht angeschlossen – {why}"); return; }
            Done($"→ {(yes ? "hat es" : "hat es nicht")}, {why}");
            chosen.Fire(s);
        }

        // Anzahl unter 1 ergibt keinen Sinn: Entfernen von -3 würde 3 Stück erzeugen.
        bool Valid()
        {
            string problem = Problem();
            if (problem == null) return true;
            Fail(problem);
            return false;
        }

        string Problem() =>
            string.IsNullOrWhiteSpace(item) ? "kein Gegenstand eingetragen"
            : count < 1 ? $"Anzahl {count} geht nicht: mindestens 1"
            : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            var p = Problem();
            if (p != null) problems.Add(p);
        }

        public override string Readout() => inventory != null ? $"{item}: {inventory.Count(item)}" : null;
    }
}
