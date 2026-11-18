using System;
using UnityEngine.Localization.Metadata;

namespace HarryTools.Dialogue {
    /// <summary> The source locale text a translation was written or last reviewed against. Stored on the translation's table entry, so it travels with the tables </summary>
    [Serializable, Metadata(AllowedTypes = MetadataType.StringTableEntry, AllowMultiple = false)]
    public sealed class SourceSnapshot : IMetadata {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> The full source text at the moment of the snapshot </summary>
        public string SourceText;
        #endregion
    }
}
