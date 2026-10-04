using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kit.Editor
{
    /// <summary>
    /// Materials for what the kit menus build, on whatever render pipeline the project uses (URP in the course).
    /// Never a hard-coded built-in shader name: in URP those render pink.
    /// </summary>
    public static class KitMaterials
    {
        static RenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline;

        /// <summary>The active pipeline's default lit shader (URP: Universal Render Pipeline/Lit).</summary>
        public static Shader DefaultShader => Pipeline ? Pipeline.defaultShader : Shader.Find("Standard");

        /// <summary>The active pipeline's default material.</summary>
        public static Material Default => Pipeline ? Pipeline.defaultMaterial : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

        /// <summary>
        /// A plain coloured material saved at <paramref name="path"/>. An existing material there is only replaced when
        /// <paramref name="replace"/> is set (explicit "Überschreiben"); otherwise the new one gets a free name next to it.
        /// </summary>
        public static Material Colored(string path, Color color, bool replace)
        {
            var mat = new Material(DefaultShader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old && replace) { EditorUtility.CopySerialized(mat, old); Object.DestroyImmediate(mat); EditorUtility.SetDirty(old); return old; }
            if (System.IO.File.Exists(path)) path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>A shipped material by GUID, or the pipeline's default if it is not in the project.</summary>
        public static Material ShippedOrDefault(string guid)
        {
            var path = KitPaths.ByGuid(guid);
            var mat = path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
            return mat ? mat : Default;
        }
    }
}
