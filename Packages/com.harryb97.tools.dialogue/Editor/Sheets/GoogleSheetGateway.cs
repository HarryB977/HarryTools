using System;
using System.Collections.Generic;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Reaches the spreadsheet through the Google Sheets extension of the Localization package. Nothing is sent while the provider or the spreadsheet ID is missing </summary>
    internal sealed class GoogleSheetGateway : IDialogueSheetGateway {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TailFields = "userEnteredValue,note";
        private const string RowCountField = "gridProperties.rowCount";
        private const string ReadFields = "sheets.properties.sheetId,sheets.data.startRow,sheets.data.startColumn,sheets.data.rowData.values.formattedValue,sheets.data.rowData.values.note";

        private readonly SheetsServiceProvider provider;
        private readonly string spreadsheetId;
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        public GoogleSheetGateway(SheetsServiceProvider _provider, string _spreadsheetId) {
            provider = _provider;
            spreadsheetId = _spreadsheetId?.Trim();
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> True with an API key, which can only read public spreadsheets </summary>
        public bool IsReadOnly => provider != null && provider.Authentication == AuthenticationType.APIKey;

        /// <inheritdoc/>
        public string SpreadsheetId => spreadsheetId ?? "";

        /// <summary> Why the provider and spreadsheet ID cannot be used, or null when both are set </summary>
        internal static string ConfigurationError(SheetsServiceProvider _provider, string _spreadsheetId) {
            if (_provider == null) return "Set a Sheets Service Provider in Project Settings > HarryTools > Dialogue first.";
            if (_provider.Authentication == AuthenticationType.None) return "The Sheets Service Provider has no credentials. Choose OAuth or an API key in it first.";
            if (string.IsNullOrWhiteSpace(_spreadsheetId)) return "Set the spreadsheet ID in Project Settings > HarryTools > Dialogue first.";
            return null;
        }

        /// <inheritdoc/>
        public IReadOnlyList<(int Id, string Title)> GetSheets() {
            List<(int Id, string Title)> _sheets = new List<(int, string)>();
            foreach ((string _name, int _id) in Connect().GetSheets()) _sheets.Add((_id, _name));
            return _sheets;
        }

        /// <inheritdoc/>
        public int AddSheet(string _title) {
            GoogleSheets _google = Connect();
            if (IsReadOnly) throw new InvalidOperationException(SheetConnection.ReadOnlyMessage);
            return _google.AddSheet(_title, provider.NewSheetProperties ?? new NewSheetProperties());
        }

        /// <summary> Pushes through the extension. The tail requests are added to the same batch, after the extension's own, so the tail rows are written in the same request that clears the rows below the entries </summary>
        public void Push(int _sheetId, StringTableCollection _collection, IList<SheetColumn> _columns, SheetGrid _tail) {
            Connect();
            if (IsReadOnly) throw new InvalidOperationException(SheetConnection.ReadOnlyMessage);

            TailedSheets _google = new TailedSheets(provider) { SpreadSheetId = spreadsheetId };
            if (_tail != null && _tail.RowCount > 0) {
                // The extension grows the tab to one row per entry plus the header; the tail may need more
                int _rows = Math.Max(_google.GetRowCount(_sheetId), _collection.SharedData.Entries.Count + 1);
                _google.Tail = TailRequests(_sheetId, _columns, _tail, 1 + PushedRows(_collection), _rows);
            }
            _google.PushStringTableCollection(_sheetId, _collection, _columns);
        }

        /// <summary> Every cell of the tab. With OAuth only the tab is requested; an API key cannot use a data filter, so the whole spreadsheet is requested and the tab picked from it </summary>
        public SheetGrid Read(int _sheetId) {
            Connect();
            SheetsService _service = provider.Service;
            Sheet _sheet;

            if (provider.Authentication == AuthenticationType.OAuth) {
                GetSpreadsheetByDataFilterRequest _body = new GetSpreadsheetByDataFilterRequest {
                    DataFilters = new List<DataFilter> { new DataFilter { GridRange = new GridRange { SheetId = _sheetId } } }
                };
                SpreadsheetsResource.GetByDataFilterRequest _request = _service.Spreadsheets.GetByDataFilter(_body, spreadsheetId);
                _request.Fields = ReadFields;
                Spreadsheet _response = _request.Execute();
                _sheet = _response.Sheets != null && _response.Sheets.Count > 0 ? _response.Sheets[0] : null;
            } else {
                SpreadsheetsResource.GetRequest _request = _service.Spreadsheets.Get(spreadsheetId);
                _request.IncludeGridData = true;
                _request.Fields = ReadFields;
                Spreadsheet _response = _request.Execute();
                _sheet = null;
                if (_response.Sheets != null) {
                    foreach (Sheet _candidate in _response.Sheets) {
                        if (_candidate?.Properties?.SheetId == _sheetId) {
                            _sheet = _candidate;
                            break;
                        }
                    }
                }
            }
            if (_sheet == null) throw new Exception("The spreadsheet has no tab with the ID " + _sheetId + ".");

            SheetGrid _grid = new SheetGrid();
            if (_sheet.Data == null) return _grid;
            foreach (GridData _data in _sheet.Data) {
                if (_data?.RowData == null) continue;
                int _startRow = _data.StartRow ?? 0;
                int _startColumn = _data.StartColumn ?? 0;
                for (int _row = 0; _row < _data.RowData.Count; _row++) {
                    IList<CellData> _cells = _data.RowData[_row]?.Values;
                    if (_cells == null) continue;
                    for (int _column = 0; _column < _cells.Count; _column++) {
                        CellData _cell = _cells[_column];
                        if (_cell == null || (_cell.FormattedValue == null && _cell.Note == null)) continue;
                        _grid.Set(_startRow + _row, _startColumn + _column, _cell.FormattedValue, _cell.Note);
                    }
                }
            }
            return _grid;
        }

        /// <summary> Reads the tab list of the spreadsheet. With an API key the connection is reported as read only </summary>
        public SheetConnection Test() {
            string _error = ConfigurationError(provider, spreadsheetId);
            if (_error != null) return SheetConnection.Failed(_error);
            try {
                return SheetConnection.Reached(GetSheets().Count, IsReadOnly);
            } catch (Exception _exception) {
                return SheetConnection.Failed("Connection failed: " + DialogueSheetSync.Describe(_exception));
            }
        }

        /// <summary> The requests that write the tail from the given row down, one per column, value and note, growing the tab first when it has fewer rows </summary>
        internal static List<Request> TailRequests(int _sheetId, IList<SheetColumn> _columns, SheetGrid _tail, int _firstRow, int _rowCount) {
            List<Request> _requests = new List<Request>();
            if (_tail == null || _tail.RowCount == 0 || _columns == null) return _requests;

            int _end = _firstRow + _tail.RowCount;
            if (_end > _rowCount) {
                _requests.Add(new Request {
                    UpdateSheetProperties = new UpdateSheetPropertiesRequest {
                        Properties = new SheetProperties { SheetId = _sheetId, GridProperties = new GridProperties { RowCount = _end } },
                        Fields = RowCountField
                    }
                });
            }
            for (int _c = 0; _c < _columns.Count; _c++) {
                int _column = _columns[_c].ColumnIndex;
                List<RowData> _rows = new List<RowData>();
                for (int _r = 0; _r < _tail.RowCount; _r++) {
                    _rows.Add(new RowData {
                        Values = new List<CellData> { new CellData { UserEnteredValue = new ExtendedValue { StringValue = _tail.Value(_r, _column) }, Note = _tail.Note(_r, _column) } }
                    });
                }
                _requests.Add(new Request {
                    UpdateCells = new UpdateCellsRequest {
                        Range = new GridRange { SheetId = _sheetId, StartColumnIndex = _column, EndColumnIndex = _column + 1, StartRowIndex = _firstRow, EndRowIndex = _end },
                        Rows = _rows,
                        Fields = TailFields
                    }
                });
            }
            return _requests;
        }

        /// <summary> The rows the extension writes for the collection: one per entry that is not excluded from export </summary>
        internal static int PushedRows(StringTableCollection _collection) {
            int _count = 0;
            IReadOnlyList<SharedTableData.SharedTableEntry> _entries = _collection.SharedData.Entries;
            for (int _i = 0; _i < _entries.Count; _i++) {
                if (!_entries[_i].Metadata.HasMetadata<ExcludeEntryFromExport>()) _count++;
            }
            return _count;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private GoogleSheets Connect() {
            string _error = ConfigurationError(provider, spreadsheetId);
            if (_error != null) throw new InvalidOperationException(_error);
            return new GoogleSheets(provider) { SpreadSheetId = spreadsheetId };
        }
        #endregion

        // ==================== TYPES ====================
        #region Types
        /// <summary> The extension's connection, with extra requests sent in the same batch as its own </summary>
        private sealed class TailedSheets : GoogleSheets {
            /// <summary> Requests added after the extension's own. Null adds none </summary>
            public IList<Request> Tail;

            public TailedSheets(IGoogleSheetsService _provider) : base(_provider) { }

            protected override BatchUpdateSpreadsheetResponse SendBatchUpdateRequest(string _spreadsheetId, IList<Request> _requests) {
                if (Tail == null || Tail.Count == 0) return base.SendBatchUpdateRequest(_spreadsheetId, _requests);
                List<Request> _all = new List<Request>(_requests);
                _all.AddRange(Tail);
                return base.SendBatchUpdateRequest(_spreadsheetId, _all);
            }
        }
        #endregion
    }
}
