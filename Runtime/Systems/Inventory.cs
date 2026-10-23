using System;
using UnityEngine;

namespace Kit
{
    // Inventar: Gegenstand hinzufügen, entfernen oder prüfen. Prüfen schickt das Ereignis zu "Hat es" oder
    // "Hat es nicht". Entfernen, was nicht da ist, wird blockiert.
    // Wessen Inventar: mit ◆ Inventar das eines Objekts (meist der Spieler, auch ◆ Wer aus einer Zone);
    // ohne das eines Bereichs, wie bei Variablen: Logik, Szene oder Spiel (bleibt beim Szenenwechsel), nach Name.
    [Serializable]
    [NodeInfo("Inventar", "Spielsysteme", "Gegenstände hinzufügen, entfernen, prüfen")]
    public class Inventory : KitNode
    {
        [Hint("Optional. Das Objekt mit der Komponente „Inventar“, z.B. der Spieler oder ◆ Wer. Leer: das Inventar des Bereichs (Name und Bereich unten).")]
        [Ref("Inventar", Optional = true)] public KitInventory inventory;
        [Hint("Nur ohne ◆ Inventar: Logik (nur diese Logik), Szene (alle in der Szene) oder Spiel (bleibt beim Laden einer anderen Szene).")]
        [Setting("Bereich")] public Scope scope = Scope.Szene;
        [Hint("Nur ohne ◆ Inventar: welches Inventar im Bereich, z.B. „Inventar“ oder „Truhe“.")]
        [Setting("Name")] public string bagName = "Inventar";
        [Hint("Name des Gegenstands, z.B. „Schlüssel“. Groß/klein zählt.")]
        [Setting("Gegenstand")] public string item = "Schlüssel";
        [Hint("Wie viele auf einmal hinzugefügt, entfernt oder beim Prüfen gebraucht werden (mindestens 1).")]
        [Setting("Anzahl")] public int count = 1;

        [Hint("Feuert nach dem Hinzufügen.")]
        [Output("Hinzugefügt")] [NonSerialized] public Output added;
        [Hint("Feuert nach dem Entfernen.")]
        [Output("Entfernt")] [NonSerialized] public Output removed;
        [Hint("Prüfen: mindestens Anzahl Stück sind da.")]
        [Output("Hat es")] [NonSerialized] public Output has;
        [Hint("Prüfen: weniger als Anzahl Stück sind da.")]
        [Output("Hat es nicht")] [NonSerialized] public Output hasNot;
        [Hint("Wie viele von diesem Gegenstand gerade da sind.")]
        [DataOut("Anzahl")] public float Amount => Peek()?.Count(item) ?? 0;

        [NonSerialized] KitBag m_ScopeBag;
        [NonSerialized] KitRefs.Wiring m_Wiring;
        [NonSerialized] string m_Slot;

        public override void OnStart() { m_ScopeBag = null; m_Wiring = KitRefs.Of(this, nameof(inventory), out m_Slot); }
        public override void OnStop() { m_ScopeBag = null; if (scope == Scope.Logik) KitBags.ReleaseLogik(Runner); }

        // Connected ◆ Inventar: that object's, or nothing. Only an unconnected node uses the Bereich.
        KitBag Peek() => inventory != null ? inventory.Bag : m_Wiring == KitRefs.Wiring.None ? ScopeBag() : null;

        string Missing() =>
            m_Wiring == KitRefs.Wiring.FromEvent ? "◆ Inventar: das Objekt aus dem Ereignis hat kein Inventar (Add Component › Inventar) oder wurde gelöscht"
            : KitRefs.Deleted(Runner, m_Slot) ? "◆ Inventar wurde gelöscht: ein anderes Objekt binden"
            : "◆ Inventar ist verbunden, aber das Objekt hat kein Inventar: Add Component › Inventar oder die Verbindung lösen";

        KitBag ScopeBag()
        {
            if (m_ScopeBag != null) return m_ScopeBag;
            if (Runner == null || string.IsNullOrWhiteSpace(bagName)) return null;
            if (scope == Scope.Szene)
            {
                // An Inventar component with this name in the scene is that scene's bag: one place to fill it at the start.
                KitInventory only = null; int n = 0;
                foreach (var c in UnityEngine.Object.FindObjectsByType<KitInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.gameObject.scene == Runner.gameObject.scene && c.BagName == bagName && c.Bereich == ObjectScope.Szene && n++ == 0) only = c;
                if (n == 1) return m_ScopeBag = only.Bag;
            }
            return m_ScopeBag = KitBags.Get(bagName, scope, Runner, Runner.gameObject.scene, out _);
        }

        string Where(KitBag bag) => inventory != null ? $"„{inventory.name}“" : $"{bag.Name} ({KitValues.ScopeName(bag.Scope)})";

        [Hint("Anzahl Stück dazulegen.")]
        [Input("Hinzufügen")]
        public void Add(Signal s)
        {
            var bag = Valid(); if (bag == null) return;
            bag.Add(item, count);
            Done($"{item}: {bag.Count(item)}");
            added.Fire(s);
        }

        [Hint("Anzahl Stück herausnehmen; sind es zu wenige, passiert nichts (blockiert).")]
        [Input("Entfernen")]
        public void Remove(Signal s)
        {
            var bag = Valid(); if (bag == null) return;
            if (bag.Count(item) < count) { Blocked($"nur {bag.Count(item)}× {item} da, {count} gebraucht"); return; }
            bag.Remove(item, count);
            Done($"{item}: {bag.Count(item)}");
            removed.Fire(s);
        }

        [Hint("Sind mindestens Anzahl Stück da? Weiter zu „Hat es“ oder „Hat es nicht“.")]
        [Input("Prüfen")]
        public void Check(Signal s)
        {
            var bag = Valid(); if (bag == null) return;
            bool yes = bag.Count(item) >= count;
            var chosen = yes ? has : hasNot;
            string why = $"{item}: {bag.Count(item)} (gebraucht {count}) in {Where(bag)}";
            if (!chosen.IsConnected) { Blocked($"{(yes ? "Hat es" : "Hat es nicht")} ist nicht angeschlossen – {why}"); return; }
            Done($"→ {(yes ? "hat es" : "hat es nicht")}, {why}");
            chosen.Fire(s);
        }

        // Anzahl unter 1 ergibt keinen Sinn: Entfernen von -3 würde 3 Stück erzeugen.
        KitBag Valid()
        {
            string problem = Problem();
            if (problem == null)
            {
                var bag = Peek();
                if (bag != null) return bag;
                problem = m_Wiring == KitRefs.Wiring.None ? "kein Inventar: ◆ Inventar verbinden oder einen Namen eintragen" : Missing();
            }
            Fail(problem);
            return null;
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

        public override string Readout()
        {
            var bag = Application.isPlaying ? Peek() : null;
            return bag != null ? $"{item}: {bag.Count(item)} · {Where(bag)}" : null;
        }
    }
}
