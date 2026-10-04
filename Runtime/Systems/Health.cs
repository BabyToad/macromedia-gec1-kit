using System;

namespace Kit
{
    // Lebenspunkte: Schaden, Heilen, Wiederbeleben – an einem Objekt mit der Komponente „Lebenspunkte“.
    // Das Objekt kann fest gebunden sein oder mit dem Ereignis kommen (z.B. ◆ Wer aus einer Zone).
    // "Tot" feuert genau einmal, wenn die Punkte auf 0 fallen. Schaden an einem Toten wird blockiert.
    [Serializable]
    [NodeInfo("Lebenspunkte", "Spielsysteme", "Schaden, Heilen, Tot")]
    public class Health : KitNode
    {
        [Ref("Wer")] public KitHealth target;
        [DataIn("Menge")] public float amount = 10f;

        [Output("Getroffen")] [NonSerialized] public Output hurt;
        [Output("Geheilt")] [NonSerialized] public Output healed;
        [Output("Tot")] [NonSerialized] public Output died;
        [DataOut("Punkte", EventLocal = true)] [NonSerialized] public float points;

        [Input("Schaden")]
        public void Damage(Signal s)
        {
            if (target.Dead) { Blocked($"„{target.name}“ ist schon tot"); return; }
            float changed = target.Change(-Math.Abs(amount));
            points = target.Current;
            Done($"−{-changed:0.#} → {points:0.#} / {target.max:0.#}");
            hurt.Fire(s);
            if (target.Dead) died.Fire(s);
        }

        [Input("Heilen")]
        public void Heal(Signal s)
        {
            if (target.Dead) { Blocked($"„{target.name}“ ist tot – erst wiederbeleben"); return; }
            float changed = target.Change(Math.Abs(amount));
            points = target.Current;
            Done($"+{changed:0.#} → {points:0.#} / {target.max:0.#}");
            healed.Fire(s);
        }

        [Input("Wiederbeleben")]
        public void Revive(Signal s)
        {
            target.ResetToStart();
            points = target.Current;
            Done($"wiederbelebt: {points:0.#}");
            healed.Fire(s);
        }

        public override string Readout() => target != null ? $"{target.name}: {target.Current:0.#} / {target.max:0.#}" : null;
    }
}
