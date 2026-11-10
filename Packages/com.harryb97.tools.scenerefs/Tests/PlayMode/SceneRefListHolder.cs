using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Test component with SceneRef fields inside a [SerializeReference] list, like ActionList and ConditionList.
    /// </summary>
    [AddComponentMenu("")]
    public class SceneRefListHolder : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        [SerializeReference] public List<object> items = new List<object>();
    }

    [Serializable]
    public class SceneRefListItem
    {
        // ==================== VARIABLES ====================

        public SceneRef<GameObject> target = new SceneRef<GameObject>();
    }
}
