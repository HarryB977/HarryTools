using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test asset with a SceneRef field, for references from assets to scene objects.
    /// </summary>
    public class SceneRefAssetHolder : ScriptableObject
    {
        // ==================== VARIABLES ====================

        public SceneRef<GameObject> target = new SceneRef<GameObject>();
    }
}
