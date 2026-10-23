using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Zone: feuert, wenn etwas einen Trigger-Collider betritt oder verlässt.
    [Serializable]
    [NodeInfo("Zone", KitGroups.Ausloeser, "Wenn etwas die Zone betritt oder verlässt")]
    public class Zone : KitNode
    {
        [Hint("Der Bereich, der beobachtet wird. Braucht einen Collider mit „Is Trigger“. Was hineinkommt, braucht einen Rigidbody oder CharacterController (der Spieler hat einen).")]
        [Ref("Zone", Permanent = true)] public Collider zone;                   // ◆ ein Collider mit „Is Trigger“
        [Hint("Nur Objekte mit diesem Tag zählen (am Objekt mit dem Rigidbody oder am Collider selbst).", edges: "Leer: alles zählt, auch eine rollende Kiste.")]
        [Setting("Nur Tag")] public string requiredTag = "Player";  // leer = alles zählt

        [Hint("Feuert, sobald etwas mit dem passenden Tag die Zone betritt. ◆ Wer ist dann dieses Objekt.")]
        [Output("Betreten")] [NonSerialized] public Output entered;
        [Hint("Feuert, sobald etwas mit dem passenden Tag die Zone verlässt. ◆ Wer ist dann dieses Objekt. Wird es drinnen ausgeschaltet oder gelöscht, feuert es nicht.")]
        [Output("Verlassen")] [NonSerialized] public Output exited;
        [Hint("Was die Zone betreten oder verlassen hat: das Objekt mit dem Rigidbody, sonst das mit dem Collider.")]
        [RefOut("Wer")] [NonSerialized] public GameObject who;      // gehört zum jeweiligen Ereignis

        // Die Rolle auf dem Zonen-Objekt meldet, was Unity dort spürt, und weiß, welche Collider gerade drinstecken:
        // ein Objekt aus mehreren Collidern (Körper + Füße) betritt die Zone einmal, beim ersten, und verlässt sie
        // einmal, beim letzten. Sie zählt auch weiter, während diese Logik aus ist.
        Role relay;

        public override void OnStart()
        {
            relay = Role.Of(zone.gameObject);
            relay.TriggerEnter += OnEnter;      // Unity meldet die Berührung, wir hören zu
            relay.TriggerExit += OnExit;
        }

        public override void OnStop()
        {
            if (relay == null) return;          // == null: auch wenn die Zone gelöscht wurde
            relay.TriggerEnter -= OnEnter;
            relay.TriggerExit -= OnExit;
        }

        // Wer zählt: das Objekt mit dem Rigidbody (der Spieler als Ganzes), sonst das mit dem Collider.
        static GameObject Owner(Collider c) => Role.OwnerOf(c);

        // Der Tag darf am Ganzen oder am Collider selbst sitzen.
        bool Counts(Collider other) => string.IsNullOrEmpty(requiredTag) || Owner(other).CompareTag(requiredTag) || other.CompareTag(requiredTag);

        void OnEnter(Collider other)
        {
            if (!Counts(other)) return;
            var owner = Owner(other);
            if (relay.ContactsOf(owner, Counts) > 1) return;   // ein weiterer passender Collider desselben Objekts: schon drin
            who = owner;
            Done("✓ Betreten: " + who.name);
            entered.Fire(new Signal());          // ein neues Ereignis beginnt hier
        }

        void OnExit(Collider other)
        {
            if (!Counts(other)) return;
            var owner = Owner(other);
            if (relay.ContactsOf(owner, Counts) > 0) return;   // ein anderer passender Collider steckt noch drin
            who = owner;
            Done("✓ Verlassen: " + who.name);
            exited.Fire(new Signal());
        }

        public override void Validate(List<string> problems)
        {
            if (zone != null && !zone.isTrigger)
                problems.Add($"„{zone.name}“: Collider ist kein Trigger (Is Trigger anhaken)");
        }
    }
}
