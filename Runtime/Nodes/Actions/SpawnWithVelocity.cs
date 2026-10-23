using System;
using UnityEngine;

namespace Kit
{
    // Erzeugen mit Schwung: legt eine Prefab-Instanz an und gibt ihr gleich eine Geschwindigkeit mit – ein Ball,
    // der aus einer Kanone rollt, ein Funke, ein Samenkorn, ein geworfener Schneeball. Hat das Prefab keinen
    // Rigidbody, bekommt die Instanz einen (mit Schwerkraft, wenn eingestellt). Das neue Objekt reist mit dem
    // Ereignis weiter (◆ Neues Objekt), z.B. zu „Kurz aktivieren“ oder einer Variable.
    [Serializable]
    [NodeInfo("Erzeugen mit Schwung", "Aktion", "Prefab-Instanz anlegen und losschicken")]
    public class SpawnWithVelocity : KitNode
    {
        [Hint("Was erzeugt wird: ein Prefab aus dem Project-Fenster.")]
        [Setting("Prefab")] public GameObject prefab;
        [Hint("Wo es erscheint und wohin „vorn“ zeigt (die blaue Achse dieses Objekts). Oft ein leeres Kind-Objekt vor einer Figur.")]
        [Ref("Ort")] public Transform at;
        [Hint("Richtung, bezogen auf den Ort: (0, 0, 1) = nach vorn, (0, 1, 1) = schräg nach oben vorn.")]
        [Setting("Richtung")] public Vector3 direction = Vector3.forward;
        [Hint("Wie schnell es losfliegt. 10 ist ein kräftiger Wurf.", unit: "m/s", range: "2 bis 30", edges: "0: es fällt nur herunter. Sehr groß: es fliegt durch dünne Wände.")]
        [DataIn("Tempo")] public float speed = 10f;
        [Hint("An: es fällt wie ein geworfener Gegenstand. Aus: es fliegt gerade weiter, bis es etwas trifft. Gilt nur, wenn das Prefab keinen eigenen Rigidbody hat.")]
        [Setting("Schwerkraft")] public bool gravity = true;
        [Hint("Nach dieser Zeit verschwindet es wieder.", unit: "s", range: "1 bis 10", edges: "0: es bleibt für immer (viele Kopien kosten Leistung).")]
        [Setting("Lebensdauer")] public float lifetime = 5f;

        [Hint("Feuert, sobald das Objekt unterwegs ist.")]
        [Output("Erzeugt")] [NonSerialized] public Output spawned;
        [Hint("Das neue Objekt, für die Knoten nach „Erzeugt“.")]
        [RefOut("Neues Objekt")] [NonSerialized] public GameObject created;

        [Hint("Eine Instanz anlegen und mit Tempo in Richtung losschicken.")]
        [Input("Erzeugen")]
        public void Create(Signal s)
        {
            if (prefab == null) { Fail("kein Prefab eingestellt"); return; }
            var dir = at.TransformDirection(direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward);
            created = UnityEngine.Object.Instantiate(prefab, at.position, Quaternion.LookRotation(dir, at.up));
            var body = created.GetComponent<Rigidbody>();
            if (body == null) { body = created.AddComponent<Rigidbody>(); body.useGravity = gravity; body.interpolation = RigidbodyInterpolation.Interpolate; }
            if (body.isKinematic) { Fail($"„{prefab.name}“ hat einen kinematischen Rigidbody: Schwung wirkt nicht"); UnityEngine.Object.Destroy(created); return; }
            if (created.GetComponentInChildren<Collider>() == null) Debug.LogWarning($"[Kit] Erzeugen mit Schwung: „{prefab.name}“ hat keinen Collider und fällt durch den Boden.", created);
            body.linearVelocity = dir * speed;
            if (lifetime > 0) UnityEngine.Object.Destroy(created, lifetime);
            Done($"✓ {created.name}, {KitValue.FormatNumber(speed)} m/s");
            spawned.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (prefab == null) { problems.Add("kein Prefab eingestellt: ein Prefab aus dem Project-Fenster auf das Feld ziehen"); return; }
            if (!prefab.activeSelf) problems.Add($"„{prefab.name}“ ist ausgeschaltet: jede Kopie wäre unsichtbar (im Prefab das Häkchen oben im Inspector setzen)");
            if (prefab.GetComponentInChildren<Collider>(true) == null) problems.Add($"„{prefab.name}“ hat keinen Collider: es fällt durch den Boden (Add Component › Box Collider)");
            var body = prefab.GetComponent<Rigidbody>();
            if (body != null && body.isKinematic) problems.Add($"„{prefab.name}“ hat einen kinematischen Rigidbody: Schwung wirkt nicht (Is Kinematic ausschalten)");
        }
    }
}
