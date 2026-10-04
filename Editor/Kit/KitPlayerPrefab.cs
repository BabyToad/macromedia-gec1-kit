using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Builds the kit's player prefab: CharacterController, KitPlayer, head pivot, camera.</summary>
    public static class KitPlayerPrefab
    {
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

        [MenuItem("Tools/Kit/Spieler-Prefab bauen")]
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
            Debug.Log("[Kit] Spieler-Prefab: " + path);
            return prefab;
        }

        /// <summary>The shipped prefab; else one built earlier in the project; else a freshly built one.</summary>
        public static GameObject Load()
        {
            var shipped = ShippedPath;
            if (shipped != null) return AssetDatabase.LoadAssetAtPath<GameObject>(shipped);
            return AssetDatabase.LoadAssetAtPath<GameObject>(Path) ?? Build();
        }
    }
}
