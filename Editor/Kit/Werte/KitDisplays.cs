using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kit.Editor
{
    /// <summary>
    /// The three ready displays for „Wert anzeigen“ – Zahl, Balken, Symbolreihe – as prefabs, and the menu that puts
    /// one into the scene: GameObject › Kit › Anzeige › … finds or makes the Canvas (plumbing that teaches nothing)
    /// and leaves the design to the student: colours, sprites, place, size.
    /// The prefabs are built by <see cref="Build"/> (one source); <see cref="WritePrefabs"/> stores them in the kit.
    /// </summary>
    public static class KitDisplays
    {
        public enum Kind { Zahl, Balken, Symbolreihe }

        const string Folder = "Assets/Kit/Runtime/Werte/Anzeigen";
        static string FileName(Kind k) => $"Anzeige {k}.prefab";

        // Colours from the course look: warm chalk white, ochre for "full", slate for "empty". No red/green pair.
        static readonly Color k_Chalk = new Color(0.95f, 0.93f, 0.87f);
        static readonly Color k_Ochre = new Color(0.91f, 0.69f, 0.29f);
        static readonly Color k_Slate = new Color(0.16f, 0.22f, 0.21f, 0.85f);
        static readonly Color k_SlateLight = new Color(0.55f, 0.62f, 0.6f, 0.55f);

        [MenuItem(KitBrand.GameObjectMenu + "Anzeige/Zahl", false, 20)] static void MenuNumber() => Place(Kind.Zahl);
        [MenuItem(KitBrand.GameObjectMenu + "Anzeige/Balken", false, 21)] static void MenuBar() => Place(Kind.Balken);
        [MenuItem(KitBrand.GameObjectMenu + "Anzeige/Symbolreihe", false, 22)] static void MenuIcons() => Place(Kind.Symbolreihe);

        /// <summary>Puts a display into the open scene, under a Canvas (made if there is none). Returns it.</summary>
        public static KitValueView Place(Kind kind)
        {
            var canvas = FindOrMakeCanvas();
            GameObject go;
            var prefab = ShippedPrefab(kind);
            if (prefab != null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            else { go = Build(kind); go.transform.SetParent(canvas.transform, false); }
            // Stack below the displays already there, top left, so a new one never hides an old one.
            var rt = (RectTransform)go.transform;
            int index = 0;
            foreach (Transform t in canvas.transform) if (t != go.transform && t.GetComponent<KitValueView>() != null) index++;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -40f - index * 70f);
            GameObjectUtility.EnsureUniqueNameForSibling(go);
            Undo.RegisterCreatedObjectUndo(go, $"{KitBrand.Name}: Anzeige {kind} anlegen");
            Selection.activeGameObject = go;
            return go.GetComponent<KitValueView>();
        }

        static GameObject ShippedPrefab(Kind kind)
        {
            foreach (var guid in AssetDatabase.FindAssets($"\"Anzeige {kind}\" t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').EndsWith("/Werte/Anzeigen/" + FileName(kind))) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static Canvas FindOrMakeCanvas()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.GetComponent<KitDialogBox>() == null) return c;
            var root = new GameObject("Anzeigen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            Undo.RegisterCreatedObjectUndo(root, $"{KitBrand.Name}: Canvas anlegen");
            return canvas;
        }

        static Sprite Builtin(string name) => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name);

        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.sizeDelta = size;
            return rt;
        }

        static void TopLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(40, -40);
        }

        static Image Paint(RectTransform rt, Sprite sprite, Color color, bool sliced)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            if (sliced && sprite != null) img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>Builds one display as a fresh scene object (not saved).</summary>
        public static GameObject Build(Kind kind)
        {
            switch (kind)
            {
                case Kind.Zahl:
                {
                    var rt = Rect("Anzeige Zahl", null, new Vector2(360, 60)); TopLeft(rt);
                    var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
                    text.text = "0"; text.fontSize = 44; text.fontStyle = FontStyles.Bold; text.color = k_Chalk;
                    text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
                    text.outlineWidth = 0.18f; text.outlineColor = new Color32(20, 28, 27, 255);
                    var view = rt.gameObject.AddComponent<KitNumberView>();
                    view.field = text; view.format = "{0}";
                    return rt.gameObject;
                }
                case Kind.Balken:
                {
                    var rt = Rect("Anzeige Balken", null, new Vector2(360, 32)); TopLeft(rt);
                    Paint(rt, Builtin("Background.psd"), k_Slate, true);
                    var area = Rect("Füllbereich", rt, Vector2.zero);
                    area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(4, 4); area.offsetMax = new Vector2(-4, -4);
                    var fill = Rect("Füllung", area, Vector2.zero);
                    fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.5f, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                    Paint(fill, Builtin("UISprite.psd"), k_Ochre, true);
                    var view = rt.gameObject.AddComponent<KitBarView>();
                    view.fill = fill;
                    return rt.gameObject;
                }
                default:
                {
                    var rt = Rect("Anzeige Symbolreihe", null, new Vector2(400, 48)); TopLeft(rt);
                    var full = Rect("Symbol voll", rt, new Vector2(40, 40));
                    full.anchorMin = full.anchorMax = full.pivot = new Vector2(0, 0.5f); full.anchoredPosition = Vector2.zero;
                    Paint(full, Builtin("Knob.psd"), k_Ochre, false);
                    var empty = Rect("Symbol leer", rt, new Vector2(40, 40));
                    empty.anchorMin = empty.anchorMax = empty.pivot = new Vector2(0, 0.5f); empty.anchoredPosition = Vector2.zero;
                    Paint(empty, Builtin("Knob.psd"), k_SlateLight, false);
                    var view = rt.gameObject.AddComponent<KitIconRowView>();
                    view.full = full; view.empty = empty; view.spacing = 10;
                    full.gameObject.SetActive(false); empty.gameObject.SetActive(false);   // templates; the view shows copies
                    return rt.gameObject;
                }
            }
        }

        /// <summary>Studio tool: (re)writes the three prefabs into the kit. Batch: -executeMethod Kit.Editor.KitDisplays.WritePrefabs</summary>
        public static void WritePrefabs()
        {
            Directory.CreateDirectory(Folder);
            foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
            {
                var go = Build(k);
                PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + FileName(k));
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(KitBrand.Log + "Anzeigen geschrieben: " + Folder);
        }
    }
}
