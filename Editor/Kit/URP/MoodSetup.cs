using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kit.Editor
{
    /// <summary>
    /// Bildstimmungen: ready looks for a URP Volume profile. A look is a starting point, not a finished picture: it
    /// writes ordinary Volume overrides (Color Adjustments, Vignette, Bloom …) that students then tune where every
    /// Unity project tunes them.
    /// </summary>
    public static class MoodSetup
    {
        public enum Look
        {
            [InspectorName("Warm (Abendsonne)")] Warm,
            [InspectorName("Kalt (Nacht)")] Cold,
            [InspectorName("Traum")] Dream,
            [InspectorName("Bedrohlich")] Threat,
            [InspectorName("Unter Wasser")] Underwater,
            [InspectorName("Erinnerung (Sepia)")] Memory,
            [InspectorName("Rausch")] Trip,
            [InspectorName("Leer (selbst einstellen)")] Empty,
        }

        public const string DefaultFolder = "Assets/Bildstimmungen";
        /// <summary>Where new profiles go. Tests and tools point it elsewhere so they never touch a student's folder.</summary>
        public static string Folder = DefaultFolder;

        public static string Name(Look l) => l switch
        {
            Look.Warm => "Warm", Look.Cold => "Kalt", Look.Dream => "Traum", Look.Threat => "Bedrohlich",
            Look.Underwater => "Unter Wasser", Look.Memory => "Erinnerung", Look.Trip => "Rausch", _ => "Bildstimmung",
        };

        /// <summary>Creates a new profile asset with this look in Assets/Bildstimmungen (never overwrites one).</summary>
        public static VolumeProfile CreateProfile(Look look)
        {
            Directory.CreateDirectory(Folder);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{Name(look)}.asset");
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            Apply(p, look);
            return p;
        }

        /// <summary>Replaces the overrides of <paramref name="p"/> with the look (one Undo step).</summary>
        public static void Apply(VolumeProfile p, Look look)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Bildstimmung " + Name(look));
            int group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(p, "Bildstimmung " + Name(look));
            // Through Undo: Strg+Z brings back the old overrides with every value tuned in them.
            foreach (var c in p.components.ToArray()) { p.components.Remove(c); Undo.DestroyObjectImmediate(c); }
            p.isDirty = true;
            switch (look)
            {
                case Look.Warm:
                    Wb(p, 22f, 4f); Ca(p, 0.05f, 12f, new Color(1f, 0.96f, 0.9f), 0f, 8f);
                    Vig(p, 0.22f, 0.5f, Color.black); Bloom(p, 0.9f, 0.7f, new Color(1f, 0.85f, 0.7f));
                    break;
                case Look.Cold:
                    Wb(p, -30f, -4f); Ca(p, -0.6f, 15f, new Color(0.82f, 0.9f, 1f), 0f, -30f);
                    Vig(p, 0.35f, 0.45f, new Color(0f, 0.02f, 0.06f)); Bloom(p, 1f, 0.4f, new Color(0.7f, 0.85f, 1f));
                    break;
                case Look.Dream:
                    Ca(p, 0.3f, -12f, new Color(1f, 0.93f, 1f), 0f, -15f);
                    Vig(p, 0.3f, 0.9f, new Color(0.12f, 0.05f, 0.18f)); Bloom(p, 0.65f, 2.2f, new Color(1f, 0.9f, 1f), 0.85f);
                    Chroma(p, 0.35f); Lens(p, -0.12f);
                    break;
                case Look.Threat:
                    Ca(p, -0.2f, 35f, new Color(1f, 0.8f, 0.76f), 0f, -45f);
                    Vig(p, 0.45f, 0.35f, new Color(0.18f, 0f, 0f)); Grain(p, FilmGrainLookup.Medium3, 0.35f); Chroma(p, 0.2f);
                    break;
                case Look.Underwater:
                    Wb(p, -40f, 25f); Ca(p, -0.3f, -10f, new Color(0.6f, 0.9f, 0.95f), 0f, -10f);
                    Vig(p, 0.35f, 0.8f, new Color(0f, 0.08f, 0.12f)); Bloom(p, 0.8f, 0.6f, new Color(0.6f, 1f, 1f)); Lens(p, -0.25f);
                    break;
                case Look.Memory:
                    Ca(p, 0f, -10f, new Color(1f, 0.88f, 0.7f), 0f, -80f);
                    Vig(p, 0.4f, 0.6f, new Color(0.1f, 0.06f, 0.02f)); Grain(p, FilmGrainLookup.Large01, 0.5f);
                    break;
                case Look.Trip:
                    Ca(p, 0.1f, 15f, new Color(1f, 0.94f, 1f), 35f, 45f); Chroma(p, 0.9f); Lens(p, 0.25f);
                    Vig(p, 0.3f, 0.6f, new Color(0.2f, 0f, 0.25f)); Bloom(p, 0.75f, 1.3f, new Color(1f, 0.85f, 1f));
                    break;
            }
            EditorUtility.SetDirty(p);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);                                   // true: all parameters overridden; we set the ones that matter
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            if (AssetDatabase.Contains(p)) AssetDatabase.AddObjectToAsset(c, p);
            Undo.RegisterCreatedObjectUndo(c, "Bildstimmung");
            foreach (var param in c.parameters) param.overrideState = false;   // only what we set below counts
            return c;
        }

        static void Set<T>(VolumeParameter<T> p, T v) { p.value = v; p.overrideState = true; }

        static void Ca(VolumeProfile p, float exposure, float contrast, Color filter, float hue, float saturation)
        {
            var c = Add<ColorAdjustments>(p);
            Set(c.postExposure, exposure); Set(c.contrast, contrast); Set(c.colorFilter, filter);
            Set(c.hueShift, hue); Set(c.saturation, saturation);
        }
        static void Wb(VolumeProfile p, float temperature, float tint) { var c = Add<WhiteBalance>(p); Set(c.temperature, temperature); Set(c.tint, tint); }
        static void Vig(VolumeProfile p, float intensity, float smoothness, Color color) { var c = Add<Vignette>(p); Set(c.intensity, intensity); Set(c.smoothness, smoothness); Set(c.color, color); }
        static void Bloom(VolumeProfile p, float threshold, float intensity, Color tint, float scatter = 0.7f)
        { var c = Add<UnityEngine.Rendering.Universal.Bloom>(p); Set(c.threshold, threshold); Set(c.intensity, intensity); Set(c.tint, tint); Set(c.scatter, scatter); }
        static void Chroma(VolumeProfile p, float intensity) { var c = Add<ChromaticAberration>(p); Set(c.intensity, intensity); }
        static void Lens(VolumeProfile p, float intensity) { var c = Add<LensDistortion>(p); Set(c.intensity, intensity); }
        static void Grain(VolumeProfile p, FilmGrainLookup type, float intensity) { var c = Add<FilmGrain>(p); Set(c.type, type); Set(c.intensity, intensity); }

        /// <summary>Makes sure the scene's main camera shows post-processing (URP leaves it off on new cameras).</summary>
        public static void EnablePostOnMainCamera()
        {
            var cam = Camera.main;
            if (!cam) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data && !data.renderPostProcessing)
            {
                Undo.RecordObject(data, "Post Processing einschalten");
                data.renderPostProcessing = true;
                Debug.Log("Bildstimmung: An der Hauptkamera ist jetzt „Post Processing“ eingeschaltet, sonst wäre nichts zu sehen.", cam);
            }
        }

        // ---- GameObject › Atmosphäre › Bildstimmung › … ---------------------------------------------------
        static void CreateGlobal(Look look)
        {
            var go = new GameObject("Bildstimmung " + Name(look));
            Undo.RegisterCreatedObjectUndo(go, go.name);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true; v.weight = 1f; v.priority = 1f;
            v.sharedProfile = CreateProfile(look);
            EnablePostOnMainCamera();
            Selection.activeGameObject = go;
        }

        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Warm", false, 40)] static void G0() => CreateGlobal(Look.Warm);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Kalt", false, 41)] static void G1() => CreateGlobal(Look.Cold);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Traum", false, 42)] static void G2() => CreateGlobal(Look.Dream);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Bedrohlich", false, 43)] static void G3() => CreateGlobal(Look.Threat);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Unter Wasser", false, 44)] static void G4() => CreateGlobal(Look.Underwater);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Erinnerung", false, 45)] static void G5() => CreateGlobal(Look.Memory);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmung (ganze Szene)/Rausch", false, 46)] static void G6() => CreateGlobal(Look.Trip);

        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Bildstimmungs-Zone", false, 30)]
        static void CreateZone(MenuCommand cmd)
        {
            var go = new GameObject("Bildstimmungs-Zone");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            var sv = SceneView.lastActiveSceneView;
            if (sv && !(cmd.context as GameObject)) go.transform.position = sv.pivot;
            Undo.RegisterCreatedObjectUndo(go, go.name);
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(6f, 3f, 6f); box.center = new Vector3(0f, 1.5f, 0f);
            Undo.AddComponent<MoodZone>(go);
            Selection.activeGameObject = go;
        }
    }

    /// <summary>Adding a Bildstimmungs-Zone gives its Volume a profile right away, so there is something to see.</summary>
    [InitializeOnLoad]
    static class MoodZoneAdded
    {
        static MoodZoneAdded() => ObjectFactory.componentWasAdded += c =>
        {
            if (Application.isPlaying || !(c is MoodZone z)) return;
            var v = z.GetComponent<Volume>();
            if (v && !v.sharedProfile) { v.sharedProfile = MoodSetup.CreateProfile(MoodSetup.Look.Dream); EditorUtility.SetDirty(v); }
            MoodSetup.EnablePostOnMainCamera();
        };
    }

    [CustomEditor(typeof(MoodZone))]
    class MoodZoneEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        MoodSetup.Look m_Look = MoodSetup.Look.Dream;
        static MoodZone s_Previewing;

        public override void OnInspectorGUI()
        {
            var z = (MoodZone)target;
            var v = z.GetComponent<Volume>();
            ZoneInspector.Status(z);
            EditorGUILayout.HelpBox("In der Zone blendet die Volume darunter ein, draußen wieder aus. Den Look stellt ihr Profil ein (Color Adjustments, Vignette, Bloom …).", MessageType.None);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                m_Look = (MoodSetup.Look)EditorGUILayout.EnumPopup(new GUIContent("Vorlage", "Eine fertige Stimmung als Ausgangspunkt. Überschreibt das Profil der Volume."), m_Look);
                if (GUILayout.Button(new GUIContent("Anwenden", "Schreibt die Vorlage in das Profil (Rückgängig mit Strg+Z)."), GUILayout.Width(80)) && v)
                {
                    if (!v.sharedProfile) { Undo.RecordObject(v, "Profil"); v.sharedProfile = MoodSetup.CreateProfile(m_Look); }
                    else MoodSetup.Apply(v.sharedProfile, m_Look);
                }
            }

            if (v && !Application.isPlaying)
            {
                bool on = s_Previewing == z;
                bool want = GUILayout.Toggle(on, new GUIContent("Vorschau in der Scene- und Game-Ansicht", "Zeigt den Look jetzt schon, ohne Play. Geht beim Abwählen wieder aus."), "Button");
                if (want != on) Preview(want ? z : null);
            }
            if (Camera.main && Camera.main.GetUniversalAdditionalCameraData() is var d && d && !d.renderPostProcessing)
            {
                EditorGUILayout.HelpBox("Die Hauptkamera zeigt kein Post-Processing.", MessageType.Warning);
                if (GUILayout.Button("Post Processing an der Hauptkamera einschalten")) MoodSetup.EnablePostOnMainCamera();
            }
        }

        static void Preview(MoodZone z)
        {
            if (s_Previewing) { var old = s_Previewing.GetComponent<Volume>(); if (old) old.weight = 0f; }
            s_Previewing = z;
            if (z) z.GetComponent<Volume>().weight = z.strength;
            SceneView.RepaintAll();
        }

        void OnDisable() { if (s_Previewing == target) Preview(null); }   // abgewählt: Vorschau aus, nichts bleibt in der Szene hängen
    }
}
