using System;
using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Tests {
    /// <summary> In-memory spreadsheet: one grid of values and notes per tab, header row included. Pushes write the cells the way the Google Sheets extension does. Any call can be made to fail </summary>
    internal sealed class FakeSheetGateway : IDialogueSheetGateway {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<Tab> sheets = new List<Tab>();
        // Starts at 0, which is a valid tab ID
        private int nextId;
        #endregion

        #region Public Variables
        /// <summary> When set, every call throws with this message </summary>
        public string FailAll;

        /// <summary> Reads, pushes and additions of tabs with these titles throw </summary>
        public readonly HashSet<string> FailingTitles = new HashSet<string>();

        /// <summary> Pushes throw after the columns produced their cells, as when the request is rejected </summary>
        public bool FailPushes;

        public int AddSheetCalls { get; private set; }
        public int PushCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public int SheetCount => sheets.Count;
        #endregion

        // ==================== GATEWAY ====================
        #region Gateway
        public IReadOnlyList<(int Id, string Title)> GetSheets() {
            ThrowIfFailing(null);
            List<(int Id, string Title)> _list = new List<(int, string)>();
            for (int _i = 0; _i < sheets.Count; _i++) _list.Add((sheets[_i].Id, sheets[_i].Title));
            return _list;
        }

        public int AddSheet(string _title) {
            ThrowIfFailing(_title);
            if (sheets.Exists(_tab => _tab.Title == _title)) throw new InvalidOperationException("A tab named " + _title + " already exists.");
            AddSheetCalls++;
            Tab _new = new Tab { Id = nextId++, Title = _title, Grid = new SheetGrid() };
            sheets.Add(_new);
            return _new.Id;
        }

        public void Push(int _sheetId, StringTableCollection _collection, IList<SheetColumn> _columns) {
            Tab _tab = Get(_sheetId);
            ThrowIfFailing(_tab.Title);

            // Same call order as the extension: begin and header per column, every cell row by row, then end, all before sending
            SheetGrid _grid = _tab.Grid.Copy();
            foreach (SheetColumn _column in _columns) {
                _column.PushBegin(_collection);
                _column.PushHeader(_collection, out string _header, out string _headerNote);
                _grid.Set(0, _column.ColumnIndex, _header, _headerNote);
            }
            int _row = 1;
            foreach (LocalizationTableCollection.Row<StringTableEntry> _entryRow in _collection.GetRowEnumeratorUnsorted()) {
                if (_entryRow.KeyEntry.Metadata.HasMetadata<ExcludeEntryFromExport>()) continue;
                foreach (SheetColumn _column in _columns) {
                    _column.PushCellData(_entryRow.KeyEntry, _entryRow.TableEntries, out string _value, out string _note);
                    switch (_column.PushFields) {
                        case PushFields.Value: _grid.SetValue(_row, _column.ColumnIndex, _value); break;
                        case PushFields.Note: _grid.SetNote(_row, _column.ColumnIndex, _note); break;
                        default: _grid.Set(_row, _column.ColumnIndex, _value, _note); break;
                    }
                }
                _row++;
            }
            foreach (SheetColumn _column in _columns) _column.PushEnd();

            if (FailPushes) throw new InvalidOperationException("The push was rejected.");
            PushCalls++;
            _tab.Grid = _grid;
        }

        public SheetGrid Read(int _sheetId) {
            Tab _tab = Get(_sheetId);
            ThrowIfFailing(_tab.Title);
            ReadCalls++;
            return _tab.Grid.Copy();
        }

        public string Test() {
            return FailAll;
        }
        #endregion

        // ==================== SHEET EDITS ====================
        #region Sheet Edits
        /// <summary> The grid of the tab with the title, for direct edits. Null when there is none </summary>
        public SheetGrid Grid(string _title) {
            return sheets.Find(_tab => _tab.Title == _title)?.Grid;
        }

        /// <summary> Replaces the grid of the tab with the title </summary>
        public void Replace(string _title, SheetGrid _grid) {
            sheets.Find(_tab => _tab.Title == _title).Grid = _grid;
        }

        /// <summary> The ID of the tab with the title, or -1 </summary>
        public int IdOf(string _title) {
            Tab _tab = sheets.Find(_t => _t.Title == _title);
            return _tab != null ? _tab.Id : -1;
        }

        /// <summary> The value in the row of the key, in the column with the header </summary>
        public string Cell(string _title, string _key, string _header) {
            SheetGrid _grid = Grid(_title);
            return _grid.Value(RowOf(_grid, _key), ColumnOf(_grid, _header));
        }

        /// <summary> Writes the value in the row of the key, in the column with the header, as a translator would </summary>
        public void SetCell(string _title, string _key, string _header, string _value) {
            SheetGrid _grid = Grid(_title);
            _grid.SetValue(RowOf(_grid, _key), ColumnOf(_grid, _header), _value);
        }

        /// <summary> Adds a row below the others with the key and a value in the column with the header </summary>
        public void AddRow(string _title, string _key, string _header, string _value) {
            SheetGrid _grid = Grid(_title);
            int _row = _grid.RowCount;
            _grid.SetValue(_row, ColumnOf(_grid, KeyColumn.ColumnHeader), _key);
            _grid.SetValue(_row, ColumnOf(_grid, _header), _value);
        }

        public static int ColumnOf(SheetGrid _grid, string _header) {
            List<string> _headers = _grid.Headers();
            int _index = _headers.IndexOf(_header);
            if (_index < 0) throw new ArgumentException("No column " + _header);
            return _index;
        }

        public static int RowOf(SheetGrid _grid, string _key) {
            int _column = ColumnOf(_grid, KeyColumn.ColumnHeader);
            for (int _row = 1; _row < _grid.RowCount; _row++) {
                if (_grid.Value(_row, _column) == _key) return _row;
            }
            throw new ArgumentException("No row " + _key);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void ThrowIfFailing(string _title) {
            if (FailAll != null) throw new InvalidOperationException(FailAll);
            if (_title != null && FailingTitles.Contains(_title)) throw new InvalidOperationException("The tab " + _title + " cannot be reached.");
        }

        private Tab Get(int _sheetId) {
            ThrowIfFailing(null);
            Tab _tab = sheets.Find(_t => _t.Id == _sheetId);
            if (_tab == null) throw new InvalidOperationException("No tab with the ID " + _sheetId);
            return _tab;
        }
        #endregion

        // ==================== TYPES ====================
        #region Types
        private sealed class Tab {
            public int Id;
            public string Title;
            public SheetGrid Grid;
        }
        #endregion
    }

    /// <summary> In-memory record of the tabs, so tests never write the project's sheet settings </summary>
    internal sealed class FakeSheetTabs : IDialogueSheetTabs {
        private readonly Dictionary<string, int> tabs = new Dictionary<string, int>();

        public int SaveCalls { get; private set; }

        public bool TryGetTab(string _collectionGuid, out int _sheetId) {
            return tabs.TryGetValue(_collectionGuid ?? "", out _sheetId);
        }

        public void SetTab(string _collectionGuid, int _sheetId) {
            tabs[_collectionGuid] = _sheetId;
        }

        public void Save() {
            SaveCalls++;
        }
    }
}
