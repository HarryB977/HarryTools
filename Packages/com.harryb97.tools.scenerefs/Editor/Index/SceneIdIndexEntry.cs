using System;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// One SceneId of the project as stored in the index.
    /// </summary>
    [Serializable]
    internal struct SceneIdIndexEntry
    {
        // ==================== VARIABLES ====================

        /// <summary>Internal guid of the SceneId.</summary>
        public string Guid;

        /// <summary>Visible id.</summary>
        public string Id;

        /// <summary>Asset guid of the scene, or "unsaved:" + handle for a loaded scene that was never saved.</summary>
        public string SceneGuid;

        public string SceneName;

        /// <summary>Names from the root to the object, separated by '/'.</summary>
        public string HierarchyPath;

        /// <summary>Full type names of the components of the object, without Transform and SceneId. Null when unknown (read from the scene file).</summary>
        public string[] ComponentTypes;

        // Unity serializes a null array as empty, so the index keeps this flag to restore the null.
        [UnityEngine.SerializeField] internal bool typesUnknown;
    }
}
