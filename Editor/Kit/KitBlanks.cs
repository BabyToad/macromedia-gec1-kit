using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Rohlinge: plain objects to animate by hand (Day 4). No Animator, clip or graph – that is the students' work.
    /// </summary>
    public static class KitBlanks
    {
        [MenuItem("Tools/Kit/Rohling „Truhe“ bauen")]
        static void TruheMenu()
        {
            var view = SceneView.lastActiveSceneView;
            var truhe = BuildTruhe(view ? view.pivot : Vector3.zero);
            Selection.activeGameObject = truhe;
        }

        /// <summary>
        /// Truhe at <paramref name="at"/>: Kiste (the box) and Scharnier (the hinge at the back top edge) with the
        /// Deckel. Rotating Scharnier around X opens the lid (positive X opens it). Undo-able; unique name on repeat.
        /// </summary>
        public static GameObject BuildTruhe(Vector3 at)
        {
            var root = new GameObject(GameObjectUtility.GetUniqueNameForSibling(null, "Truhe"));
            root.transform.position = at;
            Cube("Kiste", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.6f));
            var hinge = new GameObject("Scharnier").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(0f, 0.6f, 0.3f);
            Cube("Deckel", hinge, new Vector3(0f, 0.05f, -0.3f), new Vector3(1f, 0.1f, 0.6f));
            Undo.RegisterCreatedObjectUndo(root, "Rohling „Truhe“ bauen");
            return root;
        }

        static void Cube(string name, Transform parent, Vector3 localPos, Vector3 scale)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);   // Box Collider bleibt dran
            c.name = name;
            c.transform.SetParent(parent, false);
            c.transform.localPosition = localPos;
            c.transform.localScale = scale;
            c.GetComponent<Renderer>().sharedMaterial = KitMaterials.Default;
        }
    }
}
