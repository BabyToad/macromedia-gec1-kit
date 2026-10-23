using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// The Inspector side of Atmosphäre: every zone says what it needs before it can work (with a button that does
    /// it), and in Play what it is doing right now. Musik shows its layers as live level bars.
    /// </summary>
    public static class ZoneInspector
    {
        /// <summary>Setup problems with a fix button, then the live state. Call at the top of a zone's Inspector.</summary>
        public static void Status(AtmosphereZone z)
        {
            var c = z.GetComponent<Collider>();
            if (!c)
            {
                EditorGUILayout.HelpBox("Die Zone hat noch keine Form: sie braucht einen Collider, der ihren Raum absteckt.", MessageType.Error);
                if (GUILayout.Button("Box-Collider hinzufügen"))
                {
                    var box = Undo.AddComponent<BoxCollider>(z.gameObject);
                    box.isTrigger = true;
                }
            }
            else if (!c.isTrigger)
            {
                EditorGUILayout.HelpBox($"„{c.GetType().Name}“ ist fest: der Spieler würde dagegen laufen statt hinein. Eine Zone braucht „Is Trigger“.", MessageType.Warning);
                if (GUILayout.Button("Zum Trigger machen")) { Undo.RecordObject(c, "Is Trigger"); c.isTrigger = true; }
            }

            if (Application.isPlaying)
            {
                var inside = z.Inside.Where(g => g).Select(g => g.name).ToArray();
                EditorGUILayout.HelpBox(inside.Length > 0 ? "● Gerade in der Zone: " + string.Join(", ", inside) : "○ Niemand in der Zone",
                                        MessageType.None);
            }
            else if (!string.IsNullOrEmpty(z.requiredTag) && !TagExists(z.requiredTag))
            {
                EditorGUILayout.HelpBox($"Den Tag „{z.requiredTag}“ gibt es im Projekt nicht: nichts kann die Zone auslösen. Tag leeren oder anlegen (Tags & Layers).", MessageType.Warning);
            }
        }

        static bool TagExists(string tag) => UnityEditorInternal.InternalEditorUtility.tags.Contains(tag);
    }

    [CustomEditor(typeof(FogZone)), CanEditMultipleObjects]
    class FogZoneEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            ZoneInspector.Status((FogZone)target);
            EditorGUILayout.HelpBox("In der Zone blendet der Nebel der Szene zu dieser Farbe und Sichtweite, draußen zurück.", MessageType.None);
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(MusicZone)), CanEditMultipleObjects]
    class MusicZoneEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            var z = (MusicZone)target;
            ZoneInspector.Status(z);
            DrawDefaultInspector();
            for (int i = 0; i < z.changes.Count; i++)
            {
                var e = z.changes[i];
                if (!e.music)
                {
                    EditorGUILayout.HelpBox($"Eintrag {i + 1}: noch keine Musik. Ein Objekt mit „Musik und Atmo“ auf „Musik“ ziehen.", MessageType.Warning);
                    var found = Object.FindAnyObjectByType<Music>();
                    if (found ? GUILayout.Button($"„{found.name}“ eintragen") : GUILayout.Button("Musik und Atmo anlegen und eintragen"))
                    {
                        Undo.RecordObject(z, "Musik eintragen");
                        if (!found)
                        {
                            var go = new GameObject("Musik");
                            Undo.RegisterCreatedObjectUndo(go, "Musik anlegen");
                            found = Undo.AddComponent<Music>(go);
                            Selection.activeGameObject = go;
                        }
                        e.music = found;
                    }
                }
                else if (!string.IsNullOrEmpty(e.layer) && e.music.IndexOf(e.layer) < 0)
                    EditorGUILayout.HelpBox($"Eintrag {i + 1}: „{e.music.name}“ hat keine Ebene „{e.layer}“. Vorhanden: {string.Join(", ", e.music.layers.Select(l => l.name))}.", MessageType.Error);
            }
        }
    }

    [CustomEditor(typeof(Music))]
    class MusicEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            var m = (Music)target;
            if (m.layers.All(l => !l.clip))
                EditorGUILayout.HelpBox("Noch still: eine Audio-Datei aus dem Project-Fenster auf „Clip“ einer Ebene ziehen.", MessageType.Info);
            var lengths = m.layers.Where(l => l.clip).Select(l => l.clip.length).Distinct().ToArray();
            if (lengths.Length > 1 && lengths.Max() - lengths.Min() > 0.05f)
                EditorGUILayout.HelpBox("Die Ebenen sind unterschiedlich lang (" + string.Join(" / ", m.layers.Where(l => l.clip).Select(l => $"{l.clip.length:0.0} s")) +
                                        "): nach der ersten Runde laufen sie aus dem Takt. Gleich lange Stücke verwenden.", MessageType.Warning);
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Jetzt", EditorStyles.boldLabel);
            Meter("Ganze Musik", m.Level);
            foreach (var l in m.layers) Meter(l.name, Mathf.Max(0f, m.LayerLevel(l.name)));
        }

        static void Meter(string name, float level)
        {
            var r = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(r, level, $"{name}: {level * 100f:0} %");
        }
    }

    [CustomEditor(typeof(DayCycle))]
    class DayCycleEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying && ((DayCycle)target).running;
        public override void OnInspectorGUI()
        {
            var d = (DayCycle)target;
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField($"{DayCycle.Clock(d.hour)} Uhr", style, GUILayout.Height(26));
            DrawDefaultInspector();
            if (RenderSettings.sun != d.GetComponent<Light>() && RenderSettings.sun)
                EditorGUILayout.HelpBox($"Lighting › Sun Source ist „{RenderSettings.sun.name}“; die Tageszeit trägt sich selbst ein, sobald sie läuft.", MessageType.None);
        }
    }
}
