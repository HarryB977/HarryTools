using System;
using UnityEngine.Localization.Metadata;

namespace HarryTools.Dialogue {
    /// <summary> The translation text as it was in the spreadsheet after the last push or pull. Stored on the translation's table entry, so a pull can tell which side changed since then </summary>
    [Serializable, Metadata(AllowedTypes = MetadataType.StringTableEntry, AllowMultiple = false)]
    public sealed class SheetSyncSnapshot : IMetadata {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> The text both sides agreed on at the last sync </summary>
        public string SyncedText;
        #endregion
    }
}
