using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;

namespace HarryTools.Dialogue.Editor {
    /// <summary> The only way the dialogue tools reach the spreadsheet. Every call may throw when the spreadsheet cannot be reached </summary>
    internal interface IDialogueSheetGateway {
        /// <summary> True when the access can only read, as with an API key: adding tabs and pushing are refused without a request </summary>
        bool IsReadOnly { get; }

        /// <summary> The ID and title of every tab of the spreadsheet </summary>
        IReadOnlyList<(int Id, string Title)> GetSheets();

        /// <summary> Adds a tab with the title and returns its ID </summary>
        int AddSheet(string _title);

        /// <summary> Writes the header and one row per entry of the collection to the tab, in the given columns only. In those columns, the rows below the written ones are cleared; other columns of the tab are kept </summary>
        void Push(int _sheetId, StringTableCollection _collection, IList<SheetColumn> _columns);

        /// <summary> Every cell of the tab, header row included, with its value and note </summary>
        SheetGrid Read(int _sheetId);

        /// <summary> Tries to reach the spreadsheet and describes the outcome </summary>
        SheetConnection Test();
    }

    /// <summary> The outcome of a connection test </summary>
    internal readonly struct SheetConnection {
        /// <summary> Message for a read-only access </summary>
        internal const string ReadOnlyMessage = "An API key can only pull. Pushing and creating tabs need OAuth.";

        /// <summary> True when the spreadsheet was reached </summary>
        public bool Connected { get; }

        /// <summary> True when it was reached with an access that can only read </summary>
        public bool ReadOnly { get; }

        /// <summary> The outcome in words </summary>
        public string Message { get; }

        private SheetConnection(bool _connected, bool _readOnly, string _message) {
            Connected = _connected;
            ReadOnly = _readOnly;
            Message = _message;
        }

        internal static SheetConnection Reached(int _tabs, bool _readOnly) {
            string _count = "The spreadsheet has " + _tabs + (_tabs == 1 ? " tab." : " tabs.");
            return new SheetConnection(true, _readOnly, _readOnly ? "Connected with an API key: pull only. " + _count + " Pushing and creating tabs need OAuth." : "Connected. " + _count);
        }

        internal static SheetConnection Failed(string _message) {
            return new SheetConnection(false, false, _message);
        }
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

        /// <summary> True when a row from the given one down has a cell with a value or a note </summary>
        public bool HasCells(int _fromRow) {
            for (int _row = System.Math.Max(0, _fromRow); _row < rows.Count; _row++) {
                for (int _column = 0; _column < rows[_row].Count; _column++) {
                    Cell _cell = rows[_row][_column];
                    if (!string.IsNullOrWhiteSpace(_cell.Value) || !string.IsNullOrEmpty(_cell.Note)) return true;
                }
            }
            return false;
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
