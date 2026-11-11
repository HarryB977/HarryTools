using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test component whose serialized data can never hold a SceneRef.
    /// </summary>
    [AddComponentMenu("")]
    public class PlainTestBehaviour : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        public int count;
        public List<Vector3> points = new List<Vector3>();
        public List<SceneRefPlainData> data = new List<SceneRefPlainData>();
        // Not serialized, so it does not count.
        [System.NonSerialized] public SceneRef<GameObject> ignored = new SceneRef<GameObject>();
    }
}
