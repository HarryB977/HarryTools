using System.Collections.Generic;
using System.Reflection;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueSheetColumnsTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/SheetDialogues";
        private const string TablesFolder = TempFolder + "/SheetTables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private const string SheetsSettingsPath = "ProjectSettings/HarryToolsDialogueSheets.asset";

        private static readonly LocaleIdentifier Source = new LocaleIdentifier(SourceCode);
        private static readonly LocaleIdentifier Other = new LocaleIdentifier(OtherCode);

        private DialogueSettings settings;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            TestLocales.RequireOrIgnore(SourceCode, OtherCode);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(AssetsFolder);
        }

        [TearDown]
        public void TearDown() {
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            // One by one, so Localization drops each table from its caches and Addressables
            DeleteContents(TablesFolder);
            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
        }

        private static void DeleteContents(string _folder) {
            if (!AssetDatabase.IsValidFolder(_folder)) return;
            // Collections first: deleting one removes it from the project before its tables go
            foreach (string _guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { _folder })) {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(_guid));
            }
            foreach (string _guid in AssetDatabase.FindAssets("", new[] { _folder })) {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (!string.IsNullOrEmpty(_path)) AssetDatabase.DeleteAsset(_path);
            }
        }

        /// <summary> A dialogue with one line per source text, whose collection holds those texts in the source locale </summary>
        private static DialogueAsset NewDialogue(out List<LineNode> _lines, params string[] _sources) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/Sheet.asset");
            _lines = new List<LineNode>();
            for (int _i = 0; _i < _sources.Length; _i++) _lines.Add(_asset.AddNode<LineNode>());
            _asset.EnsureIds();
            for (int _i = 0; _i < _sources.Length; _i++) {
                Assert.IsTrue(DialogueTables.SetSourceText(_asset, _lines[_i].Text, _lines[_i].Id, _sources[_i]));
            }
            return _asset;
        }

        /// <summary> A line whose translation was written against an older source text </summary>
        private static StringTableCollection OutdatedLine(out LineNode _line, out DialogueAsset _asset) {
            _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            _line = _lines[0];
            Assert.IsTrue(DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola"));
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello there"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            return _collection;
        }

        private static SharedTableData.SharedTableEntry Entry(StringTableCollection _collection, string _key) {
            return _collection.SharedData.GetEntry(_key);
        }

        private static StringTable Table(StringTableCollection _collection, LocaleIdentifier _locale) {
            return _collection.GetTable(_locale) as StringTable;
        }

        /// <summary> A sheet row, indexed by column position </summary>
        private sealed class SheetRow {
            public string[] Values;
            public string[] Notes;
        }

        /// <summary> Runs the push calls of the columns in the order the extension makes them, without sending anything. Commits the pushed texts as synced unless told otherwise </summary>
        private static List<SheetRow> Push(StringTableCollection _collection, List<SheetColumn> _columns, out string[] _headers, bool _commit = true) {
            int _width = 0;
            foreach (SheetColumn _column in _columns) _width = Mathf.Max(_width, _column.ColumnIndex + 1);

            _headers = new string[_width];
            foreach (SheetColumn _column in _columns) {
                _column.PushBegin(_collection);
                _column.PushHeader(_collection, out _headers[_column.ColumnIndex], out _);
            }

            List<SheetRow> _rows = new List<SheetRow>();
            foreach (LocalizationTableCollection.Row<StringTableEntry> _row in _collection.GetRowEnumeratorUnsorted()) {
                SheetRow _sheetRow = new SheetRow { Values = new string[_width], Notes = new string[_width] };
                foreach (SheetColumn _column in _columns) {
                    _column.PushCellData(_row.KeyEntry, _row.TableEntries, out _sheetRow.Values[_column.ColumnIndex], out _sheetRow.Notes[_column.ColumnIndex]);
                }
                _rows.Add(_sheetRow);
            }
            foreach (SheetColumn _column in _columns) _column.PushEnd();
            if (_commit) DialogueSheetColumns.CommitPush(_columns);
            return _rows;
        }

        /// <summary> Runs the pull calls of the columns over the rows, in the order the extension makes them: the key first, then every other column of the row </summary>
        private static void Pull(StringTableCollection _collection, List<SheetColumn> _columns, List<SheetRow> _rows) {
            foreach (SheetColumn _column in _columns) _column.PullBegin(_collection);
            IPullKeyColumn _key = (IPullKeyColumn)_columns[0];
            int _keyIndex = _columns[0].ColumnIndex;
            foreach (SheetRow _row in _rows) {
                SharedTableData.SharedTableEntry _entry = _key.PullKey(_row.Values[_keyIndex], _row.Notes[_keyIndex]);
                if (_entry == null) continue;
                for (int _i = 1; _i < _columns.Count; _i++) {
                    int _index = _columns[_i].ColumnIndex;
                    _columns[_i].PullCellData(_entry, _index < _row.Values.Length ? _row.Values[_index] : null, _index < _row.Notes.Length ? _row.Notes[_index] : null);
                }
            }
            foreach (SheetColumn _column in _columns) _column.PullEnd();
        }

        private static List<SheetColumn> PullColumns(StringTableCollection _collection, string[] _headers) {
            List<SheetColumn> _columns = DialogueSheetColumns.ForPull(_collection, _headers, out string _error);
            Assert.IsNotNull(_columns, _error);
            return _columns;
        }

        /// <summary> Writes a cell of the row of the key, in the column with the header </summary>
        private static void SetCell(List<SheetRow> _rows, string[] _headers, string _key, string _header, string _value) {
            int _column = System.Array.IndexOf(_headers, _header);
            Assert.GreaterOrEqual(_column, 0, _header);
            _rows.Find(_row => _row.Values[0] == _key).Values[_column] = _value;
        }

        private static DialogueTranslationColumn TranslationColumn(List<SheetColumn> _columns, LocaleIdentifier _locale) {
            return (DialogueTranslationColumn)_columns.Find(_c => _c is DialogueTranslationColumn _t && _t.LocaleIdentifier == _locale);
        }

        private static SheetSyncSnapshot Synced(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            return Table(_collection, _locale).GetEntry(Entry(_collection, _key).Id)?.GetMetadata<SheetSyncSnapshot>();
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void KeyColumn_Pull_UnknownKey_IgnoredAndCounted_NoCreate_NoRename() {
            StringTableCollection _collection = DialogueTables.Linked(NewDialogue(out List<LineNode> _lines, "Hello"));
            SharedTableData.SharedTableEntry _known = Entry(_collection, _lines[0].Id);
            int _count = _collection.SharedData.Entries.Count;

            DialogueKeyColumn _column = new DialogueKeyColumn();
            _column.PullBegin(_collection);

            Assert.AreSame(_known, _column.PullKey(_lines[0].Id, null));
            Assert.IsNull(_column.PullKey("unknown", null));
            Assert.IsNull(_column.PullKey("renamed", _known.Id.ToString()), "A row is matched by its key, never renamed through its note");
            Assert.AreEqual(2, _column.UnknownRows);

            Assert.AreEqual(_count, _collection.SharedData.Entries.Count);
            Assert.AreEqual(_lines[0].Id, _known.Key);
            Assert.IsNull(Entry(_collection, "unknown"));
            Assert.IsNull(Entry(_collection, "renamed"));

            // Through the interface, as the extension calls it
            IPullKeyColumn _asPullKey = _column;
            Assert.IsNull(_asPullKey.PullKey("unknown", null));
            Assert.AreEqual(3, _column.UnknownRows);

            _column.PullBegin(_collection);
            Assert.AreEqual(0, _column.UnknownRows);
        }

        [Test]
        public void TranslationColumn_Pull_WritesAndStamps() {
            StringTableCollection _collection = OutdatedLine(out LineNode _line, out _);
            List<SheetRow> _rows = Push(_collection, DialogueSheetColumns.ForPush(_collection), out string[] _headers);
            Assert.AreEqual("Hola", Synced(_collection, _line.Id, Other).SyncedText, "A committed push records the pushed text");
            SetCell(_rows, _headers, _line.Id, Other.ToString(), "Hola de nuevo");

            List<SheetColumn> _columns = PullColumns(_collection, _headers);
            Pull(_collection, _columns, _rows);

            Assert.AreEqual("Hola de nuevo", DialogueTranslations.TextOf(_collection, _line.Id, Other));
            Assert.AreEqual("Hola de nuevo", Synced(_collection, _line.Id, Other).SyncedText);
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello there", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
            Assert.AreEqual("Hello there", DialogueTranslations.TextOf(_collection, _line.Id, Source), "The source text is never written");
            Assert.AreEqual(0, TranslationColumn(_columns, Other).Conflicts, "Only the sheet changed");
        }

        [Test]
        public void TranslationColumn_Pull_LocalOnlyEdit_Survives() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            List<SheetRow> _rows = Push(_collection, DialogueSheetColumns.ForPush(_collection), out string[] _headers);

            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola, editado"));
            List<SheetColumn> _columns = PullColumns(_collection, _headers);
            Pull(_collection, _columns, _rows);

            Assert.AreEqual("Hola, editado", DialogueTranslations.TextOf(_collection, _lines[0].Id, Other));
            Assert.AreEqual(0, TranslationColumn(_columns, Other).Conflicts);
        }

        [Test]
        public void TranslationColumn_Pull_BothChanged_SheetWins_ConflictCounted() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            List<SheetRow> _rows = Push(_collection, DialogueSheetColumns.ForPush(_collection), out string[] _headers);

            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola local"));
            SetCell(_rows, _headers, _lines[0].Id, Other.ToString(), "Hola hoja");
            List<SheetColumn> _columns = PullColumns(_collection, _headers);
            Pull(_collection, _columns, _rows);

            Assert.AreEqual("Hola hoja", DialogueTranslations.TextOf(_collection, _lines[0].Id, Other));
            Assert.AreEqual(1, TranslationColumn(_columns, Other).Conflicts);

            // Pulling the same sheet again keeps a newer local edit and finds no conflict
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola local otra vez"));
            Pull(_collection, _columns, _rows);
            Assert.AreEqual("Hola local otra vez", DialogueTranslations.TextOf(_collection, _lines[0].Id, Other));
            Assert.AreEqual(0, TranslationColumn(_columns, Other).Conflicts);
        }

        [Test]
        public void TranslationColumn_Pull_FirstSync_NoSyncedText() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello", "Bye", "Thanks");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[2].Text, _lines[2].Id, Other, "Gracias"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            // Pushed but never committed, as when the push fails: nothing counts as synced
            List<SheetRow> _rows = Push(_collection, DialogueSheetColumns.ForPush(_collection), out string[] _headers, false);
            Assert.IsNull(Synced(_collection, _lines[0].Id, Other));

            SetCell(_rows, _headers, _lines[0].Id, Other.ToString(), "Hola hoja");
            SetCell(_rows, _headers, _lines[1].Id, Other.ToString(), "Adios");
            List<SheetColumn> _columns = PullColumns(_collection, _headers);
            Pull(_collection, _columns, _rows);

            Assert.AreEqual("Hola hoja", DialogueTranslations.TextOf(_collection, _lines[0].Id, Other), "Without a synced text the sheet wins");
            Assert.AreEqual("Adios", DialogueTranslations.TextOf(_collection, _lines[1].Id, Other));
            Assert.AreEqual("Gracias", DialogueTranslations.TextOf(_collection, _lines[2].Id, Other));
            Assert.AreEqual(1, TranslationColumn(_columns, Other).Conflicts, "Only the overwritten local translation is a conflict");
            Assert.AreEqual("Gracias", Synced(_collection, _lines[2].Id, Other).SyncedText, "A cell equal to the local text is recorded as synced");
        }

        [Test]
        public void Pull_EmptyCell_KeepsLocalTranslation() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello", "Bye");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            DialogueTranslationColumn _column = new DialogueTranslationColumn { LocaleIdentifier = Other };

            _column.PullBegin(_collection);
            foreach (string _blank in new[] { "", null, "   ", "\n" }) {
                _column.PullCellData(Entry(_collection, _lines[0].Id), _blank, null);
                _column.PullCellData(Entry(_collection, _lines[1].Id), _blank, null);
            }
            _column.PullEnd();

            Assert.AreEqual("Hola", DialogueTranslations.TextOf(_collection, _lines[0].Id, Other));
            Assert.IsNull(Table(_collection, Other).GetEntry(Entry(_collection, _lines[1].Id).Id), "An empty cell never adds an entry");
        }

        [Test]
        public void TranslationColumn_Pull_IgnoresSourceLocaleAndForeignEntries() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            DialogueTranslationColumn _sourceColumn = new DialogueTranslationColumn { LocaleIdentifier = Source };
            _sourceColumn.PullBegin(_collection);
            _sourceColumn.PullCellData(Entry(_collection, _lines[0].Id), "Overwritten", null);
            _sourceColumn.PullEnd();
            Assert.AreEqual("Hello", DialogueTranslations.TextOf(_collection, _lines[0].Id, Source));

            // An entry of another shared table, not of this collection
            SharedTableData _foreign = ScriptableObject.CreateInstance<SharedTableData>();
            SharedTableData.SharedTableEntry _detached = _foreign.AddKey("detached");
            DialogueTranslationColumn _column = new DialogueTranslationColumn { LocaleIdentifier = Other };
            _column.PullBegin(_collection);
            _column.PullCellData(_detached, "Hola", null);
            _column.PullEnd();
            Object.DestroyImmediate(_foreign);
            Assert.IsNull(Table(_collection, Other).GetEntry(_detached.Id));
        }

        [Test]
        public void StatusColumn_Push_OutdatedWithPreviousSource() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello", "Bye");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[1].Text, _lines[1].Id, Other, "Adios"));
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _lines[0].Text, _lines[0].Id, "Hello there"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            DialogueStatusColumn _column = new DialogueStatusColumn { LocaleIdentifier = Other };

            _column.PushBegin(_collection);
            _column.PushHeader(_collection, out string _header, out string _headerNote);
            _column.PushCellData(Entry(_collection, _lines[0].Id), new List<StringTableEntry>(), out string _outdated, out string _outdatedNote);
            _column.PushCellData(Entry(_collection, _lines[1].Id), new List<StringTableEntry>(), out string _upToDate, out string _upToDateNote);
            _column.PushEnd();

            Assert.AreEqual("es Status", _header);
            Assert.IsNull(_headerNote);
            Assert.AreEqual("Outdated\nPrevious source: Hello", _outdated);
            Assert.AreEqual("Hello there", _outdatedNote, "The note records the source text the mark was pushed against");
            Assert.IsTrue(string.IsNullOrEmpty(_upToDate));
            Assert.IsTrue(string.IsNullOrEmpty(_upToDateNote));
        }

        [Test]
        public void StatusColumn_Pull_EmptyCellMarksReviewed() {
            StringTableCollection _collection = OutdatedLine(out LineNode _line, out _);
            DialogueStatusColumn _column = new DialogueStatusColumn { LocaleIdentifier = Other };

            _column.PushBegin(_collection);
            _column.PushCellData(Entry(_collection, _line.Id), new List<StringTableEntry>(), out string _mark, out string _note);
            _column.PushEnd();

            _column.PullBegin(_collection);
            _column.PullCellData(Entry(_collection, _line.Id), _mark, _note);
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other), "A kept mark does nothing");
            _column.PullCellData(Entry(_collection, _line.Id), "", _note);
            _column.PullEnd();

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hola", DialogueTranslations.TextOf(_collection, _line.Id, Other));
            Assert.AreEqual("Hello there", DialogueTranslations.TextOf(_collection, _line.Id, Source));
        }

        [Test]
        public void StatusColumn_Pull_EmptyCell_OutdatedSinceLastPush_StaysOutdated() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            LineNode _line = _lines[0];
            Assert.IsTrue(DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            DialogueStatusColumn _column = new DialogueStatusColumn { LocaleIdentifier = Other };

            // Pushed while up to date: the cell is empty and has no note
            _column.PushBegin(_collection);
            _column.PushCellData(Entry(_collection, _line.Id), new List<StringTableEntry>(), out string _value, out string _note);
            _column.PushEnd();

            // The source text changes after the push; then a mark pushed against an older source text is cleared
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello there"));
            _column.PullBegin(_collection);
            _column.PullCellData(Entry(_collection, _line.Id), _value, _note);
            _column.PullCellData(Entry(_collection, _line.Id), "", "Hello");
            _column.PullEnd();

            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
        }

        [Test]
        public void StatusColumn_Pull_BlankCell_NoteLineEndingsNormalized() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "First\r\nline");
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _lines[0].Text, _lines[0].Id, "Second\r\nline"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            DialogueStatusColumn _column = new DialogueStatusColumn { LocaleIdentifier = Other };

            _column.PullBegin(_collection);
            _column.PullCellData(Entry(_collection, _lines[0].Id), "  ", "Second\nline");
            _column.PullEnd();

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _lines[0].Id, Other));
        }

        [Test]
        public void Columns_ForPull_RemovedLocale_NoCrossWrite() {
            DialogueAsset _asset = NewDialogue(out List<LineNode> _lines, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            LocaleIdentifier _extra = new LocaleIdentifier("zz");
            Assert.IsNull(LocalizationEditorSettings.GetLocale("zz"), "not a project locale");
            TestAddressables.AddForeignTable(_collection, _extra);
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, Other, "Hola"));
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, _extra, "Zz text"));

            List<SheetRow> _rows = Push(_collection, DialogueSheetColumns.ForPush(_collection), out string[] _headers);
            SetCell(_rows, _headers, _lines[0].Id, Other.ToString(), "Hola hoja");
            SetCell(_rows, _headers, _lines[0].Id, _extra.ToString(), "Zz sheet");

            // The es table leaves the collection, so the zz columns no longer follow the source column directly
            _collection.RemoveTable(Table(_collection, Other));
            List<SheetColumn> _columns = PullColumns(_collection, _headers);
            Pull(_collection, _columns, _rows);

            Assert.AreEqual(3, _columns.Count);
            Assert.AreEqual(System.Array.IndexOf(_headers, _extra.ToString()), _columns[1].ColumnIndex);
            Assert.AreEqual(System.Array.IndexOf(_headers, "zz Status"), _columns[2].ColumnIndex);
            Assert.AreEqual("Zz sheet", DialogueTranslations.TextOf(_collection, _lines[0].Id, _extra));
        }

        [Test]
        public void Columns_ForPull_MissingHeaders() {
            StringTableCollection _collection = DialogueTables.Linked(NewDialogue(out _, "Hello"));

            Assert.IsNull(DialogueSheetColumns.ForPull(_collection, new[] { "Comments", Other.ToString() }, out string _error));
            StringAssert.Contains("Key", _error);

            List<SheetColumn> _keyOnly = DialogueSheetColumns.ForPull(_collection, new[] { "Other", " key " }, out _error);
            Assert.IsNull(_error);
            Assert.AreEqual(1, _keyOnly.Count, "Locales without their headers are skipped");
            Assert.AreEqual(1, _keyOnly[0].ColumnIndex);
        }

        [Test]
        public void Columns_ForPull_ExcludeSourceLocale() {
            StringTableCollection _collection = DialogueTables.Linked(NewDialogue(out _, "Hello"));

            List<SheetColumn> _push = DialogueSheetColumns.ForPush(_collection);
            Push(_collection, _push, out string[] _headers, false);
            CollectionAssert.AreEqual(new[] { "Key", "Key Comments", Source.ToString(), Other.ToString(), "es Status" }, _headers);
            List<SheetColumn> _pull = PullColumns(_collection, _headers);

            Assert.AreEqual(5, _push.Count);
            Assert.IsInstanceOf<DialogueKeyColumn>(_push[0]);
            Assert.IsInstanceOf<KeyCommentColumn>(_push[1]);
            Assert.IsInstanceOf<LocaleColumn>(_push[2]);
            Assert.AreEqual(Source, ((LocaleColumn)_push[2]).LocaleIdentifier);
            Assert.IsInstanceOf<DialogueTranslationColumn>(_push[3]);
            Assert.AreEqual(Other, ((DialogueTranslationColumn)_push[3]).LocaleIdentifier);
            Assert.IsInstanceOf<DialogueStatusColumn>(_push[4]);
            Assert.AreEqual(Other, ((DialogueStatusColumn)_push[4]).LocaleIdentifier);
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E" }, _push.ConvertAll(_c => _c.Column));

            Assert.AreEqual(3, _pull.Count);
            Assert.IsInstanceOf<DialogueKeyColumn>(_pull[0]);
            Assert.IsInstanceOf<DialogueTranslationColumn>(_pull[1]);
            Assert.IsInstanceOf<DialogueStatusColumn>(_pull[2]);
            Assert.IsFalse(_pull.Exists(_c => _c is LocaleColumn || _c is KeyCommentColumn));
            CollectionAssert.AreEqual(new[] { "A", "D", "E" }, _pull.ConvertAll(_c => _c.Column), "Pulled columns sit where their headers are");
        }

        [Test]
        public void SheetsSettings_NotInResources_SavedInProjectSettings() {
            FilePathAttribute _attribute = typeof(DialogueSheetsSettings).GetCustomAttribute<FilePathAttribute>();
            Assert.IsNotNull(_attribute);

            // The path the singleton saves to, read without saving
            MethodInfo _getFilePath = typeof(ScriptableSingleton<DialogueSheetsSettings>).GetMethod("GetFilePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(_getFilePath);
            string _path = ((string)_getFilePath.Invoke(null, null)).Replace('\\', '/');
            Assert.AreEqual(SheetsSettingsPath, _path);
            StringAssert.DoesNotContain("Resources", _path);
            StringAssert.DoesNotStartWith("Assets/", _path);

            Assert.IsTrue(typeof(DialogueSheetsSettings).Assembly.GetName().Name.EndsWith(".Editor"), "The settings live in an editor assembly");
            Assert.IsTrue(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(DialogueSheetsSettings.instance)), "The settings are not a project asset");
        }
        #endregion
    }
}
