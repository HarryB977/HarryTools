using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>Unrelated MonoBehaviour with the same field names as SceneId. The YAML reader must ignore it.</summary>
    [AddComponentMenu("")]
    public sealed class SceneIndexerTestBehaviour : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        public string guid = "ffffffffffffffffffffffffffffffff";
        public string id = "NotAnId";
    }
}
