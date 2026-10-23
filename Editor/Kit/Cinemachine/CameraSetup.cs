using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Editor side of the Kamera presets and Kamera-Zone. Adding one does the drudgery at once and visibly:
    /// the main camera gets its CinemachineBrain, the Spieler camera becomes the CinemachineCamera "Spieler-Sicht",
    /// a new preset camera stands where the Scene view looks. All of it is one Undo step.
    /// </summary>
    [InitializeOnLoad]
    public static class CameraSetup
    {
        static CameraSetup() => ObjectFactory.componentWasAdded += Added;

        static void Added(Component c)
        {
            if (Application.isPlaying) return;
            if (c is CameraPreset preset)
            {
                PlaceAtSceneView(preset.transform);
                if (preset is RailCamera rail) rail.PlaceRail();      // die Schiene zieht mit an den neuen Ort
                BridgePlayer();
            }
            else if (c is CameraZone zone)
            {
                BridgePlayer();
                if (!zone.view) zone.view = Object.FindAnyObjectByType<CameraPreset>()?.Camera;
            }
        }

        /// <summary>A new camera at the world origin is useless: put it where the Scene view looks from.</summary>
        static void PlaceAtSceneView(Transform t)
        {
            var sv = SceneView.lastActiveSceneView;
            if (!sv || t.position != Vector3.zero) return;
            Undo.RecordObject(t, "Kamera ausrichten");
            t.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
        }

        /// <summary>
        /// Main camera driven by KitPlayerCamera → Brain on the camera, the player's view on "Spieler-Sicht".
        /// Returns true if something changed.
        /// </summary>
        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Spieler-Kamera für Cinemachine einrichten", false, 30)]
        public static bool BridgePlayer()
        {
            var main = Camera.main;
            if (!main) return false;
            bool changed = false;
            var brain = main.GetComponent<CinemachineBrain>();
            if (!brain)
            {
                brain = Undo.AddComponent<CinemachineBrain>(main.gameObject);
                PlayerCameraBridge.Configure(brain);
                changed = true;
            }
            else if (!brain.IgnoreTimeScale)
            {
                Undo.RecordObject(brain, "Kamerafahrten in echter Zeit");   // sonst stehen Kamerafahrten in Pause und Zeitlupe still
                brain.IgnoreTimeScale = true;
                changed = true;
            }
            var direct = main.GetComponent<KitPlayerCamera>();
            if (direct)
            {
                var view = PlayerCameraBridge.CreateView(main, direct);
                Undo.RegisterCreatedObjectUndo(view.gameObject, "Spieler-Sicht anlegen");
                Undo.DestroyObjectImmediate(direct);
                EditorGUIUtility.PingObject(view.gameObject);
                Debug.Log("Kamera: Die Spieler-Kamera läuft jetzt über Cinemachine („Spieler-Sicht“). Ohne andere Kameras sieht alles aus wie vorher.", view);
                changed = true;
            }
            return changed;
        }

        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Spieler-Kamera für Cinemachine einrichten", true)]
        static bool CanBridge() => Camera.main && (Camera.main.GetComponent<KitPlayerCamera>() || !Camera.main.GetComponent<CinemachineBrain>()
                                                   || !Camera.main.GetComponent<CinemachineBrain>().IgnoreTimeScale);

        // ---- GameObject › Kamera › … : a ready camera object where the Scene view looks ----------------------
        static void Create<T>(string name, MenuCommand cmd) where T : CameraPreset
        {
            var go = new GameObject(name);
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.AddComponent<CinemachineCamera>();
            Undo.AddComponent<T>(go);                                // löst Added aus: ausrichten, Spieler-Kamera einrichten
            Selection.activeGameObject = go;
        }

        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Verfolgen", false, 10)] static void Follow(MenuCommand c) => Create<FollowCamera>("Kamera Verfolgen", c);
        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Fest", false, 11)] static void Fixed(MenuCommand c) => Create<FixedCamera>("Kamera Fest", c);
        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Blick auf", false, 12)] static void LookAt(MenuCommand c) => Create<LookAtCamera>("Kamera Blick auf", c);
        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Schiene", false, 13)] static void Rail(MenuCommand c) => Create<RailCamera>("Kamera Schiene", c);

        [MenuItem(KitBrand.GameObjectMenu + "Kamera/Kamera-Zone", false, 20)]
        static void Zone(MenuCommand c)
        {
            var go = new GameObject("Kamera-Zone");
            GameObjectUtility.SetParentAndAlign(go, c.context as GameObject);
            var sv = SceneView.lastActiveSceneView;
            if (sv && !(c.context as GameObject)) go.transform.position = sv.pivot;
            Undo.RegisterCreatedObjectUndo(go, "Kamera-Zone");
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(6f, 3f, 6f); box.center = new Vector3(0f, 1.5f, 0f);
            Undo.AddComponent<CameraZone>(go);
            Selection.activeGameObject = go;
        }
    }

    [CustomEditor(typeof(CameraZone)), CanEditMultipleObjects]
    class CameraZoneEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var z = (CameraZone)target;
            ZoneInspector.Status(z);
            DrawDefaultInspector();
            if (z.view) return;
            EditorGUILayout.HelpBox("Noch keine Kamera: eine CinemachineCamera auf „Kamera“ ziehen, oder hier eine feste Kamera anlegen und danach in der Scene-Ansicht ausrichten.", MessageType.Warning);
            if (GUILayout.Button("Feste Kamera anlegen und eintragen"))
            {
                var go = new GameObject("Kamera " + z.name);
                Undo.RegisterCreatedObjectUndo(go, "Kamera anlegen");
                go.AddComponent<CinemachineCamera>();
                Undo.AddComponent<FixedCamera>(go);                  // steht dann, wo die Scene-Ansicht hinschaut
                Undo.RecordObject(z, "Kamera eintragen");
                z.view = go.GetComponent<CinemachineCamera>();
                Selection.activeGameObject = go;
            }
        }
    }

    [CustomEditor(typeof(CameraPreset), true)]
    class CameraPresetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var p = (CameraPreset)target;
            var cam = p.Camera;
            string what = p switch
            {
                FollowCamera _ => "hängt dem Ziel hinterher. Abstand: „Follow Offset“ im CinemachineFollow darunter.",
                FixedCamera _ => "steht still. Position und Drehung: dieses Objekt verschieben und drehen (oder in der Scene-Ansicht ausrichten: GameObject › Align With View).",
                LookAtCamera _ => "steht still und dreht sich zum Ziel.",
                RailCamera _ => "fährt auf der Schiene mit. Die Schiene ist das Objekt „Schiene“: mit dem Spline-Werkzeug biegen.",
                _ => "",
            };
            EditorGUILayout.HelpBox($"Kamera „{p.Kind}“ {what}", MessageType.None);
            if (!(p is FixedCamera))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Ziel", "Wem die Kamera folgt oder wen sie anschaut: das „Tracking Target“ der CinemachineCamera."));
                    var t = (Transform)EditorGUILayout.ObjectField(cam.Target.TrackingTarget, typeof(Transform), true);
                    if (t != cam.Target.TrackingTarget) { Undo.RecordObject(cam, "Ziel"); cam.Target.TrackingTarget = t; }
                }
                if (!cam.Target.TrackingTarget) EditorGUILayout.HelpBox("Kein Ziel: die Kamera weiß nicht, wem sie folgen soll.", MessageType.Warning);
            }
            DrawDefaultInspector();
            if (Camera.main && Camera.main.GetComponent<KitPlayerCamera>())
            {
                EditorGUILayout.HelpBox("Die Spieler-Kamera ist noch nicht mit Cinemachine verbunden. Im Play-Modus geschieht das von selbst; hier sichtbar machen:", MessageType.Info);
                if (GUILayout.Button("Spieler-Kamera einrichten")) CameraSetup.BridgePlayer();
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Zeigen", "Nur im Play-Modus: zu dieser Kamera wechseln, zum Ausprobieren."))) p.Show();
                if (GUILayout.Button(new GUIContent("Zurückgeben", "Nur im Play-Modus: das Bild an die Kamera davor zurückgeben."))) p.Hide(p.seconds, p.ease);
            }
        }
    }
}
