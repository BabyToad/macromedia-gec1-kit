using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Owns the material copy the node Farbe makes when an object should glow but its material has no emission.
    /// It lives on that object and destroys the copy together with it, so spawning and removing glowing objects
    /// never piles up materials in memory. Added at runtime only, never saved.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class EmissionCopy : MonoBehaviour
    {
        static readonly HashSet<EmissionCopy> s_Live = new HashSet<EmissionCopy>();

        Material m_Copy;

        /// <summary>The copy this object glows with, made on demand (or the existing one, if it is still in use).</summary>
        public static Material For(Renderer r)
        {
            var owner = r.GetComponent<EmissionCopy>();
            if (!owner)
            {
                owner = r.gameObject.AddComponent<EmissionCopy>();
                owner.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
            }
            if (owner.m_Copy && r.sharedMaterial == owner.m_Copy) return owner.m_Copy;
            if (owner.m_Copy) Destroy(owner.m_Copy);                  // the renderer got another material since: the old copy goes
            owner.m_Copy = r.material;                                // Unity's per-object copy of the current material
            return owner.m_Copy;
        }

        /// <summary>How many copies the kit owns right now (for tests).</summary>
        public static int Count => s_Live.Count;

        void Awake() => s_Live.Add(this);

        void OnDestroy()
        {
            s_Live.Remove(this);
            if (m_Copy) Destroy(m_Copy);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => s_Live.Clear();
    }
}
