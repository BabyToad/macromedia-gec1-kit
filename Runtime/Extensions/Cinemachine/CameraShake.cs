using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    // Kamera wackeln: löst einen Cinemachine-Impuls aus (Bildschirmwackeln). Form und Stärke stellt die
    // Impulsquelle ein; sichtbar wird es nur an Kameras mit CinemachineImpulseListener.
    [Serializable]
    [NodeInfo("Kamera wackeln (Cinemachine)", "Erweiterung", "Cinemachine-Impuls auslösen")]
    public class CameraShake : KitNode
    {
        [Ref("Impulsquelle")] public CinemachineImpulseSource source;
        [Setting("Stärke")] public float strength = 1f;

        [Output("Ausgelöst")] [NonSerialized] public Output fired;

        [Input("Auslösen")]
        public void Shake(Signal s)
        {
            source.GenerateImpulse(strength);
            Done("✓ Impuls");
            fired.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (UnityEngine.Object.FindAnyObjectByType<CinemachineImpulseListener>() == null)
                problems.Add("Keine Kamera hat einen CinemachineImpulseListener: das Wackeln wäre unsichtbar");
        }
    }
}
