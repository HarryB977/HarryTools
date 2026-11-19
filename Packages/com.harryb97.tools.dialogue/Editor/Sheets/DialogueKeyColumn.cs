using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Sheet column of the entry keys. Pulling only matches rows to entries that already exist, by key; it never adds or renames keys. Derives from <see cref="KeyColumn"/> because pushing requires one </summary>
    internal sealed class DialogueKeyColumn : KeyColumn, IPullKeyColumn {
        // ==================== VARIABLES ===================
        #region Private Variables
        private SharedTableData sharedData;
        #endregion

        #region Public Variables
        /// <summary> Rows of the last pull whose key matched no entry of the collection. Reset when a pull begins </summary>
        public int UnknownRows { get; private set; }

        /// <summary> What the pull before the push read. Its key cell notes are pushed back on the row of their key. Null pushes no notes </summary>
        public SheetPullCapture Capture { get; set; }

        /// <summary> The value holds the key; the note holds the cell note the pull before the push read for the key, so it moves with its row </summary>
        public override PushFields PushFields => PushFields.ValueAndNote;
        #endregion

        // ==================== PUSH ====================
        #region Push
        /// <summary> The key, and the note its cell had </summary>
        public override void PushCellData(SharedTableData.SharedTableEntry _keyEntry, IList<StringTableEntry> _tableEntries, out string _value, out string _note) {
            _value = _keyEntry.Key;
            _note = Capture?.Note(_keyEntry.Id, Capture.KeyColumn);
        }
        #endregion

        // ==================== PULL ====================
        #region Pull
        /// <inheritdoc/>
        public override void PullBegin(StringTableCollection _collection) {
            sharedData = _collection != null ? _collection.SharedData : null;
            UnknownRows = 0;
        }

        /// <summary> The existing entry with the key in the cell. Null, and counted in <see cref="UnknownRows"/>, when there is none </summary>
        public new SharedTableData.SharedTableEntry PullKey(string _cellValue, string _cellNote) {
            SharedTableData.SharedTableEntry _entry = sharedData != null && !string.IsNullOrEmpty(_cellValue) ? sharedData.GetEntry(_cellValue) : null;
            if (_entry == null) UnknownRows++;
            return _entry;
        }
        #endregion
    }
}
