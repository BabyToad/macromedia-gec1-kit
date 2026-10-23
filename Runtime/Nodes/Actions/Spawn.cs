using System;
using UnityEngine;

namespace Kit
{
    // Erzeugen: legt eine Kopie (Instanz) eines Prefabs an. Das neue Objekt reist mit dem Ereignis
    // weiter (◆ Neues Objekt) – ein späteres Erzeugen überschreibt es nicht.
    [Serializable]
    [NodeInfo("Erzeugen", KitGroups.Aktion, "Prefab-Instanz anlegen")]
    public class Spawn : KitNode
    {
        [Hint("Was erzeugt wird: ein Prefab aus dem Project-Fenster.", edges: "Leer: Fehler.")]
        [Setting("Prefab")] public GameObject prefab;
        [Hint("Wo das neue Objekt erscheint, mit Position und Drehung dieses Objekts. Ein leeres GameObject reicht; das neue wird nicht sein Kind.")]
        [Ref("Ort")] public Transform at;
        [Hint("Nach wie vielen Sekunden das neue Objekt wieder verschwindet.", unit: "s", range: "1 bis 30", edges: "0 oder negativ: bleibt für immer.")]
        [Setting("Lebensdauer")] public float lifetime = 0f;

        [Hint("Feuert, sobald das Objekt da ist. ◆ Neues Objekt gehört zu diesem Ereignis.")]
        [Output("Erzeugt")] [NonSerialized] public Output spawned;
        [Hint("Das gerade erzeugte Objekt. Ein späteres Erzeugen überschreibt es nicht.")]
        [RefOut("Neues Objekt")] [NonSerialized] public GameObject created;

        [Hint("Legt eine neue Kopie des Prefabs am Ort an.")]
        [Input("Erzeugen")]
        public void Create(Signal s)
        {
            if (prefab == null) { Fail("kein Prefab eingestellt"); return; }
            created = UnityEngine.Object.Instantiate(prefab, at.position, at.rotation);
            if (lifetime > 0) UnityEngine.Object.Destroy(created, lifetime);
            Done($"✓ {created.name}");
            spawned.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (prefab == null) problems.Add("kein Prefab eingestellt");
        }
    }
}
