using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;

namespace HarryTools.Dialogue.Editor {
    /// <summary> What a pull read from a tab, kept in memory for the push that follows it in the same operation: the row of each entry it matched, so the cell notes go back on the row of their key, and the rows it could not match, so the push writes them back below the others </summary>
    internal sealed class SheetPullCapture {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly SheetGrid grid;
        private readonly List<string> headers;
        private readonly Dictionary<long, int> rows = new Dictionary<long, int>();
        private readonly List<int> unknownRows = new List<int>();
        #endregion

        #region Public Variables
        /// <summary> The position of the key column in the tab that was read </summary>
        public int KeyColumn { get; }

        /// <summary> Rows that matched no entry: a key the collection lacks, a repeated key after its first row, or a row with content but no key </summary>
        public int UnknownRows => unknownRows.Count;
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        internal SheetPullCapture(SheetGrid _grid, List<string> _headers, int _keyColumn) {
            grid = _grid;
            headers = _headers ?? new List<string>();
            KeyColumn = _keyColumn;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Records the row the entry was read from </summary>
        internal void AddKnown(long _id, int _row) {
            rows[_id] = _row;
        }

        /// <summary> Records a row that matched no entry </summary>
        internal void AddUnknown(int _row) {
            unknownRows.Add(_row);
        }

        /// <summary> The position the column had in the tab that was read, found by its header. -1 when the tab lacked it </summary>
        internal int ColumnOf(StringTableCollection _collection, SheetColumn _column) {
            return DialogueSheetColumns.HeaderIndex(_collection, _column, headers);
        }

        /// <summary> The note the cell of the entry had in the given column of the tab that was read. Null when the entry had no row there, or the column was missing </summary>
        internal string Note(long _id, int _column) {
            if (_column < 0 || grid == null || !rows.TryGetValue(_id, out int _row)) return null;
            return grid.Note(_row, _column);
        }

        /// <summary> The cells of the unknown rows in the order they were read, laid out by the push columns: row 0 is the first unknown row and each cell sits at the push position of its column, taken from where that column's header was. Columns the tab lacked are left empty </summary>
        internal SheetGrid Tail(StringTableCollection _collection, IList<SheetColumn> _columns) {
            SheetGrid _tail = new SheetGrid();
            if (grid == null || _columns == null) return _tail;

            List<(int From, int To)> _map = new List<(int, int)>();
            for (int _i = 0; _i < _columns.Count; _i++) _map.Add((ColumnOf(_collection, _columns[_i]), _columns[_i].ColumnIndex));

            for (int _r = 0; _r < unknownRows.Count; _r++) {
                for (int _c = 0; _c < _map.Count; _c++) {
                    int _from = _map[_c].From;
                    _tail.Set(_r, _map[_c].To, _from >= 0 ? grid.Value(unknownRows[_r], _from) : null, _from >= 0 ? grid.Note(unknownRows[_r], _from) : null);
                }
            }
            return _tail;
        }
        #endregion
    }
}
