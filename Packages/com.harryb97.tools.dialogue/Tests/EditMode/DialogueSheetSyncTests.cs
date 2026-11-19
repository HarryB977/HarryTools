using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueSheetSyncTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/SyncDialogues";
        private const string TablesFolder = TempFolder + "/SyncTables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private const string SheetsSettingsPath = "ProjectSettings/HarryToolsDialogueSheets.asset";
        private const string KeyHeader = "Key";

        private static readonly LocaleIdentifier Source = new LocaleIdentifier(SourceCode);
        private static readonly LocaleIdentifier Other = new LocaleIdentifier(OtherCode);

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        private FakeSheetGateway gateway;
        private FakeSheetTabs tabs;
        private DialogueSheetSync sync;
        private bool sheetsSettingsExisted;
        private DateTime sheetsSettingsWriteTime;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/SyncLocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = createdSettings;
            }
            EnsureLocale(SourceCode);
            EnsureLocale(OtherCode);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            for (int _i = 0; _i < addedLocales.Count; _i++) {
                if (addedLocales[_i] != null) LocalizationEditorSettings.RemoveLocale(addedLocales[_i]);
            }
            addedLocales.Clear();

            if (createdSettings != null) LocalizationEditorSettings.ActiveLocalizationSettings = null;
            createdSettings = null;

            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [SetUp]
        public void SetUp() {
            sheetsSettingsExisted = File.Exists(SheetsSettingsPath);
            sheetsSettingsWriteTime = sheetsSettingsExisted ? File.GetLastWriteTimeUtc(SheetsSettingsPath) : default;

            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(AssetsFolder);
            EnsureFolder(TablesFolder);

            gateway = new FakeSheetGateway();
            tabs = new FakeSheetTabs();
            sync = new DialogueSheetSync(gateway, tabs);
        }

        [TearDown]
        public void TearDown() {
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            DeleteContents(TablesFolder);
            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);

            Assert.AreEqual(sheetsSettingsExisted, File.Exists(SheetsSettingsPath), "The project's sheet settings are never written");
            if (sheetsSettingsExisted) Assert.AreEqual(sheetsSettingsWriteTime, File.GetLastWriteTimeUtc(SheetsSettingsPath));
        }

        private void EnsureLocale(string _code) {
            if (LocalizationEditorSettings.GetLocale(_code) != null) return;

            EnsureFolder(LocalesFolder);
            Locale _locale = Locale.CreateLocale(_code);
            AssetDatabase.CreateAsset(_locale, LocalesFolder + "/" + _code + ".asset");
            DialogueLocaleWatcher.AddLocaleForTests(_locale);
            addedLocales.Add(_locale);
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

        /// <summary> A dialogue in the folder with one line per source text </summary>
        private static DialogueAsset NewDialogue(string _folder, string _name, out List<LineNode> _lines, params string[] _sources) {
            EnsureFolder(_folder);
            string _path = _folder + "/" + _name + ".asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DialogueAsset>(), _path);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
            DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_path);

            _lines = new List<LineNode>();
            for (int _i = 0; _i < _sources.Length; _i++) _lines.Add(_asset.AddNode<LineNode>());
            _asset.EnsureIds();
            for (int _i = 0; _i < _sources.Length; _i++) {
                Assert.IsTrue(DialogueTables.SetSourceText(_asset, _lines[_i].Text, _lines[_i].Id, _sources[_i]));
            }
            return _asset;
        }

        private static DialogueAsset NewDialogue(string _name, out List<LineNode> _lines, params string[] _sources) {
            return NewDialogue(AssetsFolder, _name, out _lines, _sources);
        }

        private static DialogueCharacter NewCharacter(string _name) {
            string _path = AssetsFolder + "/" + _name + ".asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DialogueCharacter>(), _path);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<DialogueCharacter>(_path);
        }

        private static void Translate(DialogueAsset _asset, LineNode _line, string _text) {
            Assert.IsTrue(DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, _text));
        }

        private static string Text(DialogueAsset _asset, LineNode _line, LocaleIdentifier _locale) {
            return DialogueTranslations.TextOf(DialogueTables.Linked(_asset), _line.Id, _locale);
        }

        private static string Guid(StringTableCollection _collection) {
            return _collection.SharedData.TableCollectionNameGuid.ToString("N");
        }

        private static void AssertDone(SyncResult _result) {
            Assert.IsTrue(_result.Succeeded, _result.Error);
        }

        private static List<string> Keys(StringTableCollection _collection) {
            return _collection.SharedData.Entries.ConvertAll(_entry => _entry.Key);
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Pull_CreatesTabOnce_StoresSheetId() {
            DialogueAsset _asset = NewDialogue("Tabbed", out _, "Hello");
            string _title = DialogueSheetSync.TabTitle(_asset);

            AssertDone(sync.Pull(_asset));
            AssertDone(sync.Pull(_asset));

            Assert.AreEqual(1, gateway.AddSheetCalls);
            Assert.AreEqual(1, gateway.SheetCount);
            Assert.IsTrue(tabs.TryGetTab(Guid(DialogueTables.Linked(_asset)), out int _sheetId));
            Assert.AreEqual(gateway.IdOf(_title), _sheetId);
            Assert.AreEqual(0, _sheetId, "The first tab has ID 0, which is still a recorded tab");
            Assert.GreaterOrEqual(tabs.SaveCalls, 1);
        }

        [Test]
        public void TabTitle_SameName_Distinct() {
            DialogueAsset _a = NewDialogue(AssetsFolder + "/A", "Same", out _, "Hello");
            DialogueAsset _b = NewDialogue(AssetsFolder + "/B", "Same", out _, "Bye");

            string _titleA = DialogueSheetSync.TabTitle(_a);
            string _titleB = DialogueSheetSync.TabTitle(_b);
            Assert.AreNotEqual(_titleA, _titleB);
            Assert.AreEqual("Same (" + _a.AssetId.Substring(0, 6) + ")", _titleA);

            AssertDone(sync.Push(_a));
            AssertDone(sync.Push(_b));
            Assert.AreEqual(2, gateway.SheetCount);
        }

        [Test]
        public void Push_RenamedDialogue_SameTab() {
            DialogueAsset _asset = NewDialogue("BeforeRename", out _, "Hello");
            AssertDone(sync.Push(_asset));
            Assert.IsTrue(tabs.TryGetTab(Guid(DialogueTables.Linked(_asset)), out int _before));

            Assert.AreEqual("", AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_asset), "AfterRename"));
            Assert.AreEqual("AfterRename", _asset.name);
            AssertDone(sync.Push(_asset));

            Assert.IsTrue(tabs.TryGetTab(Guid(DialogueTables.Linked(_asset)), out int _after));
            Assert.AreEqual(_before, _after);
            Assert.AreEqual(1, gateway.AddSheetCalls);
            Assert.AreEqual(2, gateway.PushCalls);
        }

        [Test]
        public void Pull_NeverTouchesSourceLocale() {
            DialogueAsset _asset = NewDialogue("SourceKept", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            gateway.SetCell(_title, _lines[0].Id, Source.ToString(), "Edited in the sheet");
            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola hoja");
            AssertDone(sync.Pull(_asset));

            Assert.AreEqual("Hello", Text(_asset, _lines[0], Source));
            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
        }

        [Test]
        public void Pull_UnknownRows_Ignored_NoKeysCreatedOrRenamed() {
            DialogueAsset _asset = NewDialogue("Unknown", out List<LineNode> _lines, "Hello", "Bye");
            AssertDone(sync.Push(_asset));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            List<string> _keys = Keys(_collection);
            string _title = DialogueSheetSync.TabTitle(_asset);

            gateway.AddRow(_title, "ghost_a", Other.ToString(), "Fantasma");
            gateway.AddRow(_title, "ghost_b", Other.ToString(), "Otro");
            gateway.AddRow(_title, _lines[0].Id, Other.ToString(), "Duplicada");
            LogAssert.Expect(LogType.Warning, new Regex("2 unknown rows ignored"));
            SyncResult _result = sync.Pull(_asset);

            AssertDone(_result);
            Assert.AreEqual(2, _result.UnknownRows);
            CollectionAssert.AreEqual(_keys, Keys(_collection), "No key was added, removed or renamed");
            Assert.IsNull(_collection.SharedData.GetEntry("ghost_a"));
            Assert.IsTrue(string.IsNullOrEmpty(Text(_asset, _lines[0], Other)), "A repeated key keeps its first row");
        }

        [Test]
        public void Pull_ReordersToScriptOrder() {
            DialogueAsset _asset = NewDialogue("Order", out _, "One", "Two", "Three");
            AssertDone(sync.Push(_asset));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            List<string> _scriptOrder = new List<string>(DialogueTableLayout.ScriptOrder(_asset));

            List<SharedTableData.SharedTableEntry> _reversed = new List<SharedTableData.SharedTableEntry>(_collection.SharedData.Entries);
            _reversed.Reverse();
            _collection.SharedData.Entries = _reversed;
            CollectionAssert.AreNotEqual(_scriptOrder, Keys(_collection).GetRange(0, _scriptOrder.Count));

            AssertDone(sync.Pull(_asset));
            CollectionAssert.AreEqual(_scriptOrder, Keys(_collection).GetRange(0, _scriptOrder.Count));
        }

        [Test]
        public void Push_PullsFirst_SheetEditSurvives() {
            DialogueAsset _asset = NewDialogue("SheetFirst", out List<LineNode> _lines, "Hello", "Bye");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola hoja");
            Translate(_asset, _lines[1], "Adios");
            AssertDone(sync.Push(_asset));

            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
            Assert.AreEqual("Hola hoja", gateway.Cell(_title, _lines[0].Id, Other.ToString()));
            Assert.AreEqual("Adios", gateway.Cell(_title, _lines[1].Id, Other.ToString()), "A local edit is pushed");
        }

        [Test]
        public void Push_PullFails_NothingPushed() {
            DialogueAsset _asset = NewDialogue("PullFails", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            Translate(_asset, _lines[0], "Hola local");
            gateway.FailingTitles.Add(_title);
            SyncResult _result = sync.Push(_asset);

            Assert.IsFalse(_result.Succeeded);
            StringAssert.Contains("nothing was pushed", _result.Error);
            Assert.AreEqual(1, gateway.PushCalls);
            gateway.FailingTitles.Clear();
            Assert.AreEqual("Hola", gateway.Cell(_title, _lines[0].Id, Other.ToString()));
        }

        [Test]
        public void Push_Rejected_NothingRecordedAsSynced() {
            DialogueAsset _asset = NewDialogue("Rejected", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            StringTableEntry Entry() => (_collection.GetTable(Other) as StringTable).GetEntry(_collection.SharedData.GetEntry(_lines[0].Id).Id);

            gateway.FailPushes = true;
            Assert.IsFalse(sync.Push(_asset).Succeeded);
            Assert.IsNull(Entry().GetMetadata<SheetSyncSnapshot>(), "A rejected push records nothing");

            gateway.FailPushes = false;
            AssertDone(sync.Push(_asset));
            Assert.AreEqual("Hola", Entry().GetMetadata<SheetSyncSnapshot>().SyncedText);
        }

        [Test]
        public void Push_NewLocale_ColumnsAdded() {
            DialogueAsset _asset = NewDialogue("NewLocale", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);
            CollectionAssert.AreEqual(new[] { KeyHeader, "Key Comments", Source.ToString(), Other.ToString(), "es Status" }, gateway.Grid(_title).Headers());

            // A table of a locale the project does not have, so no project locale is added
            LocaleIdentifier _extra = new LocaleIdentifier("zz");
            Assert.IsNull(LocalizationEditorSettings.GetLocale("zz"));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            TestAddressables.AddForeignTable(_collection, _extra);
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, _extra, "Zz text"));
            AssertDone(sync.Push(_asset));

            CollectionAssert.AreEqual(new[] { KeyHeader, "Key Comments", Source.ToString(), Other.ToString(), "es Status", _extra.ToString(), "zz Status" }, gateway.Grid(_title).Headers());
            Assert.AreEqual("Zz text", gateway.Cell(_title, _lines[0].Id, _extra.ToString()));
            Assert.AreEqual("Hola", gateway.Cell(_title, _lines[0].Id, Other.ToString()));
        }

        [Test]
        public void Pull_ColumnsMovedInSheet_ReadByHeader() {
            DialogueAsset _asset = NewDialogue("Moved", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            // The translator puts the columns in the opposite order
            SheetGrid _old = gateway.Grid(_title);
            int _width = _old.Headers().Count;
            SheetGrid _moved = new SheetGrid();
            for (int _row = 0; _row < _old.RowCount; _row++) {
                for (int _column = 0; _column < _width; _column++) _moved.Set(_row, _width - 1 - _column, _old.Value(_row, _column), _old.Note(_row, _column));
            }
            gateway.Replace(_title, _moved);
            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola movida");

            AssertDone(sync.Pull(_asset));
            Assert.AreEqual("Hola movida", Text(_asset, _lines[0], Other));
            Assert.AreEqual("Hello", Text(_asset, _lines[0], Source));
        }

        [Test]
        public void Pull_LocallyClearedTranslation_SheetChanged_CountsConflict() {
            DialogueAsset _asset = NewDialogue("Cleared", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            (_collection.GetTable(Other) as StringTable).GetEntry(_collection.SharedData.GetEntry(_lines[0].Id).Id).Value = "";
            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola hoja");
            LogAssert.Expect(LogType.Warning, new Regex("1 conflict where"));
            SyncResult _result = sync.Pull(_asset);

            AssertDone(_result);
            Assert.AreEqual(1, _result.Conflicts);
            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
        }

        [Test]
        public void PushAll_OneFails_OthersDone_SummaryLists() {
            DialogueAsset _a = NewDialogue("AllA", out _, "Hello");
            DialogueAsset _b = NewDialogue("AllB", out _, "Bye");
            DialogueAsset _c = NewDialogue("AllC", out _, "Thanks");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(NewCharacter("AllCharacter"), "Ada"));
            gateway.FailingTitles.Add(DialogueSheetSync.TabTitle(_b));

            SyncSummary _summary = sync.PushAll(new[] { _a, _b, _c });

            Assert.AreEqual(4, _summary.Results.Count, "The three dialogues and the characters");
            Assert.AreEqual(1, _summary.Failures);
            Assert.IsFalse(_summary.Results[1].Succeeded);
            Assert.AreEqual("AllB", _summary.Results[1].Name);
            Assert.IsTrue(_summary.Results[0].Succeeded && _summary.Results[2].Succeeded && _summary.Results[3].Succeeded);
            Assert.AreNotEqual(-1, gateway.IdOf(DialogueSheetSync.TabTitle(_a)));
            Assert.AreNotEqual(-1, gateway.IdOf(DialogueSheetSync.TabTitle(_c)));
            Assert.AreNotEqual(-1, gateway.IdOf("Characters"));
            Assert.AreEqual(3, gateway.PushCalls);

            string _text = _summary.Describe();
            StringAssert.Contains("Pushed 3 of 4", _text);
            StringAssert.Contains("AllB: failed", _text);
            StringAssert.Contains("0 unknown rows ignored, 0 conflicts", _text);
        }

        [Test]
        public void Characters_PushPull_OwnTab() {
            DialogueCharacter _character = NewCharacter("Ada");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_character, "Ada"));
            Assert.IsTrue(DialogueCharacterTables.SetName(_character, Other, "Ada ES"));
            StringTableCollection _collection = DialogueCharacterTables.Find();
            Assert.AreNotEqual(DialogueCharacterTables.CollectionName, _collection.TableCollectionName, "Tests use their own characters collection");

            AssertDone(sync.PushCharacters());
            Assert.IsTrue(tabs.TryGetTab(Guid(_collection), out int _sheetId));
            Assert.AreEqual(gateway.IdOf("Characters"), _sheetId);
            Assert.AreEqual("Ada ES", gateway.Cell("Characters", _character.CharacterId, Other.ToString()));

            gateway.SetCell("Characters", _character.CharacterId, Other.ToString(), "Ada hoja");
            AssertDone(sync.PullCharacters());
            Assert.AreEqual("Ada hoja", DialogueTranslations.TextOf(_collection, _character.CharacterId, Other));
            Assert.AreEqual("Ada", DialogueTranslations.TextOf(_collection, _character.CharacterId, Source));

            DialogueAsset _asset = NewDialogue("WithCharacters", out _, "Hello");
            AssertDone(sync.Push(_asset));
            Assert.AreEqual(2, gateway.SheetCount, "Dialogues and characters have tabs of their own");
        }

        [Test]
        public void Gateway_NotConfigured_ClearMessage_NoRequest() {
            GoogleSheetGateway _google = new GoogleSheetGateway(null, null);
            SheetConnection _connection = _google.Test();
            Assert.IsFalse(_connection.Connected);
            StringAssert.Contains("Sheets Service Provider", _connection.Message);
            Assert.Throws<InvalidOperationException>(() => _google.GetSheets());

            DialogueSheetSync _sync = new DialogueSheetSync(_google, tabs);
            DialogueAsset _asset = NewDialogue("NotConfigured", out _, "Hello");
            SyncResult _result = _sync.Push(_asset);
            Assert.IsFalse(_result.Succeeded);
            StringAssert.Contains("Sheets Service Provider", _result.Error);
        }

        [Test]
        public void Pull_EmptyCell_KeepsLocalTranslation() {
            DialogueAsset _asset = NewDialogue("EmptyCells", out List<LineNode> _lines, "Hello", "Bye");
            Translate(_asset, _lines[0], "Hola");
            Translate(_asset, _lines[1], "Adios");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            // The first row has a blank translation; the second has only its key, as a sheet returns a row whose trailing cells are empty
            SheetGrid _old = gateway.Grid(_title);
            SheetGrid _grid = new SheetGrid();
            List<string> _headers = _old.Headers();
            for (int _column = 0; _column < _headers.Count; _column++) _grid.Set(0, _column, _headers[_column], null);
            int _key = FakeSheetGateway.ColumnOf(_old, KeyHeader);
            int _other = FakeSheetGateway.ColumnOf(_old, Other.ToString());
            Assert.Greater(_other, _key);
            _grid.Set(1, _key, _lines[0].Id, null);
            _grid.Set(1, _other, "   ", null);
            _grid.Set(2, _key, _lines[1].Id, null);
            gateway.Replace(_title, _grid);

            SyncResult _result = sync.Pull(_asset);
            AssertDone(_result);
            Assert.AreEqual(0, _result.UnknownRows);
            Assert.AreEqual(0, _result.Conflicts);
            Assert.AreEqual("Hola", Text(_asset, _lines[0], Other));
            Assert.AreEqual("Adios", Text(_asset, _lines[1], Other));
        }

        [Test]
        public void Push_LocaleColumnsShifted_PullNoCrossWrite() {
            DialogueAsset _asset = NewDialogue("Shifted", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            // A new locale's table goes before the es table, so the es columns move right on the next push
            LocaleIdentifier _extra = new LocaleIdentifier("zz");
            TestAddressables.AddForeignTable(_collection, _extra);
            StringTable _otherTable = _collection.GetTable(Other) as StringTable;
            _collection.RemoveTable(_otherTable);
            _collection.AddTable(_otherTable);
            Assert.IsTrue(DialogueTables.SetText(_asset, _lines[0].Text, _lines[0].Id, _extra, "Zz text"));
            AssertDone(sync.Push(_asset));

            CollectionAssert.AreEqual(new[] { KeyHeader, "Key Comments", Source.ToString(), _extra.ToString(), "zz Status", Other.ToString(), "es Status" }, gateway.Grid(_title).Headers());
            Assert.AreEqual("Hola", gateway.Cell(_title, _lines[0].Id, Other.ToString()));
            Assert.AreEqual("Zz text", gateway.Cell(_title, _lines[0].Id, _extra.ToString()));

            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola hoja");
            gateway.SetCell(_title, _lines[0].Id, _extra.ToString(), "Zz sheet");
            AssertDone(sync.Pull(_asset));
            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
            Assert.AreEqual("Zz sheet", Text(_asset, _lines[0], _extra));
            Assert.AreEqual("Hello", Text(_asset, _lines[0], Source));
        }

        [Test]
        public void Push_ApiKey_FailsEarly_NoRequest() {
            DialogueAsset _asset = NewDialogue("ApiKeyPush", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            gateway.IsReadOnly = true;

            SyncResult _result = sync.Push(_asset);
            Assert.IsFalse(_result.Succeeded);
            Assert.AreEqual(SheetConnection.ReadOnlyMessage, _result.Error);
            Assert.AreEqual(0, gateway.WriteAttempts);
            Assert.AreEqual(0, gateway.ReadCalls);

            SyncResult _characters = sync.PushCharacters();
            Assert.IsFalse(_characters.Succeeded);
            Assert.AreEqual(0, gateway.WriteAttempts);
        }

        [Test]
        public void Pull_ApiKey_NoTab_NothingToPull() {
            DialogueAsset _asset = NewDialogue("ApiKeyNoTab", out _, "Hello");
            gateway.IsReadOnly = true;

            AssertDone(sync.Pull(_asset));
            Assert.AreEqual(0, gateway.WriteAttempts, "No tab is created");
            Assert.AreEqual(0, gateway.ReadCalls);
            Assert.IsFalse(tabs.TryGetTab(Guid(DialogueTables.Linked(_asset)), out _));
        }

        [Test]
        public void Pull_ApiKey_ExistingTab_Pulls() {
            DialogueAsset _asset = NewDialogue("ApiKeyPull", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);
            gateway.SetCell(_title, _lines[0].Id, Other.ToString(), "Hola hoja");

            gateway.IsReadOnly = true;
            AssertDone(sync.Pull(_asset));
            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
        }

        [Test]
        public void Gateway_ApiKey_WritesRefusedWithoutRequest() {
            SheetsServiceProvider _provider = ScriptableObject.CreateInstance<SheetsServiceProvider>();
            try {
                _provider.SetApiKey("not-a-real-key");
                GoogleSheetGateway _google = new GoogleSheetGateway(_provider, "not-a-real-spreadsheet");
                Assert.IsTrue(_google.IsReadOnly);

                InvalidOperationException _add = Assert.Throws<InvalidOperationException>(() => _google.AddSheet("Tab"));
                Assert.AreEqual(SheetConnection.ReadOnlyMessage, _add.Message);
                DialogueAsset _asset = NewDialogue("ApiKeyGateway", out _, "Hello");
                StringTableCollection _collection = DialogueTables.Linked(_asset);
                InvalidOperationException _push = Assert.Throws<InvalidOperationException>(() => _google.Push(0, _collection, DialogueSheetColumns.ForPush(_collection)));
                Assert.AreEqual(SheetConnection.ReadOnlyMessage, _push.Message);

                StringAssert.StartsWith("Connected with an API key: pull only.", SheetConnection.Reached(3, true).Message);
                Assert.AreEqual("Connected. The spreadsheet has 1 tab.", SheetConnection.Reached(1, false).Message);
            } finally {
                Object.DestroyImmediate(_provider);
            }
        }

        [Test]
        public void Pull_BlankHeaderRowWithData_Fails() {
            DialogueAsset _asset = NewDialogue("NoHeaders", out List<LineNode> _lines, "Hello");
            AssertDone(sync.Push(_asset));
            string _title = DialogueSheetSync.TabTitle(_asset);

            SheetGrid _grid = gateway.Grid(_title);
            List<string> _headers = _grid.Headers();
            for (int _column = 0; _column < _headers.Count; _column++) _grid.SetValue(0, _column, "");

            SyncResult _result = sync.Pull(_asset);
            Assert.IsFalse(_result.Succeeded);
            StringAssert.Contains("no headers", _result.Error);
        }

        [Test]
        public void Pull_SheetEdit_TablesSaved() {
            DialogueAsset _asset = NewDialogue("Saved", out List<LineNode> _lines, "Hello");
            Translate(_asset, _lines[0], "Hola");
            AssertDone(sync.Push(_asset));
            gateway.SetCell(DialogueSheetSync.TabTitle(_asset), _lines[0].Id, Other.ToString(), "Hola hoja");

            AssertDone(sync.Pull(_asset));
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Assert.AreEqual("Hola hoja", Text(_asset, _lines[0], Other));
            Assert.IsFalse(EditorUtility.IsDirty(_collection.GetTable(Other)));
            Assert.IsFalse(EditorUtility.IsDirty(_collection.SharedData));
        }

        [Test]
        public void Describe_UnwrapsAndHintsNotFound() {
            Exception _wrapped = new AggregateException(new TargetInvocationException(new InvalidOperationException("Inner reason.")));
            Assert.AreEqual("Inner reason.", DialogueSheetSync.Describe(_wrapped));
            Assert.AreEqual("Outer reason.", DialogueSheetSync.Describe(new InvalidOperationException("Outer reason.", new Exception("inner"))), "Other exceptions keep their own message");

            Google.GoogleApiException _notFound = new Google.GoogleApiException("Sheets", "Requested entity was not found.") { HttpStatusCode = HttpStatusCode.NotFound };
            StringAssert.EndsWith("Check the spreadsheet ID.", DialogueSheetSync.Describe(new AggregateException(_notFound)));
        }
        #endregion
    }
}
