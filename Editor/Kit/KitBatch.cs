using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Entry points for batch mode (Unity.exe -batchmode -executeMethod Kit.Editor.KitBatch.X).</summary>
    public static class KitBatch
    {
        public static void Compile() => Debug.Log("[KitBatch] compiled");
    }
}
