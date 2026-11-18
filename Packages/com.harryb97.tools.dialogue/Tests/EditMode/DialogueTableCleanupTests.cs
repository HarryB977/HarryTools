using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Tests {
    public class DialogueTableCleanupTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/Dialogues";
        private const string TablesFolder = TempFolder + "/Tables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private static readonly string[] ThirdCandidates = { "fr", "de", "it", "pt", "ja", "ko", "pl" };
        private string thirdCode;

        private readonly List<Locale> addedLocales = new List<Locale>();
        private DialogueSettings settings;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            EnsureLocale(SourceCode);
            EnsureLocale(OtherCode);
            for (int _i = 0; _i < ThirdCandidates.Length; _i++) {
                if (LocalizationEditorSettings.GetLocale(ThirdCandidates[_i]) != null) continue;
                thirdCode = ThirdCandidates[_i];
                break;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            for (int _i = 0; _i < addedLocales.Count; _i++) {
                if (addedLocales[_i] != null) LocalizationEditorSettings.RemoveLocale(addedLocales[_i]);
            }
            addedLocales.Clear();
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
            RemoveThird();
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            DeleteContents(TablesFolder);
            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
        }

        private void RemoveThird() {
            for (int _i = addedLocales.Count - 1; _i >= 0; _i--) {
                if (addedLocales[_i] == null || addedLocales[_i].Identifier.Code != thirdCode) continue;
                LocalizationEditorSettings.RemoveLocale(addedLocales[_i]);
                addedLocales.RemoveAt(_i);
            }
        }

        private Locale EnsureLocale(string _code) {
            Locale _existing = LocalizationEditorSettings.GetLocale(_code);
            if (_existing != null) return _existing;

            EnsureFolder(LocalesFolder);
            Locale _locale = Locale.CreateLocale(_code);
            AssetDatabase.CreateAsset(_locale, LocalesFolder + "/" + _code + ".asset");
            LocalizationEditorSettings.AddLocale(_locale);
            addedLocales.Add(_locale);
            return _locale;
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
        }

        private static void DeleteContents(string _folder) {
            if (!AssetDatabase.IsValidFolder(_folder)) return;
            foreach (string _guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { _folder })) {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(_guid));
            }
            foreach (string _guid in AssetDatabase.FindAssets("", new[] { _folder })) {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (!string.IsNullOrEmpty(_path)) AssetDatabase.DeleteAsset(_path);
            }
        }

        private static DialogueAsset NewAsset(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        private static StringTable Table(StringTableCollection _collection, string _code) {
            return _collection.GetTable(new LocaleIdentifier(_code)) as StringTable;
        }

        private static string TextOf(StringTableCollection _collection, string _code, string _key) {
            SharedTableData.SharedTableEntry _entry = _collection.SharedData.GetEntry(_key);
            return _entry != null ? Table(_collection, _code).GetEntry(_entry.Id)?.Value : null;
        }

        private static void WriteBoth(DialogueAsset _asset, LocalizedString _target, string _key, string _source, string _other) {
            DialogueTables.SetText(_asset, _target, _key, new LocaleIdentifier(SourceCode), _source);
            DialogueTables.SetText(_asset, _target, _key, new LocaleIdentifier(OtherCode), _other);
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void DeleteNode_RemovesItsEntries_UndoRestoresWithTexts() {
            DialogueAsset _asset = NewAsset("CleanDelete");
            LineNode _line = _asset.AddNode<LineNode>();
            LineNode _keep = _asset.AddNode<LineNode>();
            WriteBoth(_asset, _line.Text, _line.Id, "Hello", "Hola");
            WriteBoth(_asset, _keep.Text, _keep.Id, "Stay", "Quedate");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            string _lineId = _line.Id;
            string _keepId = _keep.Id;

            Undo.IncrementCurrentGroup();
            DialogueGraphOps.Delete(_asset, new[] { _lineId });

            Assert.IsNull(_asset.Find(_lineId));
            Assert.IsNull(_collection.SharedData.GetEntry(_lineId));
            Assert.IsNotNull(_collection.SharedData.GetEntry(_keepId));
            Assert.AreEqual("Stay", TextOf(_collection, SourceCode, _keepId));

            Undo.PerformUndo();

            Assert.IsNotNull(_asset.Find(_lineId));
            Assert.IsNotNull(_collection.SharedData.GetEntry(_lineId));
            Assert.AreEqual("Hello", TextOf(_collection, SourceCode, _lineId));
            Assert.AreEqual("Hola", TextOf(_collection, OtherCode, _lineId));
        }

        [Test]
        public void RemoveOption_RemovesOptionAndLockedEntries() {
            DialogueAsset _asset = NewAsset("CleanOption");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _first = DialogueGraphOps.AddOption(_asset, _choice);
            ChoiceOption _second = DialogueGraphOps.AddOption(_asset, _choice);
            WriteBoth(_asset, _first.Text, _first.Id, "One", "Uno");
            WriteBoth(_asset, _first.LockedReason, _first.Id + ".locked", "Closed", "Cerrado");
            WriteBoth(_asset, _second.Text, _second.Id, "Two", "Dos");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            string _firstId = _first.Id;
            string _secondId = _second.Id;

            DialogueGraphOps.RemoveOption(_asset, _choice, _firstId);

            Assert.IsNull(_collection.SharedData.GetEntry(_firstId));
            Assert.IsNull(_collection.SharedData.GetEntry(_firstId + ".locked"));
            Assert.AreEqual("Dos", TextOf(_collection, OtherCode, _secondId));
            Assert.AreEqual(1, _collection.SharedData.Entries.Count);
        }

        [Test]
        public void CleanOrphans_RemovesOnlyOwnerless() {
            DialogueAsset _asset = NewAsset("CleanOrphans");
            LineNode _line = _asset.AddNode<LineNode>();
            WriteBoth(_asset, _line.Text, _line.Id, "Kept", "Guardado");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            _collection.SharedData.AddKey("gone-1");
            _collection.SharedData.AddKey("gone-2");
            Table(_collection, SourceCode).AddEntry("gone-1", "x");

            int _removed = DialogueTables.CleanOrphans(_asset);

            Assert.AreEqual(2, _removed);
            Assert.AreEqual(1, _collection.SharedData.Entries.Count);
            Assert.AreEqual("Kept", TextOf(_collection, SourceCode, _line.Id));
            Assert.AreEqual("Guardado", TextOf(_collection, OtherCode, _line.Id));
            Assert.AreEqual(0, DialogueTables.CleanOrphans(_asset));
        }

        [Test]
        public void RemoveOption_UndoRestoresEntriesWithTexts() {
            DialogueAsset _asset = NewAsset("CleanOptionUndo");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _option = DialogueGraphOps.AddOption(_asset, _choice);
            WriteBoth(_asset, _option.Text, _option.Id, "One", "Uno");
            WriteBoth(_asset, _option.LockedReason, _option.Id + ".locked", "Closed", "Cerrado");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            string _id = _option.Id;

            Undo.IncrementCurrentGroup();
            DialogueGraphOps.RemoveOption(_asset, _choice, _id);
            Assert.IsNull(_collection.SharedData.GetEntry(_id));

            Undo.PerformUndo();

            Assert.AreEqual(1, _choice.Options.Count);
            Assert.AreEqual("Uno", TextOf(_collection, OtherCode, _id));
            Assert.AreEqual("Closed", TextOf(_collection, SourceCode, _id + ".locked"));
        }

        [Test]
        public void DeleteSeveralNodes_UndoneInOneStep() {
            DialogueAsset _asset = NewAsset("CleanDeleteMany");
            LineNode _a = _asset.AddNode<LineNode>();
            LineNode _b = _asset.AddNode<LineNode>();
            WriteBoth(_asset, _a.Text, _a.Id, "A", "Aa");
            WriteBoth(_asset, _b.Text, _b.Id, "B", "Bb");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            string _idA = _a.Id;
            string _idB = _b.Id;

            Undo.IncrementCurrentGroup();
            DialogueGraphOps.Delete(_asset, new[] { _idA, _idB });
            Assert.AreEqual(0, _collection.SharedData.Entries.Count);

            Undo.PerformUndo();

            Assert.IsNotNull(_asset.Find(_idA));
            Assert.IsNotNull(_asset.Find(_idB));
            Assert.AreEqual("Aa", TextOf(_collection, OtherCode, _idA));
            Assert.AreEqual("B", TextOf(_collection, SourceCode, _idB));
        }

        [Test]
        public void LocaleAdded_CharactersCollectionGetsTable() {
            if (LocalizationEditorSettings.GetStringTableCollection("DialogueCharacters") != null) Assert.Ignore("A characters collection already exists in the project.");
            EnsureFolder(TablesFolder);
            StringTableCollection _characters = LocalizationEditorSettings.CreateStringTableCollection("DialogueCharacters", TablesFolder);
            Assert.IsNull(_characters.GetTable(new LocaleIdentifier(thirdCode)));

            EnsureLocale(thirdCode);

            Assert.IsNotNull(_characters.GetTable(new LocaleIdentifier(thirdCode)));
        }

        [Test]
        public void LocaleAdded_RenamedLinkedCollectionGetsTable() {
            DialogueAsset _asset = NewAsset("CleanRenamed");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            _collection.SharedData.TableCollectionName = "SomethingElse_" + _asset.name;
            EditorUtility.SetDirty(_collection.SharedData);

            EnsureLocale(thirdCode);

            Assert.IsNotNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));
        }

        [Test]
        public void EnsureCollection_ExistingCollection_RepairsMissingLocaleTable() {
            DialogueAsset _asset = NewAsset("CleanRepair");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            Locale _third = EnsureLocale(thirdCode);
            StringTable _table = Table(_collection, thirdCode);
            Assert.IsNotNull(_table);
            _collection.RemoveTable(_table, true);
            Assert.IsNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));

            DialogueTables.EnsureCollection(_asset);

            Assert.IsNotNull(_collection.GetTable(_third.Identifier));
        }

        [Test]
        public void LocaleAdded_TablesCreated() {
            DialogueAsset _asset = NewAsset("CleanLocale");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            Assert.IsNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));

            EnsureLocale(thirdCode);

            Assert.IsNotNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));
        }

        [Test]
        public void LocaleRemoved_KeepsTable() {
            Locale _third = EnsureLocale(thirdCode);
            DialogueAsset _asset = NewAsset("CleanLocaleRemoved");
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            Assert.IsNotNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));

            LocalizationEditorSettings.RemoveLocale(_third);
            addedLocales.Remove(_third);

            Assert.IsNotNull(_collection.GetTable(new LocaleIdentifier(thirdCode)));
        }
        #endregion
    }
}
