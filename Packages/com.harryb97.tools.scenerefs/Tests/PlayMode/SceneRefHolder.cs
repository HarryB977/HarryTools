using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test component with plain SceneRef fields.
    /// </summary>
    [AddComponentMenu("")]
    public class SceneRefHolder : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        public SceneRef<GameObject> target = new SceneRef<GameObject>();
        public SceneRef<AudioSource> audioSource = new SceneRef<AudioSource>();
        public SceneRef<Light> light = new SceneRef<Light>();
    }
}
