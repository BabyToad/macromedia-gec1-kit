using System;

namespace Kit
{
    // Dialog: zeigt eine Zeile im Dialogfenster und wartet. Ohne Antworten geht es mit "Weiter" weiter,
    // mit Antworten über A, B oder C. Kommt eine andere Zeile dazwischen, feuert "Abgebrochen".
    // Optional hält der Spieler so lange an.
    // Jede gezeigte Zeile ist ein eigener Vorgang: mit eigenem Fenster, eigenem Spieler, eigener Nummer.
    // Aufräumen betrifft nur den eigenen Vorgang – nie die Zeile einer anderen Interaktion.
    [Serializable]
    [NodeInfo("Dialog", "Spielsysteme", "Zeile zeigen, auf Antwort warten")]
    public class Dialog : KitNode
    {
        [Ref("Fenster")] public KitDialogBox box;
        [Ref("Spieler anhalten", Optional = true)] public KitPlayer player;
        [Setting("Sprecher")] public string speaker = "Wache";
        [Setting("Text")] public string text = "Halt! Wer da?";
        [Setting("Antwort A")] public string answerA = "";
        [Setting("Antwort B")] public string answerB = "";
        [Setting("Antwort C")] public string answerC = "";

        [Output("Weiter")] [NonSerialized] public Output next;
        [Output("A")] [NonSerialized] public Output a;
        [Output("B")] [NonSerialized] public Output b;
        [Output("C")] [NonSerialized] public Output c;
        [Output("Abgebrochen")] [NonSerialized] public Output cancelled;

        // Der laufende Vorgang dieses Knotens (null = keiner).
        class Op { public KitDialogBox box; public int token; public KitPlayer player; public Signal signal; public string[] choices; public bool lost; }
        Op current;

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
            if (current != op)                              // schon während des Zeigens beendet (verdrängt oder Interaktion gestoppt)
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
        // aber nur, wenn die Interaktion dann noch läuft (beim Beenden von Play oder Szenenwechsel bleibt es still).
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
