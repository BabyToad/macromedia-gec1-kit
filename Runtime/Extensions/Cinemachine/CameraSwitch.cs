using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    // Kamera wechseln: hebt eine Cinemachine-Kamera über alle anderen (Aktivieren) oder senkt sie wieder
    // (Deaktivieren). Der CinemachineBrain an der Hauptkamera blendet dann zur Kamera mit der höchsten Priorität.
    [Serializable]
    [NodeInfo("Kamera wechseln", "Erweiterung", "Cinemachine-Kamera aktivieren")]
    public class CameraSwitch : KitNode
    {
        [Ref("Kamera")] public CinemachineCamera cam;
        [Setting("Priorität aktiv")] public int activePriority = 100;

        [Output("Gewechselt")] [NonSerialized] public Output switched;

        // Priorität vor dem Aktivieren, pro Kamera (die Kamera kann auch mit dem Ereignis kommen).
        System.Collections.Generic.Dictionary<CinemachineCamera, int> normal;

        public override void OnStart() => normal = new System.Collections.Generic.Dictionary<CinemachineCamera, int>();

        [Input("Aktivieren")]
        public void On(Signal s)
        {
            if (!normal.ContainsKey(cam)) normal[cam] = cam.Priority;
            cam.Priority = activePriority; Done("✓ aktiv"); switched.Fire(s);
        }

        [Input("Deaktivieren")]
        public void Off(Signal s)
        {
            if (!normal.TryGetValue(cam, out var p)) { Done($"„{cam.name}“ war nicht aktiviert"); return; }
            cam.Priority = p; normal.Remove(cam); Done("✓ zurück"); switched.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
                problems.Add("Die Hauptkamera hat keinen CinemachineBrain: der Wechsel wäre unsichtbar");
        }
    }
}
