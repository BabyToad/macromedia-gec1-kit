using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Eingang: ein Ankunftspunkt in einer Szene. Kommt der Spieler aus einer anderen Szene, steht er hier – mit den
    // Füßen auf diesem Punkt, mit Blick in die blaue Pfeilrichtung (die Z-Achse des Objekts). Danach ist der Eingang
    // auch sein Zurücksetz-Punkt (Herunterfallen, Checkpoint „Zurücksetzen“), bis ein Checkpoint etwas anderes merkt.
    //
    // Der Name des Eingangs ist der Name des Objekts in der Hierarchy. Welcher Eingang gilt:
    //  1. Nennt „Szene laden“ einen Eingang, dieser.
    //  2. Sonst der Eingang, der so heißt wie die Szene, aus der der Spieler kommt (Tür im Keller → Eingang „Keller“
    //     im Hof). So passen Hin- und Rückweg ohne Eintippen zusammen.
    //  3. Sonst der Eingang mit Haken bei „Standard“.
    //  4. Sonst bleibt der Spieler dort, wo er in der Szene steht.
    [AddComponentMenu(KitBrand.ComponentMenu + "Eingang")]
    [DisallowMultipleComponent]
    public class KitEntrance : MonoBehaviour
    {
        [Hint("Hier kommt der Spieler an, wenn „Szene laden“ keinen Eingang nennt und kein Eingang so heißt wie die " +
              "Szene, aus der er kommt. Höchstens ein Eingang pro Szene sollte Standard sein.", label: "Standard")]
        public bool isDefault;

        /// <summary>Name des Eingangs = Name des Objekts.</summary>
        public string Name => name;

        /// <summary>Der Spieler ist gerade hier angekommen (nach dem Laden der Szene).</summary>
        public event Action<KitPlayer> PlayerArrived;

        static readonly List<KitEntrance> s_All = new List<KitEntrance>();

        /// <summary>Alle eingeschalteten Eingänge der geladenen Szenen.</summary>
        public static IReadOnlyList<KitEntrance> All => s_All;

        void OnEnable() { if (!s_All.Contains(this)) s_All.Add(this); }
        void OnDisable() => s_All.Remove(this);

        public static KitEntrance Find(string entranceName) =>
            string.IsNullOrEmpty(entranceName) ? null : s_All.Find(e => e && e.Name == entranceName);

        /// <summary>Stellt den Spieler auf diesen Eingang, Blick in Pfeilrichtung, und merkt ihn als Zurücksetz-Punkt.</summary>
        public void Receive(KitPlayer player)
        {
            var rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);   // aufrecht, nur die Blickrichtung zählt
            player.Teleport(transform.position, rotation);
            player.SetRespawnPoint(transform.position, rotation);
            PlayerArrived?.Invoke(player);
        }

        // ---------------------------------------------------------------------------------- arrival
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            s_All.Clear();
            KitScenes.PlacePlayer = Place;
        }

        /// <summary>Applies the rule above after a kit scene change. Returns what happened, in German.</summary>
        internal static string Place(string entrance, string cameFrom)
        {
            KitEntrance target;
            string rule;
            if (!string.IsNullOrEmpty(entrance))
            {
                target = Find(entrance);
                if (!target)
                {
                    var msg = $"kein Eingang „{entrance}“ in dieser Szene – Spieler bleibt, wo er steht";
                    Debug.LogWarning(KitBrand.Log + "Szene laden: " + msg + Available());
                    return msg;
                }
                rule = "genannt";
            }
            else if ((target = Find(cameFrom)) != null) rule = $"heißt wie die Szene „{cameFrom}“";
            else if ((target = s_All.Find(e => e && e.isDefault)) != null) rule = "Standard";
            else return "kein passender Eingang – Spieler bleibt, wo er steht";

            var player = FindAnyObjectByType<KitPlayer>();
            if (!player)
            {
                Debug.LogWarning(KitBrand.Log + $"Eingang „{target.Name}“: in dieser Szene gibt es keinen Spieler zum Hinstellen.", target);
                return $"Eingang „{target.Name}“, aber kein Spieler in der Szene";
            }
            target.Receive(player);
            return $"am Eingang „{target.Name}“ ({rule})";
        }

        static string Available()
        {
            if (s_All.Count == 0) return " (diese Szene hat keine Eingänge)";
            var names = new List<string>();
            foreach (var e in s_All) if (e) names.Add($"„{e.Name}“");
            names.Sort(System.StringComparer.CurrentCulture);
            return $" (vorhanden: {string.Join(", ", names)})";
        }

#if UNITY_EDITOR
        // A standing figure and its facing, so the entrance can be placed without pressing Play.
        void OnDrawGizmos()
        {
            var rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Gizmos.color = isDefault ? new Color(0.35f, 0.85f, 0.75f) : new Color(0.45f, 0.75f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, rot, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0, 1f, 0), new Vector3(0.8f, 2f, 0.8f));
            Gizmos.DrawLine(new Vector3(0, 0.05f, 0), new Vector3(0, 0.05f, 1.2f));
            Gizmos.DrawLine(new Vector3(0, 0.05f, 1.2f), new Vector3(-0.25f, 0.05f, 0.9f));
            Gizmos.DrawLine(new Vector3(0, 0.05f, 1.2f), new Vector3(0.25f, 0.05f, 0.9f));
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
