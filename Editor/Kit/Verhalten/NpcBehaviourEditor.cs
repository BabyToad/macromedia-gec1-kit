using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Inspector for every Verhalten component: one sentence on what it does, whether it is the starting or
    /// current behaviour, its settings, and in Play a live line of what it is doing. Patrouillieren adds
    /// buttons to lay out its route.
    /// </summary>
    [CustomEditor(typeof(NpcBehaviour), true)]
    [CanEditMultipleObjects]
    public class NpcBehaviourEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        static string Summary(BehaviourKind k)
        {
            switch (k)
            {
                case BehaviourKind.Patrouillieren: return "Geht eine Route aus Wegpunkten ab. „Ziel erreicht“ an jedem Wegpunkt.";
                case BehaviourKind.Umherstreifen: return "Geht zu zufälligen Punkten im Umkreis, mit Pausen. „Ziel erreicht“ an jedem Punkt.";
                case BehaviourKind.Wachen: return "Steht am Posten und schaut sich um. Was sie sieht und hört, steht in Wahrnehmung.";
                case BehaviourKind.Folgen: return "Bleibt in der Nähe des Ziels. „Ziel erreicht“, wenn sie es eingeholt hat.";
                case BehaviourKind.Fliehen: return "Läuft vor der Bedrohung weg. „Ziel erreicht“ beim Sicherheitsabstand.";
                case BehaviourKind.Zurückkehren: return "Geht nach Hause und schaut wie beim Start. „Ziel erreicht“ bei Ankunft.";
                case BehaviourKind.Nachsehen: return "Geht zur letzten Spur (gesehen, gehört, Geräusch) und sieht sich um. Dann „Ziel erreicht“.";
                default: return "";
            }
        }

        public override void OnInspectorGUI()
        {
            var b = (NpcBehaviour)target;
            EditorGUILayout.LabelField(Summary(b.Kind), EditorStyles.wordWrappedMiniLabel);
            Role(b);

            serializedObject.Update();
            VerhaltenFields.Draw(serializedObject);
            serializedObject.ApplyModifiedProperties();

            if (b is Patrol p && targets.Length == 1) RouteTools(p);
            if (!Application.isPlaying && targets.Length == 1) FloorCheck(b);
            if (!Application.isPlaying && targets.Length == 1 && b.GetComponents<NpcBehaviour>().Length > 1 && b.enabled)
                EditorGUILayout.LabelField($"Im Logik-Graph: Auslöser „Figur“ meldet mit „Nur während: {NpcBehaviour.NameOf(b.Kind)}“ nur, " +
                                           "solange dieses Verhalten läuft. Ein Figur-Knoten pro Verhalten hält den Graph übersichtlich.",
                                           EditorStyles.wordWrappedMiniLabel);

            if (Application.isPlaying && targets.Length == 1)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Jetzt (Play)", EditorStyles.boldLabel);
                if (!b.enabled) EditorGUILayout.LabelField("aus · Häkchen setzen probiert dieses Verhalten aus", EditorStyles.miniLabel);
                else EditorGUILayout.LabelField("▶ " + b.Status, EditorStyles.wordWrappedLabel);
                if (b.AddedAtRuntime)
                    EditorGUILayout.HelpBox("Von „Verhalten wechseln“ mit Standardwerten ergänzt. Nach Play ist es weg: " +
                                            NpcBehaviour.MenuPath + NpcBehaviour.NameOf(b.Kind) + ", um es einzustellen.", MessageType.Info);
            }
        }

        /// <summary>Starting behaviour, current behaviour, or waiting.</summary>
        static void Role(NpcBehaviour b)
        {
            var all = b.GetComponents<NpcBehaviour>();
            if (Application.isPlaying)
            {
                if (b.enabled) EditorGUILayout.LabelField("● aktuelles Verhalten", EditorStyles.boldLabel);
                return;
            }
            var first = all.FirstOrDefault(x => x.enabled);
            if (first == b)
            {
                string extra = all.Count(x => x.enabled) > 1 ? " (weitere mit Häkchen warten, das oberste gilt)" : "";
                EditorGUILayout.LabelField("● Startverhalten" + extra, EditorStyles.miniBoldLabel);
            }
            else if (b.enabled)
                EditorGUILayout.LabelField($"wartet: beim Start gilt das obere Verhalten ({NpcBehaviour.NameOf(first.Kind)})", EditorStyles.miniLabel);
            else if (all.Length > 1 || first == null)
                EditorGUILayout.LabelField("aus · startet mit „Verhalten wechseln“ im Graph", EditorStyles.miniLabel);
        }

        /// <summary>
        /// Figures walk on colliders. Without one under the feet nothing moves at Play, so say it now, with a fix.
        /// </summary>
        static void FloorCheck(NpcBehaviour b)
        {
            var feet = new Vector3(b.transform.position.x, Senses.Feet(b.transform) + 0.2f, b.transform.position.z);
            foreach (var hit in Physics.RaycastAll(feet, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(b.transform)) return;
            EditorGUILayout.HelpBox($"Unter „{b.name}“ ist kein Boden mit Collider. Figuren gehen nur auf Collidern; " +
                                    "ohne Boden bleibt sie bei Play stehen.", MessageType.Warning);
            if (GUILayout.Button(new GUIContent("Boden darunter anlegen", "Legt eine Fläche (Plane, 20 × 20 m) unter die Figur.")))
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Boden";
                Undo.RegisterCreatedObjectUndo(floor, "Boden anlegen");
                SceneManagementMove(floor, b.gameObject);
                floor.transform.position = feet - Vector3.up * 0.2f;
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
            }
        }

        static bool InPrefab(Component c) =>
            !c.gameObject.scene.IsValid() || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(c.gameObject) != null;

        static void RouteTools(Patrol p)
        {
            if (InPrefab(p))
            {
                EditorGUILayout.HelpBox("Im Prefab gibt es keine Route: Wegpunkte sind Objekte der Szene. Das Prefab in die Szene " +
                                        "ziehen und dort „Wegpunkt hinzufügen“ drücken; jede Instanz bekommt ihre eigene Route.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Wegpunkt hinzufügen", "Legt einen Wegpunkt an (ein leeres Objekt) und wählt ihn aus. " +
                                                    "Im Scene View mit dem Verschiebe-Werkzeug (W) an seinen Platz ziehen.")))
                    AddWaypoint(p);
                using (new EditorGUI.DisabledScope(p.waypoints.Count == 0))
                    if (GUILayout.Button(new GUIContent("Route auswählen", "Alle Wegpunkte auswählen, um sie zusammen zu verschieben.")))
                        Selection.objects = p.waypoints.Where(w => w != null).Select(w => (Object)w.gameObject).ToArray();
            }
            if (p.waypoints.Any(w => w == null))
                EditorGUILayout.HelpBox("Ein Wegpunkt ist leer oder wurde gelöscht; er wird übersprungen.", MessageType.Warning);
            if (p.waypoints.Any(w => w != null && w.IsChildOf(p.transform)))
            {
                EditorGUILayout.HelpBox("Ein Wegpunkt ist ein Kind der Figur und läuft mit ihr mit. Wegpunkte gehören neben die Figur, " +
                                        "nicht in sie hinein.", MessageType.Warning);
                if (GUILayout.Button("Wegpunkte neben die Figur legen"))
                {
                    var group = RouteGroup(p);
                    foreach (var w in p.waypoints.Where(w => w != null && w.IsChildOf(p.transform)).ToList())
                        Undo.SetTransformParent(w, group, "Wegpunkte neben die Figur legen");
                }
            }
        }

        /// <summary>
        /// A new Patrouillieren comes with a short route, so the first Play already shows it walking:
        /// one waypoint where the figure stands, one 6 m ahead. Only from Add Component (not from code).
        /// </summary>
        [InitializeOnLoadMethod]
        static void GiveNewPatrolsARoute()
        {
            ObjectFactory.componentWasAdded -= Added;
            ObjectFactory.componentWasAdded += Added;
        }

        static void Added(Component c)
        {
            if (Application.isPlaying || !(c is Patrol p) || p.waypoints.Count > 0) return;
            if (InPrefab(p)) return;   // a route lives in the scene; a prefab cannot point at scene objects
            AddWaypoint(p, select: false, at: p.transform.position);
            AddWaypoint(p, select: false, at: p.transform.position + p.transform.forward * 6f);
        }

        static Transform RouteGroup(Patrol p)
        {
            var groupName = p.name + " · Route";
            var parent = p.transform.parent;
            Transform group = parent != null
                ? parent.Find(groupName)
                : p.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == groupName)?.transform;
            if (group != null) return group;
            var go = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(go, "Wegpunkt hinzufügen");
            SceneManagementMove(go, p.gameObject);
            Undo.SetTransformParent(go.transform, parent, "Wegpunkt hinzufügen");
            go.transform.position = Vector3.zero;
            return go.transform;
        }

        /// <summary>
        /// New waypoint beside the figure, in a group „&lt;Figur&gt; · Route“ next to it (not inside it, or the
        /// route would walk along with the figure).
        /// </summary>
        public static Transform AddWaypoint(Patrol p, bool select = true, Vector3? at = null)
        {
            var group = RouteGroup(p);
            var last = p.waypoints.LastOrDefault(w => w != null);
            var pos = at ?? (last != null ? last.position + last.forward * 3f : p.transform.position + p.transform.forward * 3f);
            pos.y = Senses.Feet(p.transform);
            var wp = new GameObject("Wegpunkt " + (p.waypoints.Count + 1));
            Undo.RegisterCreatedObjectUndo(wp, "Wegpunkt hinzufügen");
            SceneManagementMove(wp, p.gameObject);
            Undo.SetTransformParent(wp.transform, group, "Wegpunkt hinzufügen");
            wp.transform.SetPositionAndRotation(pos, last != null ? last.rotation : p.transform.rotation);
            Undo.RecordObject(p, "Wegpunkt hinzufügen");
            p.waypoints.Add(wp.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(p);   // on a prefab instance the route is an override
            EditorUtility.SetDirty(p);
            if (select) Selection.activeGameObject = wp;
            return wp.transform;
        }

        static void SceneManagementMove(GameObject go, GameObject like)
        {
            if (go.scene != like.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, like.scene);
        }
    }

    /// <summary>Inspector for Wahrnehmung: the settings, and in Play what the figure knows right now.</summary>
    [CustomEditor(typeof(Senses))]
    [CanEditMultipleObjects]
    public class SensesEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var s = (Senses)target;
            EditorGUILayout.LabelField("Was die Figur bemerkt. Meldet im Graph über den Auslöser „Figur“: Sieht Spieler, Hört Spieler, " +
                                       "Hört Geräusch, Verliert Spieler, Berührt.", EditorStyles.wordWrappedMiniLabel);
            serializedObject.Update();
            VerhaltenFields.Draw(serializedObject);
            serializedObject.ApplyModifiedProperties();
            if (!s.sight && !s.hearing)
                EditorGUILayout.HelpBox("Sehen und Hören sind aus: die Figur bemerkt den Spieler nur, wenn er sie berührt.", MessageType.Info);
            if (targets.Length != 1) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Navigation: " + KitNavigation.Describe(), EditorStyles.miniLabel);
            if (!Application.isPlaying) return;

            EditorGUILayout.LabelField("Jetzt (Play)", EditorStyles.boldLabel);
            var c = GUI.color;
            GUI.color = VerhaltenStyle.Of(s.State);
            EditorGUILayout.LabelField("▶ " + s.Describe(), EditorStyles.boldLabel);
            GUI.color = c;
            var r = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.ProgressBar(r, s.Alert, s.Noticed ? "bemerkt" : $"Bemerken {s.Alert * 100f:0} %");
            EditorGUILayout.LabelField($"sieht: {(s.CanSee ? "ja" : "nein")}   hört: {(s.CanHear ? "ja" : "nein")}   berührt: {(s.IsTouching ? "ja" : "nein")}", EditorStyles.miniLabel);
            if (s.Target != null)
                EditorGUILayout.LabelField($"{s.Target.name}: {Vector3.Distance(s.transform.position, s.Target.position):0.0} m entfernt, " +
                                           $"{s.TargetSpeed:0.0} m/s (hörbar bis {s.HearingRangeFor(s.TargetSpeed):0.0} m)", EditorStyles.miniLabel);
            if (s.HasClue) EditorGUILayout.LabelField($"letzte Spur: vor {Time.time - s.LastClueTime:0.0} s", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Verhalten: " + NpcBehaviour.NameOf(s.CurrentKind), EditorStyles.miniLabel);
        }
    }
}
