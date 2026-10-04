using System;
using UnityEngine;

namespace Kit
{
    // Erzeugen: legt eine Kopie (Instanz) eines Prefabs an. Das neue Objekt reist mit dem Ereignis
    // weiter (◆ Neues Objekt) – ein späteres Erzeugen überschreibt es nicht.
    [Serializable]
    [NodeInfo("Erzeugen", "Aktion", "Prefab-Instanz anlegen")]
    public class Spawn : KitNode
    {
        [Setting("Prefab")] public GameObject prefab;
        [Ref("Ort")] public Transform at;
        [Setting("Lebensdauer (s, 0 = immer)")] public float lifetime = 0f;

        [Output("Erzeugt")] [NonSerialized] public Output spawned;
        [RefOut("Neues Objekt")] [NonSerialized] public GameObject created;

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
