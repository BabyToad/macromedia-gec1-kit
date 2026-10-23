using UnityEngine;
using UnityEngine.Rendering;

namespace Kit
{
    /// <summary>
    /// Tageszeit: one slider from 0 to 24 h sets the sun's angle and colour, the ambient light and (optionally) the
    /// fog colour. Works in the editor too: drag the slider and the scene changes. It can also run on its own
    /// (a whole day in a few minutes), or be blended by the node "Tageszeit".
    /// Put it on the scene's Directional Light (the sun).
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Tageszeit")]
    [RequireComponent(typeof(Light))]
    [ExecuteAlways]
    public class DayCycle : MonoBehaviour
    {
        [Hint("Die Uhrzeit. Ziehen zeigt die Wirkung sofort, auch ohne Play.", unit: "h", range: "0 bis 24", edges: "6: Sonnenaufgang. 12: Mittag. 18: Sonnenuntergang. 0 und 24: Mitternacht.", label: "Uhrzeit")]
        [Range(0f, 24f)] public float hour = 16.5f;

        [Hint("An: die Zeit läuft im Spiel von selbst weiter. Aus: sie bleibt stehen, bis ein Knoten „Tageszeit“ sie bewegt.", label: "Läuft")]
        public bool running = false;

        [Hint("Nur wenn die Zeit läuft: so lange dauert ein ganzer Tag in echter Zeit.", unit: "min", range: "2 bis 20", edges: "Klein: Tag und Nacht jagen sich. Groß: kaum merklich.", label: "Minuten pro Tag")]
        [Min(0.1f)] public float minutesPerDay = 6f;

        [Hint("Himmelsrichtung, in der die Sonne aufgeht. Dreht den ganzen Sonnenweg über die Szene.", unit: "°", range: "0 bis 360", label: "Sonnenaufgang")]
        [Range(0f, 360f)] public float sunrise = 90f;

        [Hint("Wie hoch die Sonne mittags steht.", unit: "°", range: "20 bis 75", edges: "Hoch: Sommer, kurze Schatten. Flach: Winter, lange Schatten.", label: "Mittagshöhe")]
        [Range(10f, 90f)] public float noonHeight = 55f;

        [Hint("Helligkeit der Sonne am Mittag (Intensity).", range: "0,8 bis 2", edges: "0: nur Himmelslicht.", label: "Helligkeit")]
        [Min(0f)] public float brightness = 1.1f;

        [Hint("An: der Nebel bekommt die Farbe des Horizonts zur Uhrzeit, so verschwimmt er glaubhaft.", edges: "Aus, wenn Nebel-Zonen oder der Knoten „Nebel“ eigene Farben setzen sollen.", label: "Nebelfarbe mitführen")]
        public bool tintFog = true;

        [Hint("Farbe der Sonne über den Tag: links Mitternacht, Mitte Mittag, rechts wieder Mitternacht.", label: "Sonnenfarbe")]
        public Gradient sunColor = Default(new Color(0.25f, 0.32f, 0.6f), new Color(1f, 0.52f, 0.28f), new Color(1f, 0.97f, 0.9f));

        [Hint("Farbe des Himmelslichts (Umgebungslicht von oben) über den Tag.", label: "Himmelslicht")]
        public Gradient skyColor = Default(new Color(0.05f, 0.07f, 0.16f), new Color(0.5f, 0.42f, 0.42f), new Color(0.42f, 0.52f, 0.68f));

        [Hint("Farbe des Lichts vom Horizont über den Tag. Prägt auch die Nebelfarbe.", label: "Horizontlicht")]
        public Gradient horizonColor = Default(new Color(0.06f, 0.08f, 0.14f), new Color(0.92f, 0.62f, 0.4f), new Color(0.62f, 0.68f, 0.74f));

        Light m_Sun;

        // How the sun and the scene's lighting looked before this component took over. Switching the component off or
        // removing it (also by Undo) gives exactly that back: adding Tageszeit never costs the lighting you had.
        [System.Serializable]
        struct Before
        {
            public LightType type; public Quaternion rotation; public Color color; public float intensity, shadowStrength;
            public Light sun; public AmbientMode ambientMode; public Color sky, equator, ground, fog;
        }
        [SerializeField, HideInInspector] Before m_Before;
        [SerializeField, HideInInspector] bool m_HasBefore;
        [SerializeField, HideInInspector] bool m_FogTinted;   // did Tageszeit write the fog colour? then giving back restores it

        /// <summary>A day gradient: night at both ends, dusk colour around 6 and 18 h, day colour at noon.</summary>
        static Gradient Default(Color night, Color dusk, Color day)
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(night, 0f), new GradientColorKey(night, 0.2f), new GradientColorKey(dusk, 0.26f),
                new GradientColorKey(day, 0.36f), new GradientColorKey(day, 0.64f), new GradientColorKey(dusk, 0.74f),
                new GradientColorKey(night, 0.8f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void OnEnable()
        {
            m_Sun = GetComponent<Light>();
            if (!m_HasBefore) Capture();
            if (m_Sun.type != LightType.Directional) m_Sun.type = LightType.Directional;
            Apply();
        }

        void Capture()
        {
            m_Before = new Before
            {
                type = m_Sun.type, rotation = transform.rotation, color = m_Sun.color, intensity = m_Sun.intensity,
                shadowStrength = m_Sun.shadowStrength, sun = RenderSettings.sun, ambientMode = RenderSettings.ambientMode,
                sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor,
            };
            m_HasBefore = true;
        }

        /// <summary>Puts the sun and the scene's lighting back to how they were before Tageszeit took over.</summary>
        public void Restore()
        {
            if (!m_HasBefore || !m_Sun) return;
            var b = m_Before;
            m_Sun.type = b.type; m_Sun.color = b.color; m_Sun.intensity = b.intensity;
            RestoreRotation(transform, b.rotation);
            m_Sun.shadowStrength = b.shadowStrength;
            RenderSettings.sun = b.sun; RenderSettings.ambientMode = b.ambientMode;
            RenderSettings.ambientSkyColor = b.sky; RenderSettings.ambientEquatorColor = b.equator; RenderSettings.ambientGroundColor = b.ground;
            if (m_FogTinted) RenderSettings.fogColor = b.fog;
            m_FogTinted = false;
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        static void RestoreRotation(Transform t, Quaternion r)
        {
#if UNITY_EDITOR
            // Unity forbids touching a Transform while it processes an Undo (Undo of Add Component lands here): right after.
            if (UnityEditor.Undo.isProcessing) { UnityEditor.EditorApplication.delayCall += () => { if (t) t.rotation = r; }; return; }
#endif
            t.rotation = r;
        }

        // Unchecked in the Inspector, or its object switched off: give the lighting back. A scene closing, Play
        // starting or ending and a script reload also disable components; those must not touch the lighting.
        void OnDisable() { if (!Closing && !Reloading && Mine) Restore(); }

        // Removed (Remove Component, or Undo of Add Component): give the lighting back, too.
        void OnDestroy() { if (!Closing && Mine) Restore(); }

        // RenderSettings belong to the active scene: an object of a scene that is being unloaded must not write into the next one.
        bool Mine => gameObject.scene.isLoaded && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene();

#if UNITY_EDITOR
        static bool s_SceneClosing, s_Reloading;
        static bool Reloading => s_Reloading;
        static bool Closing => s_SceneClosing || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying
                               || Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;

        [UnityEditor.InitializeOnLoadMethod]
        static void Hooks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (scene, removing) => s_SceneClosing = true;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += scene => s_SceneClosing = false;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => s_Reloading = true;   // statics start fresh afterwards
        }
#else
        static bool Closing => false;
        static bool Reloading => false;
#endif

        void Update()
        {
            if (Application.isPlaying && running)
            {
                hour = Mathf.Repeat(hour + Time.unscaledDeltaTime * 24f / (minutesPerDay * 60f), 24f);   // echte Minuten, wie versprochen
            }
            Apply();
        }

        void OnValidate() { if (isActiveAndEnabled) Apply(); }

        /// <summary>Writes sun, ambient and fog for <see cref="hour"/>.</summary>
        public void Apply()
        {
            if (!m_Sun) m_Sun = GetComponent<Light>();
            float t = Mathf.Repeat(hour, 24f) / 24f;
            // Sonnenbahn: um 6 Uhr am Horizont, um 12 Uhr am höchsten, nachts unter dem Boden (dann scheint sie nicht).
            float arc = (Mathf.Repeat(hour, 24f) - 6f) / 12f * 180f;         // 0° Aufgang … 180° Untergang
            float elevation = Mathf.Sin(arc * Mathf.Deg2Rad) * noonHeight;
            float azimuth = sunrise + arc;
            transform.rotation = Quaternion.Euler(elevation, azimuth - 90f, 0f) * Quaternion.identity;
            float up = Mathf.Clamp01(elevation / 10f);                       // weich ausblenden, wenn sie untergeht
            m_Sun.color = sunColor.Evaluate(t);
            m_Sun.intensity = brightness * up;
            m_Sun.shadowStrength = Mathf.Lerp(0.4f, 1f, up);

            RenderSettings.sun = m_Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var sky = skyColor.Evaluate(t); var horizon = horizonColor.Evaluate(t);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = horizon;
            RenderSettings.ambientGroundColor = Color.Lerp(horizon, Color.black, 0.6f);
            if (tintFog) { RenderSettings.fogColor = horizon; m_FogTinted = true; }
        }

        /// <summary>Hour as text, e.g. "16:30".</summary>
        public static string Clock(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }
}
