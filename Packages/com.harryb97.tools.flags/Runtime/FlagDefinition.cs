using System;
using UnityEngine;

namespace HarryTools.Flags
{
    /// <summary>
    /// Definition of one flag: identity, scope, owning scene (for local flags), initial value and description.
    /// </summary>
    [Serializable]
    public sealed class FlagDefinition
    {
        // ==================== VARIABLES ====================

        [SerializeField] private string guid;
        [SerializeField] private string name;
        [SerializeField] private FlagScope scope;
        [SerializeField] private string sceneGuid;
        [SerializeField] private string sceneName;
        [SerializeField] private bool initialValue;
        [SerializeField] private string description;

        // ==================== PROPERTIES ====================

        /// <summary>Stable identifier (32 hex characters).</summary>
        public string Guid => guid;

        /// <summary>Readable name, unique within its scope.</summary>
        public string Name => name;

        public FlagScope Scope => scope;

        /// <summary>Guid of the owning scene asset. Empty for global flags.</summary>
        public string SceneGuid => sceneGuid;

        /// <summary>Last known name of the owning scene.</summary>
        public string SceneName => sceneName;

        public bool InitialValue => initialValue;

        public string Description => description;

        /// <summary>"Global / Door01" or "SceneName / Door01".</summary>
        public string DisplayPath => (scope == FlagScope.Global ? "Global" : sceneName) + " / " + name;

        // ==================== INTERNAL ====================

        internal FlagDefinition(string _guid, string _name, FlagScope _scope, string _sceneGuid, string _sceneName)
        {
            guid = _guid;
            name = _name;
            scope = _scope;
            sceneGuid = _sceneGuid ?? string.Empty;
            sceneName = _sceneName ?? string.Empty;
            description = string.Empty;
        }

        internal void SetName(string _name) => name = _name;
        internal void SetSceneGuid(string _sceneGuid) => sceneGuid = _sceneGuid ?? string.Empty;
        internal void SetSceneName(string _sceneName) => sceneName = _sceneName ?? string.Empty;
        internal void SetInitialValue(bool _value) => initialValue = _value;
        internal void SetDescription(string _description) => description = _description ?? string.Empty;
    }
}
