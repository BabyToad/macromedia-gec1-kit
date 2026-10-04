using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kit.Editor
{
    /// <summary>Ready-made scene objects for the Spielsysteme nodes.</summary>
    public static class KitSystemsMenu
    {
        /// <summary>A screen-space dialog window: panel at the bottom, speaker, line, three answers.</summary>
        [MenuItem("GameObject/Kit/Dialogfenster", false, 11)]
        public static KitDialogBox CreateDialogBox()
        {
            var root = new GameObject("Dialogfenster", typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

            var panel = new GameObject("Rahmen", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var pr = (RectTransform)panel.transform;
            pr.anchorMin = new Vector2(0.15f, 0.04f); pr.anchorMax = new Vector2(0.85f, 0.3f); pr.offsetMin = pr.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.09f, 0.92f);

            TMP_Text Text(string name, Vector2 min, Vector2 max, float size, FontStyles style)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(panel.transform, false);
                var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = new Vector2(24, 0); r.offsetMax = new Vector2(-24, 0);
                var t = go.GetComponent<TextMeshProUGUI>(); t.fontSize = size; t.fontStyle = style; t.color = new Color(0.94f, 0.92f, 0.86f);
                return t;
            }

            var box = root.AddComponent<KitDialogBox>();
            box.panel = panel;
            box.speaker = Text("Sprecher", new Vector2(0, 0.78f), new Vector2(1, 0.98f), 30, FontStyles.Bold);
            box.line = Text("Zeile", new Vector2(0, 0.42f), new Vector2(1, 0.78f), 34, FontStyles.Normal);
            box.answers = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
                box.answers[i] = Text("Antwort " + (char)('A' + i), new Vector2(i / 3f, 0.05f), new Vector2((i + 1) / 3f, 0.35f), 28, FontStyles.Normal);
            panel.SetActive(false);

            Undo.RegisterCreatedObjectUndo(root, "Kit: Dialogfenster anlegen");
            Selection.activeGameObject = root;
            return box;
        }
    }
}
