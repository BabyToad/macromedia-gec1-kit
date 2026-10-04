using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Zone: feuert, wenn etwas einen Trigger-Collider betritt oder verlässt.
    [Serializable]
    [NodeInfo("Zone", "Auslöser", "Wenn etwas die Zone betritt oder verlässt")]
    public class Zone : KitNode
    {
        [Ref("Zone", Permanent = true)] public Collider zone;                   // ◆ ein Collider mit „Is Trigger“
        [Setting("Nur Tag")] public string requiredTag = "Player";  // leer = alles zählt

        [Output("Betreten")] [NonSerialized] public Output entered;
        [Output("Verlassen")] [NonSerialized] public Output exited;
        [RefOut("Wer")] [NonSerialized] public GameObject who;      // gehört zum jeweiligen Ereignis

        KitPhysicsRelay relay;

        public override void OnStart()
        {
            relay = KitPhysicsRelay.On(zone.gameObject);
            relay.TriggerEnter += OnEnter;      // Unity meldet die Berührung, wir hören zu
            relay.TriggerExit += OnExit;
        }

        public override void OnStop()
        {
            if (relay == null) return;          // == null: auch wenn die Zone gelöscht wurde
            relay.TriggerEnter -= OnEnter;
            relay.TriggerExit -= OnExit;
        }

        bool Counts(Collider other) => string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);

        void OnEnter(Collider other)
        {
            if (!Counts(other)) return;
            who = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
            Done("✓ Betreten: " + who.name);
            entered.Fire(new Signal());          // ein neues Ereignis beginnt hier
        }

        void OnExit(Collider other)
        {
            if (!Counts(other)) return;
            who = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
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
