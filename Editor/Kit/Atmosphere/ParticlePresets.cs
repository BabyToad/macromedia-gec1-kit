using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// GameObject › Atmosphäre › Partikel › …: ready particle systems for the usual moods. Each is an ordinary
    /// Particle System with an ordinary URP material: a starting point to tune in the Inspector, not a black box.
    /// Materials go to Assets/Partikel (made once, then reused), the textures come with the kit.
    /// </summary>
    public static class ParticlePresets
    {
        public enum Kind { Sparks, Smoke, Dust, Fireflies, Rain, Snow, Confetti, DustPuff, Embers }

        public const string DefaultMaterialFolder = "Assets/Partikel";
        /// <summary>Where preset materials go. Tests and tools point it elsewhere so they never touch a student's folder.</summary>
        public static string MaterialFolder = DefaultMaterialFolder;
        const string k_Unlit = "Universal Render Pipeline/Particles/Unlit";

        public static string Name(Kind k) => k switch
        {
            Kind.Sparks => "Funken", Kind.Smoke => "Rauch", Kind.Dust => "Staub im Licht", Kind.Fireflies => "Glühwürmchen",
            Kind.Rain => "Regen", Kind.Snow => "Schnee", Kind.Confetti => "Konfetti (Stoß)", Kind.DustPuff => "Staubwolke (Stoß)",
            _ => "Glut",
        };

        /// <summary>Creates the preset at <paramref name="position"/>. Used by the menu and by tests.</summary>
        public static ParticleSystem Create(Kind kind, Vector3 position, Transform parent = null)
        {
            var go = new GameObject(Name(kind));
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // erst fertig einstellen, dann spielen
            Configure(ps, kind);
            if (!IsBurst(kind)) ps.Play(true);
            return ps;
        }

        static void Configure(ParticleSystem ps, Kind kind)
        {
            var main = ps.main; var em = ps.emission; var shape = ps.shape;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            var col = ps.colorOverLifetime; var size = ps.sizeOverLifetime; var noise = ps.noise;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true; main.loop = true;
            em.rateOverTime = 10f;
            bool additive = false; string tex = "Partikel Weich";

            switch (kind)
            {
                case Kind.Sparks:
                    main.startLifetime = Range(0.4f, 0.9f); main.startSpeed = Range(3f, 7f); main.startSize = Range(0.05f, 0.1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.15f));
                    main.gravityModifier = 1.2f; em.rateOverTime = 45f;
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.05f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    col.enabled = true; col.color = Fade(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.25f, 0.05f), 0f, 0.2f);
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 1.5f;
                    additive = true;
                    break;
                case Kind.Embers:
                    main.startLifetime = Range(2f, 4f); main.startSpeed = Range(0.3f, 1f); main.startSize = Range(0.05f, 0.1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.35f, 0.1f));
                    main.gravityModifier = -0.15f; em.rateOverTime = 20f;
                    shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.4f; shape.rotation = new Vector3(-90f, 0f, 0f);
                    noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.8f;
                    col.enabled = true; col.color = Fade(Color.white, Color.white, 0.1f, 0.4f);
                    additive = true;
                    break;
                case Kind.Smoke:
                    main.startLifetime = Range(4f, 6f); main.startSpeed = Range(0.4f, 0.9f); main.startSize = Range(0.6f, 1f);
                    main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.55f); main.startRotation = Range(0f, Mathf.PI * 2f);
                    main.gravityModifier = -0.03f; em.rateOverTime = 6f;
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.3f; shape.rotation = new Vector3(-90f, 0f, 0f);
                    size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
                    noise.enabled = true; noise.strength = 0.3f; noise.frequency = 0.3f;
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = Range(-0.4f, 0.4f);
                    col.enabled = true; col.color = Fade(Color.white, Color.white, 0.15f, 0.5f);
                    tex = "Partikel Wolke";
                    break;
                case Kind.Dust:
                    main.startLifetime = Range(6f, 10f); main.startSpeed = Range(0.02f, 0.08f); main.startSize = Range(0.015f, 0.04f);
                    main.startColor = new Color(1f, 0.95f, 0.85f, 0.7f); main.prewarm = true; em.rateOverTime = 18f;
                    shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(6f, 3f, 6f);
                    noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.2f; noise.scrollSpeed = 0.05f;
                    col.enabled = true; col.color = Fade(Color.white, Color.white, 0.3f, 0.3f);
                    additive = true;
                    break;
                case Kind.Fireflies:
                    main.startLifetime = Range(5f, 8f); main.startSpeed = Range(0.05f, 0.2f); main.startSize = Range(0.1f, 0.16f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.4f), new Color(1f, 0.9f, 0.35f));
                    main.prewarm = true; em.rateOverTime = 7f;
                    shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(8f, 2f, 8f);
                    noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f;
                    col.enabled = true; col.color = Pulse();
                    additive = true;
                    break;
                case Kind.Rain:
                    main.startLifetime = 0.9f; main.startSpeed = Range(14f, 18f); main.startSize = Range(0.025f, 0.04f);
                    main.startColor = new Color(0.8f, 0.86f, 0.98f, 0.6f); main.prewarm = true; em.rateOverTime = 900f;
                    main.maxParticles = 3000;
                    shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24f, 0.1f, 24f);
                    shape.rotation = new Vector3(90f, 0f, 0f);                   // Partikel fliegen nach unten
                    ps.transform.position += Vector3.up * 10f;
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1f;
                    break;
                case Kind.Snow:
                    main.startLifetime = 12f; main.startSpeed = Range(0.4f, 0.9f); main.startSize = Range(0.03f, 0.08f);
                    main.startColor = new Color(1f, 1f, 1f, 0.9f); main.prewarm = true; em.rateOverTime = 140f;
                    main.maxParticles = 3000; main.gravityModifier = 0.02f;
                    shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24f, 0.1f, 24f);
                    shape.rotation = new Vector3(90f, 0f, 0f);
                    ps.transform.position += Vector3.up * 8f;
                    noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.25f;
                    break;
                case Kind.Confetti:
                    main.startLifetime = Range(2.5f, 3.5f); main.startSpeed = Range(5f, 9f); main.startSize = Range(0.07f, 0.13f);
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                                      new GradientColorKey(new Color(0.3f, 0.75f, 1f), 0.66f), new GradientColorKey(new Color(0.5f, 0.95f, 0.5f), 1f) },
                              new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                    main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
                    main.startRotation = Range(0f, Mathf.PI * 2f); main.gravityModifier = 0.9f;
                    em.rateOverTime = 0f;                                    // nur Stoß: Knoten „Partikel“ › Stoß
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 30f; shape.radius = 0.2f; shape.rotation = new Vector3(-90f, 0f, 0f);
                    var rot2 = ps.rotationOverLifetime; rot2.enabled = true; rot2.z = Range(-6f, 6f);
                    var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 2.5f; vel.dampen = 0.15f;   // flattert langsam herab
                    col.enabled = true; col.color = Fade(Color.white, Color.white, 0f, 0.15f);
                    tex = null;                                               // eckige Schnipsel
                    break;
                case Kind.DustPuff:
                    main.startLifetime = Range(0.7f, 1.2f); main.startSpeed = Range(1f, 2.5f); main.startSize = Range(0.3f, 0.6f);
                    main.startColor = new Color(0.62f, 0.56f, 0.48f, 0.6f); main.startRotation = Range(0f, Mathf.PI * 2f);
                    em.rateOverTime = 0f;
                    shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.3f; shape.rotation = new Vector3(-90f, 0f, 0f);
                    size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2f));
                    var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 0.3f; limit.dampen = 0.3f;
                    col.enabled = true; col.color = Fade(Color.white, Color.white, 0.05f, 0.6f);
                    tex = "Partikel Wolke";
                    break;
            }
            if (IsBurst(kind))
            {
                // Stoß-Vorlagen: ein ruhendes System, das nur auf „Stoß“ Teilchen macht. Nicht in Schleife, nicht beim Start,
                // und es rechnet auch außerhalb des Bildes weiter, damit „Verklungen“ immer kommt.
                main.loop = false; main.playOnAwake = false; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
            r.sharedMaterial = Material(kind, tex, additive);
        }

        /// <summary>Presets that only emit on the node's Stoß (Konfetti, Staubwolke).</summary>
        public static bool IsBurst(Kind k) => k == Kind.Confetti || k == Kind.DustPuff;

        static ParticleSystem.MinMaxCurve Range(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);

        // Fade in over fadeIn, out over the last fadeOut of the life; colour goes from a to b.
        static ParticleSystem.MinMaxGradient Fade(Color a, Color b, float fadeIn, float fadeOut)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.01f, fadeIn)),
                              new GradientAlphaKey(1f, 1f - Mathf.Max(0.01f, fadeOut)), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static ParticleSystem.MinMaxGradient Pulse()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.15f, 0.35f), new GradientAlphaKey(1f, 0.55f),
                              new GradientAlphaKey(0.2f, 0.75f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>The preset's URP particle material in Assets/Partikel: made once, then reused (and tunable there).</summary>
        static Material Material(Kind kind, string texture, bool additive)
        {
            Directory.CreateDirectory(MaterialFolder);
            var path = $"{MaterialFolder}/{Name(kind)}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            var shader = Shader.Find(k_Unlit);
            m = new Material(shader) { name = Name(kind) };
            if (texture != null)
            {
                var tex = Textures.Load(texture);
                if (tex) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            }
            // URP particle material: transparent; additive glows (sparks, fireflies), alpha blending covers (smoke, snow).
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);                   // 0 Alpha, 2 Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static class Textures
        {
            public static Texture2D Load(string name)
            {
                foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
                return null;
            }
        }

        static void Menu(Kind k, MenuCommand cmd)
        {
            var parent = cmd.context as GameObject;
            var sv = SceneView.lastActiveSceneView;
            var at = parent ? parent.transform.position : sv ? sv.pivot : Vector3.zero;
            var ps = Create(k, at, parent ? parent.transform : null);
            Undo.RegisterCreatedObjectUndo(ps.gameObject, Name(k));
            Selection.activeGameObject = ps.gameObject;
        }

        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Funken", false, 10)] static void M0(MenuCommand c) => Menu(Kind.Sparks, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Glut", false, 11)] static void M8(MenuCommand c) => Menu(Kind.Embers, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Rauch", false, 12)] static void M1(MenuCommand c) => Menu(Kind.Smoke, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Staub im Licht", false, 13)] static void M2(MenuCommand c) => Menu(Kind.Dust, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Glühwürmchen", false, 14)] static void M3(MenuCommand c) => Menu(Kind.Fireflies, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Regen", false, 15)] static void M4(MenuCommand c) => Menu(Kind.Rain, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Schnee", false, 16)] static void M5(MenuCommand c) => Menu(Kind.Snow, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Konfetti (Stoß)", false, 30)] static void M6(MenuCommand c) => Menu(Kind.Confetti, c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Partikel/Staubwolke (Stoß)", false, 31)] static void M7(MenuCommand c) => Menu(Kind.DustPuff, c);
    }
}
