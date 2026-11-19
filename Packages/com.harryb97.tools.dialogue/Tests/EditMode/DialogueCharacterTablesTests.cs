using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueCharacterTablesTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/CharacterAssets";
        private const string TablesFolder = TempFolder + "/CharacterTables";
        // Never the project's own collection name: tests only ever create this one, under the temp folder
        private const string TestCollectionName = "DialogueCharactersTest";
        private const string SourceCode = "en";
        private const string OtherCode = "es";

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/CharacterLocalizationSettings.asset");
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
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            DialogueCharacterTables.UseCollectionNameForTests(TestCollectionName);
            EnsureFolder(AssetsFolder);
            EnsureFolder(TablesFolder);
        }

        [TearDown]
        public void TearDown() {
            DialogueCharacterTables.UseCollectionNameForTests(null);
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            DeleteContents(TablesFolder);
            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
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

        private static DialogueCharacter NewCharacter(string _name) {
            DialogueCharacter _character = ScriptableObject.CreateInstance<DialogueCharacter>();
            string _path = AssetsFolder + "/" + _name + ".asset";
            AssetDatabase.CreateAsset(_character, _path);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<DialogueCharacter>(_path);
        }

        private static DialogueAsset NewDialogue(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        private static StringTable Table(StringTableCollection _collection, string _code) {
            return _collection.GetTable(new LocaleIdentifier(_code)) as StringTable;
        }

        private static string Key(DialogueCharacter _character) => _character.CharacterId;

        private static LocaleIdentifier Other => new LocaleIdentifier(OtherCode);
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void CharacterId_AssignedOnCreate_NewOnDuplicate() {
            DialogueCharacter _a = NewCharacter("A");
            string _pathA = AssetDatabase.GetAssetPath(_a);
            string _guidA = AssetDatabase.AssetPathToGUID(_pathA);
            Assert.AreEqual(_guidA, _a.CharacterId);

            string _pathB = AssetsFolder + "/B.asset";
            Assert.IsTrue(AssetDatabase.CopyAsset(_pathA, _pathB));
            AssetDatabase.ImportAsset(_pathB, ImportAssetOptions.ForceUpdate);
            DialogueCharacter _b = AssetDatabase.LoadAssetAtPath<DialogueCharacter>(_pathB);
            string _guidB = AssetDatabase.AssetPathToGUID(_pathB);

            Assert.AreNotEqual(_guidA, _guidB);
            Assert.AreEqual(_guidB, _b.CharacterId);
            Assert.AreEqual(_guidA, _a.CharacterId);

            string _pathC = AssetsFolder + "/C.asset";
            Assert.AreEqual("", AssetDatabase.MoveAsset(_pathA, _pathC));
            AssetDatabase.ImportAsset(_pathC, ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(_guidA, AssetDatabase.LoadAssetAtPath<DialogueCharacter>(_pathC).CharacterId, "a moved character keeps its id");
        }

        [Test]
        public void EnsureId_CharacterWithoutId_GetsItLazily() {
            DialogueCharacter _character = NewCharacter("Lazy");
            _character.CharacterId = "";

            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_character)), DialogueCharacterTables.EnsureId(_character));
        }

        [Test]
        public void SetSourceName_CreatesEntry_PointsDisplayName() {
            DialogueCharacter _character = NewCharacter("Named");

            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_character, "Ada"));

            StringTableCollection _collection = DialogueCharacterTables.Find();
            Assert.IsNotNull(_collection);
            Assert.AreEqual(TestCollectionName, _collection.TableCollectionName);
            Assert.IsTrue(AssetDatabase.GetAssetPath(_collection.SharedData).StartsWith(TablesFolder), "created under the tables folder");
            Assert.IsNotNull(_collection.SharedData.GetEntry(Key(_character)));
            Assert.AreEqual("Ada", DialogueTables.GetSourceText(_character.DisplayName));
            Assert.IsTrue(DialogueCharacterTables.IsOwnCollection(_character));
        }

        [Test]
        public void SetSourceName_ChangesNameOfTranslated_MarksOutdated() {
            DialogueCharacter _character = NewCharacter("Outdated");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_character, "Ada"));
            Assert.IsTrue(DialogueCharacterTables.SetName(_character, Other, "Ada ES"));
            StringTableCollection _collection = DialogueCharacterTables.Find();
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, Key(_character), Other));

            DialogueCharacterTables.SetSourceName(_character, "Ada Lovelace");

            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, Key(_character), Other));
            Assert.AreEqual("Ada", DialogueTranslations.PreviousSource(_collection, Key(_character), Other));
            DialogueTranslations.MarkReviewed(_collection, Key(_character));
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, Key(_character), Other));
        }

        [Test]
        public void SetName_NameInAnotherTable_ReturnsFalse() {
            DialogueCharacter _character = NewCharacter("Elsewhere");
            StringTableCollection _other = LocalizationEditorSettings.CreateStringTableCollection("CharacterElsewhere", TablesFolder);
            SharedTableData.SharedTableEntry _entry = _other.SharedData.AddKey("someone");
            (_other.GetTable(new LocaleIdentifier(SourceCode)) as StringTable).AddEntry(_entry.Id, "Someone");
            _character.DisplayName.TableReference = _other.SharedData.TableCollectionNameGuid;
            _character.DisplayName.TableEntryReference = _entry.Id;

            Assert.IsFalse(DialogueCharacterTables.SetSourceName(_character, "Changed"));

            Assert.AreEqual("Someone", DialogueTables.GetSourceText(_character.DisplayName));
            Assert.IsFalse(DialogueCharacterTables.IsOwnCollection(_character));
        }

        [Test]
        public void MoveToOwnCollection_CopiesAllLocales_KeepsSource() {
            DialogueCharacter _character = NewCharacter("Moved");
            StringTableCollection _other = LocalizationEditorSettings.CreateStringTableCollection("CharacterMoved", TablesFolder);
            SharedTableData.SharedTableEntry _entry = _other.SharedData.AddKey("someone");
            (_other.GetTable(new LocaleIdentifier(SourceCode)) as StringTable).AddEntry(_entry.Id, "Someone");
            (_other.GetTable(Other) as StringTable).AddEntry(_entry.Id, "Alguien");
            _character.DisplayName.TableReference = _other.SharedData.TableCollectionNameGuid;
            _character.DisplayName.TableEntryReference = _entry.Id;

            DialogueCharacterTables.MoveToOwnCollection(_character);

            Assert.IsTrue(DialogueCharacterTables.IsOwnCollection(_character));
            Assert.AreEqual("Someone", DialogueTables.GetText(_character.DisplayName.TableReference, _character.DisplayName.TableEntryReference, new LocaleIdentifier(SourceCode)));
            Assert.AreEqual("Alguien", DialogueTables.GetText(_character.DisplayName.TableReference, _character.DisplayName.TableEntryReference, Other));
            Assert.AreEqual("Alguien", Table(_other, OtherCode).GetEntry(_entry.Id).Value, "the original entry stays");
            Assert.AreEqual("Someone", Table(_other, SourceCode).GetEntry(_entry.Id).Value);
            Assert.IsNotNull(_other.SharedData.GetEntry("someone"));
        }

        [Test]
        public void Comment_ListsDialoguesUsingIt() {
            DialogueCharacter _character = NewCharacter("Speaker");
            DialogueCharacter _unused = NewCharacter("Unused");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_character, "Ada"));
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_unused, "Zed"));
            DialogueAsset _b = NewDialogue("Beta");
            DialogueAsset _a = NewDialogue("Alpha");
            DialogueAsset _other = NewDialogue("Other");
            _a.AddNode<LineNode>().Character = _character;
            _b.AddNode<LineNode>().Character = _character;
            _b.AddNode<LineNode>().Character = _character;
            _other.AddNode<LineNode>();
            foreach (DialogueAsset _asset in new[] { _a, _b, _other }) {
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssetIfDirty(_asset);
            }

            Assert.AreEqual("Character\nUsed in: Alpha, Beta", DialogueCharacterTables.BuildComment(_character));
            Assert.AreEqual("Character", DialogueCharacterTables.BuildComment(_unused));

            // Saving the dialogues already refreshed the comments through the save hook
            DialogueCharacterTables.Refresh();
            StringTableCollection _collection = DialogueCharacterTables.Find();
            Assert.AreEqual("Character\nUsed in: Alpha, Beta", _collection.SharedData.GetEntry(Key(_character)).Metadata.GetMetadata<Comment>().CommentText);
            Assert.IsFalse(DialogueCharacterTables.Refresh(), "nothing changed the second time");
        }

        [Test]
        public void Refresh_OrdersEntriesByName() {
            DialogueCharacter _zed = NewCharacter("First");
            DialogueCharacter _ada = NewCharacter("Second");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_zed, "Zed"));
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_ada, "ada"));

            DialogueCharacterTables.Refresh();

            IReadOnlyList<SharedTableData.SharedTableEntry> _entries = DialogueCharacterTables.Find().SharedData.Entries;
            Assert.AreEqual(Key(_ada), _entries[0].Key);
            Assert.AreEqual(Key(_zed), _entries[1].Key);
        }

        [Test]
        public void Validator_SpeakerNameUntranslated() {
            DialogueCharacter _character = NewCharacter("Spoken");
            Assert.IsTrue(DialogueCharacterTables.SetSourceName(_character, "Ada"));
            DialogueAsset _asset = NewDialogue("SpokenDialogue");
            LineNode _line = _asset.AddNode<LineNode>();
            _line.Character = _character;
            _asset.EnsureIds();
            List<DialogueIssue> _issues = new List<DialogueIssue>();

            DialogueValidator.Validate(_asset, null, _issues);
            Assert.IsTrue(_issues.Exists(_i => _i.NodeId == _line.Id && _i.Severity == DialogueIssueSeverity.Info && _i.Message.StartsWith("Speaker name untranslated in: ") && _i.Message.Contains(OtherCode)));

            Assert.IsTrue(DialogueCharacterTables.SetName(_character, Other, "Ada ES"));
            _issues.Clear();
            DialogueValidator.Validate(_asset, null, _issues);
            Assert.IsFalse(_issues.Exists(_i => _i.Message.StartsWith("Speaker name untranslated") && _i.Message.Contains(OtherCode)));
        }
        #endregion
    }
}
