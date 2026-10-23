using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kit.Editor
{
    /// <summary>
    /// GameObject › Kit › Bildschirm › Start / Pause / Gewonnen / Verloren. The first time, the project gets its own
    /// Prefab Variant of the shipped screen in Assets/Bildschirme (restyle it there, every scene follows); then the
    /// variant is placed in the scene. Start is visible, the others start switched off.
    /// </summary>
    public static class KitScreensMenu
    {
        public enum Kind { Start, Pause, Gewonnen, Verloren }

        /// <summary>Where the project's own screen variants go. Tests point it at a temporary folder.</summary>
        public static string Folder = "Assets/Bildschirme";

        // GUIDs of the shipped prefabs in Runtime/Screens (stable: the package keeps the .meta files).
        public static readonly Dictionary<Kind, string> Guids = new Dictionary<Kind, string>
        {
            { Kind.Start, "ef0f9b2800b511649aa5abeabc5a716d" }, { Kind.Pause, "bf666762c8c2ea9478caab475ccac256" }, { Kind.Gewonnen, "63e0a8a7778ba064ab108e92a7f785f8" }, { Kind.Verloren, "18d9b9e7229155240a2518983cb33c69" },
        };

        [MenuItem(KitBrand.GameObjectMenu + "Bildschirm/Start", false, 13)] static void Start(MenuCommand c) => Create(Kind.Start);
        [MenuItem(KitBrand.GameObjectMenu + "Bildschirm/Pause", false, 14)] static void Pause(MenuCommand c) => Create(Kind.Pause);
        [MenuItem(KitBrand.GameObjectMenu + "Bildschirm/Gewonnen", false, 15)] static void Won(MenuCommand c) => Create(Kind.Gewonnen);
        [MenuItem(KitBrand.GameObjectMenu + "Bildschirm/Verloren", false, 16)] static void Lost(MenuCommand c) => Create(Kind.Verloren);

        /// <summary>The project's own variant of a screen, made on first use.</summary>
        public static string OwnVariant(Kind kind)
        {
            var own = $"{Folder}/{kind}.prefab";
            if (File.Exists(own)) return own;
            var shippedPath = KitPaths.ByGuid(Guids[kind]);
            if (shippedPath == null) { Debug.LogError(KitBrand.Log + $"Bildschirm „{kind}“ fehlt im Paket (GUID {Guids[kind]})."); return null; }
            KitPaths.EnsureFolder(Folder);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(shippedPath));
            try { PrefabUtility.SaveAsPrefabAsset(inst, own); }
            finally { Object.DestroyImmediate(inst); }
            Debug.Log(KitBrand.Log + $"Eigene Variante „{own}“ angelegt: dort umgestalten (Farben, Schrift, Texte), alle Szenen übernehmen es.",
                AssetDatabase.LoadAssetAtPath<GameObject>(own));
            return own;
        }

        public static GameObject Create(Kind kind)
        {
            var own = OwnVariant(kind);
            if (own == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(own), EditorSceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(go, $"{KitBrand.Name}: Bildschirm {kind} anlegen");
            if (kind != Kind.Start)                         // shown by switching it on (An/Aus), e.g. on Esc or at the goal
            {
                go.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);   // saved with the scene as an override
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
            EnsureEventSystem();
            Selection.activeGameObject = go;
            return go;
        }

        /// <summary>UI clicks need one EventSystem per scene; placed now, so it is visible in the Hierarchy.</summary>
        public static void EnsureEventSystem()
        {
            var es = KitScreen.EnsureEventSystem();
            if (es) Undo.RegisterCreatedObjectUndo(es, $"{KitBrand.Name}: EventSystem anlegen");
        }
    }

    /// <summary>Bildschirm in the Inspector: which of its buttons already do something, and the one step for the others.</summary>
    [CustomEditor(typeof(KitScreen))]
    public class KitScreenEditor : UnityEditor.Editor
    {
        public enum State { Wired, NoAction, LogicOff, OnClick, Open }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var screen = (KitScreen)target;
            var buttons = screen.GetComponentsInChildren<Button>(true);
            if (buttons.Length == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Knöpfe", EditorStyles.boldLabel);
            bool anyOpen = false;
            foreach (var b in buttons)
            {
                var (state, by) = StateOf(screen, b);
                anyOpen |= state != State.Wired && state != State.OnClick;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(state == State.Wired || state == State.OnClick ? "✓" : "○", GUILayout.Width(16));
                    if (GUILayout.Button(new GUIContent(b.name, "Knopf in der Hierarchy zeigen"), EditorStyles.label, GUILayout.Width(140)))
                        EditorGUIUtility.PingObject(b.gameObject);
                    EditorGUILayout.LabelField(Describe(state, by), EditorStyles.miniLabel);
                }
            }
            if (anyOpen)
                EditorGUILayout.HelpBox(
                    "Ein Knopf tut erst etwas, wenn eine Logik auf ihn hört. Ein Schritt pro Knopf:\n" +
                    "Logik öffnen (oder Add Component › " + KitBrand.Name + " › Logik) → Auslöser „Knopf gedrückt“ → bei ◆ Knopf den Knopf aus der Hierarchy hineinziehen → " +
                    "„Gedrückt“ mit der Aktion verbinden.\n" +
                    "Zum Beispiel: Neustart, Nächste Szene, Szene laden (Hauptmenü), Spiel beenden. Diesen Bildschirm schließen (Weiter): " +
                    "An/Aus › Ausschalten mit ◆ Objekt = dieser Bildschirm; dann laufen Spieler und Zeit wieder.",
                    MessageType.Info);
        }

        static string Describe(State s, string by) => s switch
        {
            State.Wired => $"verbunden ({by})",
            State.OnClick => "verbunden (On Click im Inspector)",
            State.NoAction => $"zugeordnet in „{by}“, aber „Gedrückt“ führt noch zu keiner Aktion",
            State.LogicOff => $"zugeordnet in „{by}“, aber diese Logik ist ausgeschaltet",
            _ => "noch nicht verbunden",
        };

        /// <summary>What a button is wired to, as truthfully as edit mode can tell (in Play: what is actually listening).</summary>
        public static (State state, string by) StateOf(KitScreen screen, Button b)
        {
            if (Application.isPlaying)          // in Play: what is actually running
            {
                if (UiButton.IsListening(b)) return (State.Wired, UiButton.BoundBy(b)?.name ?? "läuft");
                if (UiButton.BoundBy(b) is Logic on) return (State.NoAction, on.name);
            }
            var best = (state: KitScreen.HasLiveOnClick(b) ? State.OnClick : State.Open, by: (string)null);
            var scene = screen.gameObject.scene;
            if (!scene.IsValid()) return best;
            foreach (var logic in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Logic>(true)))
            {
                var g = logic.Graph;
                if (!g) continue;
                for (int i = 0; i < g.nodes.Count; i++)
                {
                    if (!(g.nodes[i] is UiButton)) continue;
                    var bound = Logic.Convert(logic.GetBinding(g.SlotFor(i, "button")), typeof(Button)) as Button;
                    if (bound != b) continue;
                    // A Logik on (or under) this screen is off while the screen is hidden; it runs once the screen shows.
                    bool off = !logic.enabled || (!logic.gameObject.activeInHierarchy && !logic.transform.IsChildOf(screen.transform));
                    var s = off ? State.LogicOff : g.WiresFrom(i, "pressed").Count == 0 ? State.NoAction : State.Wired;
                    if (s == State.Wired) return (s, logic.name);
                    if (best.state == State.Open || (best.state == State.LogicOff && s == State.NoAction)) best = (s, logic.name);
                }
            }
            return best;
        }
    }
}
