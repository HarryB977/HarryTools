using UnityEngine;

namespace HarryTools.SceneRefs
{
    /// <summary>
    /// Hidden component that gives a scene object a stable internal guid and a readable, unique id.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class SceneId : MonoBehaviour
    {
        // ==================== VARIABLES ====================

        [SerializeField] private string guid;
        [SerializeField] private string id;

        // ==================== PROPERTIES ====================

        /// <summary>Internal, stable identifier (32 hex characters).</summary>
        public string Guid => guid;

        /// <summary>Readable identifier shown to the user.</summary>
        public string Id => id;

        // ==================== METHODS ====================

        internal void Set(string _guid, string _id)
        {
            guid = _guid;
            id = _id;
        }

        private void OnDestroy() => SceneRefs.Unregister(this);

        #region Hide Flags

        private void Reset() => Hide();

        private void OnValidate() => Hide();

        private void Hide() => hideFlags |= HideFlags.HideInInspector;

        #endregion
    }
}
