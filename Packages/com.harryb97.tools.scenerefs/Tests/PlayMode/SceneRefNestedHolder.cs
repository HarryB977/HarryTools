using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test component with SceneRefs nested in serializable classes and lists, next to plain data that cannot hold one.
    /// </summary>
    [AddComponentMenu("")]
    public class SceneRefNestedHolder : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        public List<Vector3> points = new List<Vector3>();
        public List<SceneRefPlainData> data = new List<SceneRefPlainData>();
        public List<SceneRefWrapper> wrappers = new List<SceneRefWrapper>();
        [SerializeField] private SceneRefWrapper single = new SceneRefWrapper();

        public SceneRefWrapper Single => single;
    }

    [Serializable]
    public class SceneRefWrapper
    {
        // ==================== VARIABLES ====================

        public string note;
        public SceneRef<GameObject>[] targets = new SceneRef<GameObject>[0];
    }

    [Serializable]
    public struct SceneRefPlainData
    {
        // ==================== VARIABLES ====================

        public int value;
        public Vector3 position;
        public GameObject reference;
    }
}
