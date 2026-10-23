using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// The kit's player. The package ships Player/Spieler.prefab (read-only in student projects); students work with a
    /// <b>Prefab Variant</b> of it, Assets/Spieler.prefab, which the course template already contains. A variant keeps
    /// every change the student makes (speed, camera, a different body) and still receives the kit's fixes on update.
    /// "Spieler-Variante anlegen" makes that variant if it is missing; "Spieler-Prefab neu bauen" rebuilds the shipped
    /// prefab itself and exists only in the studio project.
    /// </summary>
    public static class KitPlayerPrefab
    {
        /// <summary>Where the student's variant lives (the template ships it there).</summary>
        public const string VariantPath = "Assets/Spieler.prefab";

        /// <summary>GUID of the shipped Player/Spieler.prefab (stable: the package keeps the .meta files).</summary>
        public const string Guid = "c689fda3acbe5a6419b1e48ea86ebdad";
        const string StudioPath = "Assets/Kit/Player/Spieler.prefab";
        // Player/Materials (URP Lit, shipped with the player): Spieler.mat, Spieler Blickrichtung.mat
        const string BodyMaterialGuid = "eb15f519112b55245ad34d47538d59cc", VisorMaterialGuid = "7ca03ec8bd60b2f4cbee13cb9d5768af";

        /// <summary>Where the shipped prefab is (package or studio project), or null if it is missing.</summary>
        public static string ShippedPath => KitPaths.ByGuid(Guid);

        /// <summary>
        /// Where "Spieler-Prefab bauen" writes: over the shipped prefab when that is writable (studio project),
        /// otherwise into the project's own folder – never into the read-only package.
        /// </summary>
        public static string Path
        {
            get
            {
                var shipped = ShippedPath;
                if (KitPaths.IsWritable(shipped)) return shipped;
                return KitPaths.FromPackage ? KitPaths.StudentFolder + "/Spieler.prefab" : StudioPath;
            }
        }

        /// <summary>
        /// Tools › Kit › Spieler-Variante anlegen: the student's own Spieler (a variant of the shipped one) at
        /// <see cref="VariantPath"/>. If it exists, it is selected instead; nothing is ever overwritten.
        /// </summary>
        [MenuItem(KitBrand.ToolsMenu + "Spieler-Variante anlegen", false, 20)]
        static void VariantMenu()
        {
            var v = CreateVariant(out var message);
            Debug.Log(KitBrand.Log + message);
            if (v) { Selection.activeObject = v; EditorGUIUtility.PingObject(v); }
        }

        /// <summary>The variant at <see cref="VariantPath"/> (made if missing), or null if the shipped prefab is missing.</summary>
        public static GameObject CreateVariant(out string message, string path = VariantPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                message = IsVariantOfShipped(existing) ? $"Die Spieler-Variante gibt es schon: {path}"
                                                       : $"{path} gibt es schon, ist aber keine Variante des Kit-Spielers. Nichts geändert.";
                return existing;
            }
            // Occupied by something Unity cannot load as a prefab (broken file, not yet imported): never write over it.
            if (System.IO.File.Exists(path) || AssetDatabase.GetMainAssetTypeAtPath(path) != null)
            {
                message = $"{path} gibt es schon, lässt sich aber nicht als Prefab laden. Nichts geändert: die Datei prüfen oder umbenennen, dann noch einmal.";
                return null;
            }
            var shipped = ShippedPath;
            if (shipped == null) { message = "Der Kit-Spieler fehlt im Kurs-Paket (Player/Spieler.prefab)."; return null; }
            KitPaths.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(shipped));
            try
            {
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool saved);   // saving an instance of a prefab makes a variant
                message = saved && variant ? $"Spieler-Variante angelegt: {path}. In die Szene ziehen und dort oder im Prefab anpassen."
                                           : $"Spieler-Variante konnte nicht gespeichert werden: {path}";
                return saved ? variant : null;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>True if the prefab is a variant whose base is the shipped Spieler.</summary>
        public static bool IsVariantOfShipped(GameObject prefab)
        {
            if (!prefab || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant) return false;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            return source && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) == Guid;
        }

        /// <summary>Rebuilds the shipped prefab from code. The menu for it lives in the studio project only (KitDev); students make a variant instead.</summary>
        public static GameObject Build()
        {
            var path = Path;
            if (path != ShippedPath && System.IO.File.Exists(path))   // eine eigene Kopie, vielleicht geändert: nicht ungefragt ersetzen
            {
                switch (KitPaths.AskExisting("Das Spieler-Prefab", path))
                {
                    case KitPaths.Existing.Cancel: return null;
                    case KitPaths.Existing.NewCopy: path = AssetDatabase.GenerateUniqueAssetPath(path); break;
                }
            }
            return BuildAt(path);
        }

        /// <summary>Builds the prefab at exactly this path (no questions; replaces what is there).</summary>
        public static GameObject BuildAt(string path)
        {
            KitPaths.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var root = new GameObject("Spieler") { tag = "Player" };
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0); cc.height = 2f; cc.radius = 0.4f;
            cc.stepOffset = 0.35f; cc.slopeLimit = 45f; cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;   // sonst verschluckt der Controller kleine Schritte bei hoher Bildrate

            var bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyVisual.name = "Körper";
            Object.DestroyImmediate(bodyVisual.GetComponent<Collider>());    // die Kollision macht der CharacterController
            bodyVisual.GetComponent<Renderer>().sharedMaterial = KitMaterials.ShippedOrDefault(BodyMaterialGuid);
            bodyVisual.transform.SetParent(root.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0, 1, 0);
            bodyVisual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Blickrichtung";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.GetComponent<Renderer>().sharedMaterial = KitMaterials.ShippedOrDefault(VisorMaterialGuid);
            visor.transform.SetParent(bodyVisual.transform, false);
            visor.transform.localPosition = new Vector3(0, 0.55f, 0.45f);
            visor.transform.localScale = new Vector3(0.7f, 0.15f, 0.2f);

            var head = new GameObject("Kopf").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 1.6f, 0);

            var player = root.AddComponent<KitPlayer>();
            player.head = head;

            var camGo = new GameObject("Kamera") { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            // Rest pose = the view from behind (KitPlayerCamera: distance 3.5, shoulder 0.5). In edit mode the Game view
            // shows the player instead of the inside of its head; in Play, KitPlayerCamera places the camera each frame.
            camGo.transform.localPosition = head.localPosition + new Vector3(0.5f, 0f, -3.5f);
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<KitPlayerCamera>();
            follow.player = player;
            follow.hideInFirstPerson = new Renderer[] { bodyVisual.GetComponent<Renderer>(), visor.GetComponent<Renderer>() };

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log(KitBrand.Log + "Spieler-Prefab: " + path);
            return prefab;
        }

        /// <summary>
        /// The Spieler the kit menus place (examples, Werkstatt): the student's variant if there is one (their tuning
        /// carries over), else the shipped prefab; else one built earlier in the project; else a freshly built one.
        /// </summary>
        public static GameObject Load()
        {
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            if (IsVariantOfShipped(variant)) return variant;
            var shipped = ShippedPath;
            if (shipped != null) return AssetDatabase.LoadAssetAtPath<GameObject>(shipped);
            return AssetDatabase.LoadAssetAtPath<GameObject>(Path) ?? Build();
        }
    }
}
