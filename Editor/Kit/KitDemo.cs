using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Builds the course example "Schlüssel und Tür": the graph (as a student would draw it) and a small scene.</summary>
    public static class KitDemo
    {
        /// <summary>Studio project: next to the kit (Assets/Kit/Demo). Student project (kit as package): Assets/Kit Beispiel.</summary>
        public static string Folder => KitPaths.FromPackage ? KitPaths.StudentFolder : "Assets/Kit/Demo";
        public static string GraphPath => Folder + "/Schlüssel und Tür.kit";
        public static string ScenePath => Folder + "/Keller.unity";

        // Fixed node ids: every build of the example gets the same ids, so slot ids – and with them the bindings of
        // any scene that uses the graph, e.g. a copied example scene – stay valid when it is rebuilt.
        static Hash128 Id(string role) => Hash128.Compute("de.macromedia.gec1/Schlüssel und Tür/" + role);

        /// <summary>Draws the graph: same nodes and wires as on the v3 board.</summary>
        public static KitGraph BuildKeyDoorGraph(string path)
        {
            var g = KitGraphEditing.CreateAsset(path);
            var key = KitGraphEditing.AddNode<ObjectNode>(g, new Vector2(0, 140), Id("key"));
            KitGraphEditing.SetOption(key, ObjectNode.NameOption, "Schlüssel");
            var use = KitGraphEditing.AddNode<InteractNode>(g, new Vector2(260, 0), Id("use"));
            var flag = KitGraphEditing.AddNode<FlagNode>(g, new Vector2(560, -40), Id("flag"));
            KitGraphEditing.SetOption(flag, "label", "Schlüssel vorhanden");
            var hide = KitGraphEditing.AddNode<SetActiveNode>(g, new Vector2(560, 190), Id("hide"));
            var zone = KitGraphEditing.AddNode<ZoneNode>(g, new Vector2(260, 420), Id("zone"));
            var branch = KitGraphEditing.AddNode<BranchNode>(g, new Vector2(560, 420), Id("branch"));
            var move = KitGraphEditing.AddNode<MoveNode>(g, new Vector2(860, 380), Id("move"));
            KitGraphEditing.SetOption(move, "offset", new Vector3(0, 3, 0));
            KitGraphEditing.SetOption(move, "seconds", 1f);
            var sign = KitGraphEditing.AddNode<SetTextNode>(g, new Vector2(860, 680), Id("sign"));
            KitGraphEditing.SetOption(sign, "format", "Verschlossen.");
            var sound = KitGraphEditing.AddNode<PlaySoundNode>(g, new Vector2(1180, 400), Id("sound"));

            KitGraphEditing.Connect(g, key, ObjectNode.Out, use, "target");
            KitGraphEditing.Connect(g, key, ObjectNode.Out, hide, "target");
            KitGraphEditing.Connect(g, use, "used", flag, "SetTrue");     // 1.
            KitGraphEditing.Connect(g, use, "used", hide, "Off");         // 2.
            KitGraphEditing.Connect(g, zone, "entered", branch, "Check");
            KitGraphEditing.Connect(g, flag, "value", branch, "condition");
            KitGraphEditing.Connect(g, branch, "yes", move, "Forward");
            KitGraphEditing.Connect(g, branch, "no", sign, "Write");
            KitGraphEditing.Connect(g, move, "arrived", sound, "Play");
            KitGraphEditing.Save(g);
            return AssetDatabase.LoadAssetAtPath<KitGraph>(path);
        }

        [MenuItem("Tools/Kit/Beispiel „Schlüssel und Tür“ bauen")]
        static void BuildMenu() => BuildAll();

        /// <summary>
        /// Builds graph + scene. An existing example (maybe changed by the student) is only replaced on an
        /// explicit "Überschreiben"; otherwise the new one goes into a fresh folder. Unsaved open scenes are
        /// offered for saving first. Returns the scene path, or null if nothing was built.
        /// </summary>
        public static string BuildAll()
        {
            if (!KitPaths.OpenScenesSafe()) return null;
            var answer = KitPaths.Existing.Overwrite;              // nothing there yet: nothing to overwrite
            if (ExampleExists(Folder)) answer = KitPaths.AskExisting("Das Beispiel „Schlüssel und Tür“", Folder);
            return BuildAll(Folder, answer);
        }

        static bool ExampleExists(string folder) =>
            File.Exists(folder + "/Schlüssel und Tür.kit") || File.Exists(folder + "/Keller.unity");

        /// <summary>The same without the dialog: what to do if the example is already in <paramref name="folder"/>.</summary>
        public static string BuildAll(string folder, KitPaths.Existing whenExisting)
        {
            if (!KitPaths.OpenScenesSafe()) return null;
            string graphPath = folder + "/Schlüssel und Tür.kit", scenePath = folder + "/Keller.unity";
            if (ExampleExists(folder))
            {
                switch (whenExisting)
                {
                    case KitPaths.Existing.Cancel: return null;
                    case KitPaths.Existing.NewCopy:
                        folder = AssetDatabase.GenerateUniqueAssetPath(folder);
                        graphPath = folder + "/Schlüssel und Tür.kit"; scenePath = folder + "/Keller.unity";
                        break;
                    case KitPaths.Existing.Overwrite:
                        break;                                     // ausdrücklich gewünscht: frisches Beispiel, siehe unten
                }
            }
            KitPaths.EnsureFolder(folder);                         // jede fehlende Ebene; nie in Packages
            var graph = File.Exists(graphPath) ? RebuildInPlace(graphPath) : BuildKeyDoorGraph(graphPath);
            var clip = WriteClick(folder + "/Tür-Klack.wav");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Licht").AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Boden"; floor.transform.localScale = new Vector3(2, 1, 2);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(KitPlayerPrefab.Load());
            player.transform.position = new Vector3(0, 0, -6);

            var root = new GameObject("Schlüssel und Tür");
            var it = root.AddComponent<Interaction>(); it.Graph = graph;
            var key = GameObject.CreatePrimitive(PrimitiveType.Cube); key.name = "Schlüssel"; key.transform.SetParent(root.transform);
            key.transform.position = new Vector3(3, 0.4f, -3); key.transform.localScale = new Vector3(0.2f, 0.2f, 0.6f);
            var zone = new GameObject("Türzone"); zone.transform.SetParent(root.transform);
            zone.transform.position = new Vector3(0, 1, 0.5f);
            var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(3, 2, 2);
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube); door.name = "Tür"; door.transform.SetParent(root.transform);
            door.transform.position = new Vector3(0, 1.5f, 2); door.transform.localScale = new Vector3(3, 3, 0.2f);
            var sign = new GameObject("Schild"); sign.transform.SetParent(root.transform);
            sign.transform.position = new Vector3(2.2f, 2, 1.8f);
            var tmp = sign.AddComponent<TMPro.TextMeshPro>(); tmp.fontSize = 3; tmp.text = "";
            var klack = new GameObject("Tür-Klack"); klack.transform.SetParent(root.transform);
            klack.transform.position = door.transform.position;
            var src = klack.AddComponent<AudioSource>(); src.playOnAwake = false; src.clip = clip;

            foreach (var slot in graph.slots)
            {
                var user = slot.users[0].Split('/');
                var node = graph.nodes[graph.IndexOf(user[0])];
                Object target = slot.label == "Schlüssel" ? key
                    : node is Zone ? zone : node is Move ? door : node is SetText ? sign : node is PlaySound ? klack : null;
                if (target) it.SetBinding(slot.id, target);
            }
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[KitDemo] {graphPath} + {scenePath}: {graph.nodes.Count} Knoten, {graph.slots.Count} Slots");
            return scenePath;
        }

        /// <summary>
        /// Overwrite keeps the asset: the fresh graph is built next to it and its content copied over the old file.
        /// The .meta (and so the GUID) stays, and the fixed node ids keep the slots: other scenes still find the graph.
        /// </summary>
        static KitGraph RebuildInPlace(string graphPath)
        {
            string fresh = Path.GetDirectoryName(graphPath).Replace('\\', '/') + "/Schlüssel und Tür (neu gebaut).kit";
            AssetDatabase.DeleteAsset(fresh);
            BuildKeyDoorGraph(fresh);
            File.Copy(fresh, graphPath, overwrite: true);
            AssetDatabase.DeleteAsset(fresh);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<KitGraph>(graphPath);
        }

        static AudioClip WriteClick(string path)
        {
            if (!File.Exists(path))
            {
                const int rate = 22050; int n = rate / 4;
                using var w = new BinaryWriter(File.Create(path));
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                for (int i = 0; i < n; i++) { float t = i / (float)rate; w.Write((short)(Mathf.Sin(2 * Mathf.PI * 180 * t) * Mathf.Exp(-t * 25) * 14000)); }
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
