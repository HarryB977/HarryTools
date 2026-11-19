using System;
using System.Collections.Generic;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Reaches the spreadsheet through the Google Sheets extension of the Localization package. Nothing is sent while the provider or the spreadsheet ID is missing </summary>
    internal sealed class GoogleSheetGateway : IDialogueSheetGateway {
        // ==================== VARIABLES ===================
        #region Private Variables
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
        /// <summary> Why the provider and spreadsheet ID cannot be used, or null when both are set </summary>
        internal static string ConfigurationError(SheetsServiceProvider _provider, string _spreadsheetId) {
            if (_provider == null) return "Set a Sheets Service Provider in Project Settings > HarryTools > Dialogue first.";
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
            return _google.AddSheet(_title, provider.NewSheetProperties ?? new NewSheetProperties());
        }

        /// <inheritdoc/>
        public void Push(int _sheetId, StringTableCollection _collection, IList<SheetColumn> _columns) {
            Connect().PushStringTableCollection(_sheetId, _collection, _columns);
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

        /// <summary> Reads the tab list of the spreadsheet. Null when it worked, otherwise the reason it did not </summary>
        public string Test() {
            string _error = ConfigurationError(provider, spreadsheetId);
            if (_error != null) return _error;
            try {
                GetSheets();
                return null;
            } catch (Exception _exception) {
                return "Connection failed: " + _exception.GetBaseException().Message;
            }
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
    }
}
