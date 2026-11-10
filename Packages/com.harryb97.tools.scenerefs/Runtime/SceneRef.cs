using System;

namespace HarryTools.SceneRefs
{
    /// <summary>
    /// Reference to a scene object (GameObject or Component) that survives scene boundaries.
    /// Uses a direct reference when possible and falls back to the internal guid registered by <see cref="SceneId"/>.
    /// </summary>
    [Serializable]
    public sealed class SceneRef<T> : SceneRefBase where T : UnityEngine.Object
    {
        // ==================== CONSTRUCTORS ====================

        public SceneRef() { }

        /// <summary>Creates a reference with only the direct reference set.</summary>
        public SceneRef(T _direct) { direct = _direct; }

        // ==================== PROPERTIES ====================

        /// <summary>The referenced object, or null when empty, not loaded or missing.</summary>
        public T Value => ResolveObject() as T;

        internal override Type TargetType => typeof(T);
    }
}
