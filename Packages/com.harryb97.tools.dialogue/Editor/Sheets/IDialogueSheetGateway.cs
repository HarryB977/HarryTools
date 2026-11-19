using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;

namespace HarryTools.Dialogue.Editor {
    /// <summary> The only way the dialogue tools reach the spreadsheet. Every call may throw when the spreadsheet cannot be reached </summary>
    internal interface IDialogueSheetGateway {
        /// <summary> The ID and title of every tab of the spreadsheet </summary>
        IReadOnlyList<(int Id, string Title)> GetSheets();

        /// <summary> Adds a tab with the title and returns its ID </summary>
        int AddSheet(string _title);

        /// <summary> Writes the columns of the collection to the tab, header row included. Other columns of the tab are kept </summary>
        void Push(int _sheetId, StringTableCollection _collection, IList<SheetColumn> _columns);

        /// <summary> Every cell of the tab, header row included, with its value and note </summary>
        SheetGrid Read(int _sheetId);

        /// <summary> Null when the spreadsheet can be reached, otherwise the reason it cannot </summary>
        string Test();
    }

    /// <summary> The cells of a spreadsheet tab by row and column, from row 0, the header row. Cells outside the filled area read as null </summary>
    internal sealed class SheetGrid {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<List<Cell>> rows = new List<List<Cell>>();
        #endregion

        #region Public Variables
        /// <summary> Rows up to the last one with a cell, the header row included </summary>
        public int RowCount => rows.Count;
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The value of the cell. Null when it has none </summary>
        public string Value(int _row, int _column) {
            return Get(_row, _column).Value;
        }

        /// <summary> The note of the cell. Null when it has none </summary>
        public string Note(int _row, int _column) {
            return Get(_row, _column).Note;
        }

        /// <summary> Writes the value and the note of the cell, growing the grid as needed </summary>
        public void Set(int _row, int _column, string _value, string _note) {
            List<Cell> _cells = Row(_row);
            while (_cells.Count <= _column) _cells.Add(default);
            _cells[_column] = new Cell { Value = _value, Note = _note };
        }

        /// <summary> Writes the value of the cell, keeping its note </summary>
        public void SetValue(int _row, int _column, string _value) {
            Set(_row, _column, _value, Note(_row, _column));
        }

        /// <summary> Writes the note of the cell, keeping its value </summary>
        public void SetNote(int _row, int _column, string _note) {
            Set(_row, _column, Value(_row, _column), _note);
        }

        /// <summary> The values of row 0 </summary>
        public List<string> Headers() {
            List<string> _headers = new List<string>();
            if (rows.Count == 0) return _headers;
            for (int _i = 0; _i < rows[0].Count; _i++) _headers.Add(rows[0][_i].Value);
            return _headers;
        }

        /// <summary> A separate grid with the same cells </summary>
        public SheetGrid Copy() {
            SheetGrid _copy = new SheetGrid();
            for (int _i = 0; _i < rows.Count; _i++) _copy.rows.Add(new List<Cell>(rows[_i]));
            return _copy;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private Cell Get(int _row, int _column) {
            if (_row < 0 || _row >= rows.Count || _column < 0 || _column >= rows[_row].Count) return default;
            return rows[_row][_column];
        }

        private List<Cell> Row(int _row) {
            while (rows.Count <= _row) rows.Add(new List<Cell>());
            return rows[_row];
        }
        #endregion

        // ==================== TYPES ====================
        #region Types
        private struct Cell {
            public string Value;
            public string Note;
        }
        #endregion
    }
}
