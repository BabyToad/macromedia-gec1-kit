using System;

namespace Kit
{
    // Dialog: zeigt eine Zeile im Dialogfenster und wartet. Ohne Antworten geht es mit "Weiter" weiter,
    // mit Antworten über A, B oder C. Kommt eine andere Zeile dazwischen, feuert "Abgebrochen".
    // Optional hält der Spieler so lange an.
    // Jede gezeigte Zeile ist ein eigener Vorgang: mit eigenem Fenster, eigenem Spieler, eigener Nummer.
    // Aufräumen betrifft nur den eigenen Vorgang – nie die Zeile einer anderen Logik.
    [Serializable]
    [NodeInfo("Dialog", KitGroups.Spielsysteme, "Zeile zeigen, auf Antwort warten")]
    public class Dialog : KitNode
    {
        [Hint("Das Dialogfenster in der Szene (Komponente „Dialogfenster“). Muss eingeschaltet sein.")]
        [Ref("Fenster")] public KitDialogBox box;
        [Hint("Dieser Spieler steht still, solange die Zeile offen ist.", edges: "Leer: der Spieler läuft weiter.")]
        [Ref("Spieler anhalten", Optional = true)] public KitPlayer player;
        [Hint("Der Name über der Zeile.", edges: "Leer: kein Name.")]
        [Setting("Sprecher")] public string speaker = "Wache";
        [Hint("Was gesagt wird.")]
        [Setting("Text")] public string text = "Halt! Wer da?";
        [Hint("Antwort für Taste 1. Sind alle Antworten leer, geht es mit „Weiter“ weiter.")]
        [Setting("Antwort A")] public string answerA = "";
        [Hint("Antwort für Taste 2.", edges: "Leer: keine Antwort B.")]
        [Setting("Antwort B")] public string answerB = "";
        [Hint("Antwort für Taste 3.", edges: "Leer: keine Antwort C.")]
        [Setting("Antwort C")] public string answerC = "";

        [Hint("Feuert, wenn eine Zeile ohne Antworten weitergeklickt wird (E, Leertaste, Enter oder A am Gamepad).")]
        [Output("Weiter")] [NonSerialized] public Output next;
        [Hint("Feuert, wenn Antwort A gewählt wird (Taste 1 oder X am Gamepad).")]
        [Output("A")] [NonSerialized] public Output a;
        [Hint("Feuert, wenn Antwort B gewählt wird (Taste 2 oder Y am Gamepad).")]
        [Output("B")] [NonSerialized] public Output b;
        [Hint("Feuert, wenn Antwort C gewählt wird (Taste 3 oder B am Gamepad).")]
        [Output("C")] [NonSerialized] public Output c;
        [Hint("Feuert, wenn eine andere Zeile diese verdrängt, bevor geantwortet wurde, auch eine neue Zeile dieses Knotens.")]
        [Output("Abgebrochen")] [NonSerialized] public Output cancelled;

        // Der laufende Vorgang dieses Knotens (null = keiner).
        class Op { public KitDialogBox box; public int token; public KitPlayer player; public Signal signal; public string[] choices; public bool lost; }
        Op current;

        [Hint("Zeigt die Zeile und wartet auf Weiter oder eine Antwort. Eine schon offene Zeile wird dabei abgebrochen.")]
        [Input("Zeigen")]
        public void Show(Signal s)
        {
            if (current != null) Finish(current, KitDialogBox.Cancelled);   // eigene alte Zeile zuerst sauber beenden

            if (box == null) { Fail("◆ Fenster fehlt (gelöscht?)"); return; }
            if (!box.isActiveAndEnabled) { Fail($"◆ Fenster „{box.name}“ ist ausgeschaltet"); return; }
            var choices = new[] { answerA, answerB, answerC };
            var op = new Op { box = box, signal = s, choices = choices };
            current = op;                                   // vor dem Zeigen: ein sofortiges Verdrängen trifft diesen Vorgang
            op.token = box.Show(speaker, text, choices, choice => { if (choice == KitDialogBox.Gone) Lost(op); else Finish(op, choice); });   // verdrängt ggf. eine fremde Zeile
            if (current != op)                              // schon während des Zeigens beendet (verdrängt oder Logik gestoppt)
            {
                box.Close(op.token);                        // eigene Zeile zu, falls sie noch offen ist (sonst nichts)
                return;
            }
            if (player) { op.player = player; player.Pause(op); }   // gemeinsame Pause: läuft erst wieder, wenn alle freigeben
            Running($"wartet auf Antwort: „{text}“");
        }

        void Finish(Op op, int choice)
        {
            if (op != current) return;                      // schon beendet
            current = null;
            if (op.box != null) op.box.Close(op.token);     // nur die eigene Zeile, falls noch offen
            if (op.player != null) op.player.Resume(op);
            switch (choice)
            {
                case KitDialogBox.Gone: Fail("◆ Fenster wurde ausgeschaltet oder gelöscht, während die Zeile offen war"); return;
                case KitDialogBox.Cancelled: Done("abgebrochen (andere Zeile)"); cancelled.Fire(op.signal); return;
                case KitDialogBox.Next: Done("✓ weiter"); next.Fire(op.signal); return;
                default:
                    Done($"✓ Antwort {"ABC"[choice]}: {op.choices[choice]}");
                    (choice == 0 ? a : choice == 1 ? b : c).Fire(op.signal);
                    return;
            }
        }

        // Fenster aus oder gelöscht: sofort Spieler freigeben; den Fehler meldet Tick im nächsten Frame –
        // aber nur, wenn die Logik dann noch läuft (beim Beenden von Play oder Szenenwechsel bleibt es still).
        void Lost(Op op)
        {
            if (op != current) return;
            op.lost = true;
            if (op.player != null) op.player.Resume(op);
        }

        public override void Tick()
        {
            // Fenster weg oder aus, ohne dass es sich melden konnte: einmal Fehler, Spieler freigeben.
            if (current != null && (current.lost || current.box == null || !current.box.isActiveAndEnabled)) Finish(current, KitDialogBox.Gone);
        }

        public override void OnStop()
        {
            if (current == null) return;
            var op = current; current = null;
            if (op.box != null) op.box.Close(op.token);     // nur die eigene Zeile
            if (op.player != null) op.player.Resume(op);
        }
    }
}
