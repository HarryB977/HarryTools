using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test asset with SceneRef fields, plain and inside a [SerializeReference] list, for references from assets to scene objects.
    /// </summary>
    public class SceneRefAssetHolder : ScriptableObject
    {
        // ==================== VARIABLES ====================

        public SceneRef<GameObject> target = new SceneRef<GameObject>();
        [SerializeReference] public List<object> items = new List<object>();
    }
}
