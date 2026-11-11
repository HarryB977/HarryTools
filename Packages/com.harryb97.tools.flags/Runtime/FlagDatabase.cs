using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Flags
{
    /// <summary>
    /// Project-wide list of flag definitions. Lives in a Resources folder so it is available at runtime.
    /// </summary>
    public sealed class FlagDatabase : ScriptableObject
    {
        // ==================== VARIABLES ====================

        /// <summary>Resources name of the database asset.</summary>
        public const string ResourcesName = "FlagDatabase";

        [SerializeField] private List<FlagDefinition> flags = new List<FlagDefinition>();

        private Dictionary<string, FlagDefinition> byGuid;

        // ==================== PROPERTIES ====================

        public IReadOnlyList<FlagDefinition> Flags => flags;

        internal List<FlagDefinition> MutableFlags => flags;

        // ==================== METHODS ====================

        public bool TryGet(string _guid, out FlagDefinition _flag)
        {
            if (byGuid == null) Rebuild();
            if (!string.IsNullOrEmpty(_guid)) return byGuid.TryGetValue(_guid, out _flag);
            _flag = null;
            return false;
        }

        /// <summary>Drops the lookup cache; call after changing <see cref="MutableFlags"/>.</summary>
        internal void Invalidate() => byGuid = null;

        // ==================== UNITY ====================

        private void OnEnable() => Rebuild();

        private void OnValidate() => Invalidate();

        // ==================== PRIVATE ====================

        private void Rebuild()
        {
            byGuid = new Dictionary<string, FlagDefinition>(flags.Count);
            foreach (FlagDefinition _flag in flags)
            {
                if (_flag != null && !string.IsNullOrEmpty(_flag.Guid)) byGuid[_flag.Guid] = _flag;
            }
        }
    }
}
