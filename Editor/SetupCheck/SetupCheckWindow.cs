using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gec1.SetupCheck
{
    /// <summary>Menu "Kurs > Setup prüfen": a pass/fail list with a fix for every problem.</summary>
    public sealed class SetupCheckWindow : EditorWindow
    {
        List<CheckResult> results;
        Vector2 scroll;
        GUIStyle badge, titleStyle, body, fix, summary;

        [MenuItem("Kurs/Setup prüfen", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<SetupCheckWindow>();
            w.titleContent = new GUIContent("Setup prüfen");
            w.minSize = new Vector2(420, 360);
            w.Refresh();
            w.Show();
        }

        void OnEnable()
        {
            if (results == null) Refresh();
        }

        void OnFocus()
        {
            // Students fix things in GitHub Desktop or the file browser, then come back.
            if (results != null) Refresh();
        }

        void Refresh()
        {
            results = SetupCheckRunner.Run();
            Repaint();
        }

        void Styles()
        {
            if (badge != null) return;
            badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fixedWidth = 72 };
            badge.normal.textColor = Color.white;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            body = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            fix = new GUIStyle(EditorStyles.label) { wordWrap = true, fontStyle = FontStyle.Italic };
            summary = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 13 };
        }

        static Color ColorFor(CheckStatus s)
        {
            switch (s)
            {
                case CheckStatus.Ok: return new Color(0.20f, 0.55f, 0.25f);
                case CheckStatus.Info: return new Color(0.30f, 0.42f, 0.60f);
                case CheckStatus.Warn: return new Color(0.75f, 0.50f, 0.05f);
                default: return new Color(0.75f, 0.18f, 0.15f);
            }
        }

        void OnGUI()
        {
            Styles();
            if (results == null) Refresh();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(CheckResult.Summary(results), summary);
            EditorGUILayout.LabelField("Kurs-Stack: " + StackConfig.UnityLabel + " (" + StackConfig.UnityVersion + "), Vorlage " + StackConfig.Template,
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Erneut prüfen", GUILayout.Height(24))) Refresh();
                if (GUILayout.Button("Bericht kopieren", GUILayout.Height(24)))
                {
                    EditorGUIUtility.systemCopyBuffer = CheckResult.Report(results, SetupCheckRunner.Header());
                    ShowNotification(new GUIContent("Kopiert. In den Chat mit dem KI-Agenten oder an Jonas einfügen."));
                }
            }
            EditorGUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var r in results)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect rect = GUILayoutUtility.GetRect(72, 18, GUILayout.Width(72));
                        EditorGUI.DrawRect(rect, ColorFor(r.Status));
                        GUI.Label(rect, CheckResult.Label(r.Status), badge);
                        GUILayout.Space(6);
                        GUILayout.Label(r.Title, titleStyle);
                    }
                    if (!string.IsNullOrEmpty(r.Detail)) GUILayout.Label(r.Detail, body);
                    if (!string.IsNullOrEmpty(r.Fix)) GUILayout.Label("Lösung: " + r.Fix, fix);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
