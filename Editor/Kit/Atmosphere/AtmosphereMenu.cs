using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>GameObject › Atmosphäre › …: zones and music as ready objects where the Scene view looks.</summary>
    static class AtmosphereMenu
    {
        static GameObject Make(string name, MenuCommand cmd)
        {
            var go = new GameObject(name);
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            var sv = SceneView.lastActiveSceneView;
            if (sv && !(cmd.context as GameObject)) go.transform.position = sv.pivot;
            Undo.RegisterCreatedObjectUndo(go, name);
            Selection.activeGameObject = go;
            return go;
        }

        static void Zone<T>(string name, MenuCommand cmd) where T : AtmosphereZone
        {
            var go = Make(name, cmd);
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(6f, 3f, 6f); box.center = new Vector3(0f, 1.5f, 0f);
            Undo.AddComponent<T>(go);
        }

        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Musik und Atmo", false, 1)] static void Music(MenuCommand c) => Undo.AddComponent<Kit.Music>(Make("Musik", c));
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Musik-Zone", false, 20)] static void MusicZone(MenuCommand c) => Zone<Kit.MusicZone>("Musik-Zone", c);
        [MenuItem(KitBrand.GameObjectMenu + "Atmosphäre/Nebel-Zone", false, 21)] static void FogZone(MenuCommand c) => Zone<Kit.FogZone>("Nebel-Zone", c);
    }
}
