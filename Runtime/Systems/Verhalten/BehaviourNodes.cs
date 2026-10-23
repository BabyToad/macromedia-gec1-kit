using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>Which behaviour the Figur trigger listens during. <see cref="Immer"/> = no matter which.</summary>
    public enum BehaviourFilter { Immer, Stehen, Patrouillieren, Umherstreifen, Wachen, Folgen, Fliehen, Zurückkehren, Nachsehen }

    // Figur: what a figure with Verhalten notices and reaches. With „Nur während“ one node per behaviour
    // collects that behaviour's ways out: the graph becomes a state machine without anyone saying the word.
    [Serializable]
    [NodeInfo("Figur", "Verhalten", "Was eine Figur bemerkt, erreicht oder berührt")]
    [Hint("Meldet, was eine Figur mit Verhalten wahrnimmt und erreicht. Mit „Nur während“ gehört ein Knoten zu einem Verhalten: " +
          "ein Figur-Knoten für Patrouillieren, einer für Folgen … So steht zusammen, was in welchem Verhalten passiert.")]
    public class NpcEvents : KitNode
    {
        [Ref("Figur", Permanent = true)]
        [Hint("Die Figur mit Verhalten (" + NpcBehaviour.MenuPath + "…). Eine Figur nur mit Wahrnehmung geht auch: eine Kamera, eine Statue.")]
        public GameObject figure;

        [Setting("Nur während")]
        [Hint("Meldet nur, solange die Figur dieses Verhalten hat.", edges: "Immer: egal, welches Verhalten gerade läuft.")]
        public BehaviourFilter during = BehaviourFilter.Immer;

        [Output("Sieht Spieler")] [NonSerialized]
        [Hint("Die Figur hat den Spieler gesehen: er war lange genug im Sichtkegel, und nichts war im Weg. Einmal pro Bemerken.")]
        public Output saw;
        [Output("Hört Spieler")] [NonSerialized]
        [Hint("Die Figur hat die Schritte des Spielers gehört (Wahrnehmung › Hören). Einmal pro Bemerken.")]
        public Output heard;
        [Output("Hört Geräusch")] [NonSerialized]
        [Hint("Ein Geräusch-Knoten war in Hörweite. Die Figur merkt sich den Ort; „Nachsehen“ geht dorthin. Den Spieler bemerkt sie dadurch nicht.")]
        public Output heardNoise;
        [Output("Verliert Spieler")] [NonSerialized]
        [Hint("Die Figur hat den Spieler so lange weder gesehen noch gehört, wie „Gedächtnis“ es erlaubt, und gibt auf.")]
        public Output lost;
        [Output("Ziel erreicht")] [NonSerialized]
        [Hint("Das laufende Verhalten ist angekommen: Wegpunkt, Posten, Zuhause, eingeholt, in Sicherheit, nachgesehen.")]
        public Output arrived;
        [Output("Berührt")] [NonSerialized]
        [Hint("Die Figur und der Spieler berühren sich (Wahrnehmung › Berühren). Einmal, bis sie sich wieder trennen.")]
        public Output touched;

        [RefOut("Wer")] [NonSerialized]
        [Hint("Wen die Figur wahrnimmt, meist den Spieler. Leer bei „Ziel erreicht“ und „Hört Geräusch“.")]
        public GameObject who;
        [DataOut("Wegpunkt", EventLocal = true)] [NonSerialized]
        [Hint("Bei „Ziel erreicht“ während Patrouillieren: die Nummer des erreichten Wegpunkts, 1, 2, 3 … Sonst 0.", unit: "Nummer")]
        public float waypoint;
        [DataOut("Abstand")]
        [Hint("Wie weit der Spieler gerade von der Figur entfernt ist.", unit: "m", edges: "-1: kein Spieler in der Szene.")]
        public float Distance => m_Senses != null && m_Senses.Target != null ? Vector3.Distance(m_Senses.transform.position, m_Senses.Target.position) : -1f;
        [DataOut("Verhalten")]
        [Hint("Name des Verhaltens, das gerade läuft, z. B. „Patrouillieren“.")]
        public string Current => m_Senses != null ? NpcBehaviour.NameOf(m_Senses.CurrentKind) : "";

        Senses m_Senses;

        public override void OnStart()
        {
            if (!figure.TryGetComponent(out m_Senses))
            {
                Fail($"„{figure.name}“ hat keine Wahrnehmung: {NpcBehaviour.MenuPath}…");
                return;
            }
            m_Senses.Reported += OnReported;
        }

        public override void OnStop()
        {
            if (m_Senses == null) return;   // also when the figure was deleted
            m_Senses.Reported -= OnReported;
            m_Senses = null;
        }

        // BehaviourFilter is Immer followed by BehaviourKind in the same order.
        bool Now(BehaviourKind kind) => during == BehaviourFilter.Immer || (int)during - 1 == (int)kind;

        string During => during == BehaviourFilter.Immer ? "" : $" (beim {during})";

        void OnReported(FigureEvent e)
        {
            // An earlier listener of the same event may have stopped this graph (OnStop ran mid-dispatch).
            if (m_Senses == null || e.figure != m_Senses || !Now(e.during)) return;
            who = e.about != null ? e.about.gameObject : null;
            waypoint = e.waypoint;
            Output o; string what;
            switch (e.what)
            {
                case FigureEvent.Kind.Sieht: o = saw; what = "Sieht Spieler"; break;
                case FigureEvent.Kind.Hört: o = heard; what = "Hört Spieler"; break;
                case FigureEvent.Kind.Geräusch: o = heardNoise; what = "Hört Geräusch"; break;
                case FigureEvent.Kind.Verliert: o = lost; what = "Verliert Spieler"; break;
                case FigureEvent.Kind.Berührt: o = touched; what = "Berührt"; break;
                default: o = arrived; what = e.waypoint > 0 ? $"Ziel erreicht: Wegpunkt {e.waypoint}" : "Ziel erreicht"; break;
            }
            Done("✓ " + what + During);
            o.Fire(new Signal());   // a new event starts here
        }

        public override string Readout() =>
            m_Senses == null ? null : $"{NpcBehaviour.NameOf(m_Senses.CurrentKind)} · {m_Senses.Describe()}";
    }

    // Verhalten wechseln: schaltet eine Figur auf ein anderes Verhalten. Das alte hört sofort auf.
    [Serializable]
    [NodeInfo("Verhalten wechseln", "Verhalten", "Figur patrouilliert, folgt, flieht … ab jetzt")]
    [Hint("Schaltet die Figur auf ein anderes Verhalten; das bisherige hört sofort auf. Im Inspector wandert das Häkchen mit.")]
    public class SwitchBehaviour : KitNode
    {
        [Ref("Figur")]
        [Hint("Die Figur, die ab jetzt etwas anderes tut.")]
        public GameObject figure;

        [Setting("Zu")]
        [Hint("Das neue Verhalten. Fehlt es der Figur, wird es beim Wechsel mit Standardwerten ergänzt.",
              edges: "Stehen: alle Verhalten aus, die Figur bleibt stehen. Ihre Wahrnehmung arbeitet weiter.")]
        public BehaviourKind to = BehaviourKind.Folgen;

        [Setting("Name")]
        [Hint("Nur nötig, wenn die Figur dieses Verhalten mehrmals hat: das Feld „Name“ des gewünschten.", edges: "Leer: das erste dieser Art.")]
        public string label = "";

        [Input("Wechseln")]
        [Hint("Jetzt wechseln.")]
        public void Switch(Signal s)
        {
            var before = figure.TryGetComponent(out Senses senses) ? senses.CurrentKind : BehaviourKind.Stehen;
            NpcBehaviour.Switch(figure, to, label, out var note);
            if (note != null) Debug.LogWarning(KitBrand.Log + note, figure);
            string text = before == to && note == null
                ? $"✓ {figure.name}: war schon {NpcBehaviour.NameOf(to)}"
                : $"✓ {figure.name}: {NpcBehaviour.NameOf(before)} → {NpcBehaviour.NameOf(to)}";
            Done(note != null ? text + " (ergänzt)" : text);
            done.Fire(s);
        }

        [Output("Erledigt")] [NonSerialized]
        [Hint("Der Wechsel ist geschehen.")]
        public Output done;

        public override void Validate(List<string> problems)
        {
            if (figure == null || to == BehaviourKind.Stehen) return;
            if (figure.GetComponent(NpcBehaviour.TypeOf(to)) == null)
                problems.Add($"„{figure.name}“ hat kein {NpcBehaviour.NameOf(to)}: wird beim Wechsel mit Standardwerten ergänzt " +
                             $"({NpcBehaviour.MenuPath}{NpcBehaviour.NameOf(to)}, um es einzustellen)");
        }
    }

    // Geräusch: ein Laut an einem Ort. Figuren mit Gehör in Reichweite hören ihn („Hört Geräusch“) und
    // merken sich den Ort; „Nachsehen“ geht dorthin. Ein geworfener Stein, eine Klingel, ein Alarm.
    [Serializable]
    [NodeInfo("Geräusch", "Verhalten", "Ein Laut, den Figuren mit Gehör hören")]
    [Hint("Ein Laut an einem Ort, für Figuren mit Gehör: ein geworfener Stein lenkt eine Wache ab, eine Glocke ruft alle herbei. " +
          "Zu hören ist er nicht; dafür „Ton abspielen“ dazunehmen.")]
    public class MakeNoise : KitNode
    {
        [Ref("Wo")]
        [Hint("Wo das Geräusch entsteht.")]
        public Transform where;

        [Setting("Reichweite")]
        [Hint("Wie weit das Geräusch trägt. Figuren mit Gehör innerhalb dieser Entfernung hören es; ihre eigene Hörweite zählt dabei nicht.",
              unit: "m", range: "3 bis 30", edges: "Ein Stein: 8 m. Eine Glocke: 30 m.")]
        public float range = 8f;

        [Input("Auslösen")]
        [Hint("Jetzt das Geräusch machen.")]
        public void Make(Signal s)
        {
            heardBy = Senses.MakeNoise(where.position, range);
            Done(heardBy == 1 ? "✓ 1 Figur hört es" : $"✓ {heardBy:0} Figuren hören es");
            done.Fire(s);
        }

        [Output("Erledigt")] [NonSerialized]
        [Hint("Das Geräusch ist gemacht.")]
        public Output done;

        [DataOut("Gehört von", EventLocal = true)] [NonSerialized]
        [Hint("Wie viele Figuren das Geräusch gehört haben.", unit: "Stück")]
        public float heardBy;
    }
}
