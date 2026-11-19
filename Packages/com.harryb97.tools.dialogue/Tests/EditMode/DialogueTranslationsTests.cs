using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueTranslationsTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/TranslationDialogues";
        private const string TablesFolder = TempFolder + "/TranslationTables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private const int MaxWaitFrames = 200;

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

        private static DialogueAsset NewAsset(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        private static StringTable Table(StringTableCollection _collection, string _code) {
            return _collection.GetTable(new LocaleIdentifier(_code)) as StringTable;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        private LineNode NewLine(out DialogueAsset _asset, string _source) {
            _asset = NewAsset("Translations");
            LineNode _line = _asset.AddNode<LineNode>();
            _asset.EnsureIds();
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, _source));
            return _line;
        }

        private static readonly LocaleIdentifier Other = new LocaleIdentifier(OtherCode);

        [Test]
        public void State_NoEntry_Untranslated() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            Assert.AreEqual(TranslationState.Untranslated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual(TranslationState.Untranslated, DialogueTranslations.State(_collection, "missing", Other));
        }

        [Test]
        public void State_EmptyText_Untranslated() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "");

            Assert.AreEqual(TranslationState.Untranslated, DialogueTranslations.State(DialogueTables.Linked(_asset), _line.Id, Other));
        }

        [Test]
        public void State_AfterWrite_UpToDate() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
        }

        [Test]
        public void State_SourceChanged_Outdated_PreviousSourceShown() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello there");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
            Assert.AreEqual("Hola", DialogueTables.GetText(_line.Text.TableReference, _line.Text.TableEntryReference, Other), "the translation text itself is untouched");
        }

        [Test]
        public void Outdated_SourceRestored_UpToDate() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(DialogueTables.Linked(_asset), _line.Id, Other));
        }

        [Test]
        public void MarkReviewed_UpToDate_UndoRestoresOutdated() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Undo.IncrementCurrentGroup();

            DialogueTranslations.MarkReviewed(_collection, _line.Id, Other);
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Changed", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));

            Undo.PerformUndo();
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
        }

        [Test]
        public void MarkReviewed_AllLocales_ClearsEveryOutdatedMark() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            DialogueTranslations.MarkReviewed(_collection, _line.Id);

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
        }

        [Test]
        public void SetText_WriteAndStamp_UndoneInOneStep() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Undo.IncrementCurrentGroup();

            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));

            Undo.PerformUndo();
            Assert.AreEqual(TranslationState.Untranslated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.IsNull(DialogueTranslations.PreviousSource(_collection, _line.Id, Other), "the stamp went with the text");
        }

        [Test]
        public void MarkReviewed_AllLocales_UndoRestoresOutdated() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Undo.IncrementCurrentGroup();

            DialogueTranslations.MarkReviewed(_collection, _line.Id);
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));

            Undo.PerformUndo();
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
        }

        [Test]
        public void LegacyTranslation_NoSnapshot_OutdatedAfterSourceChange() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            // Written straight into the table, as before snapshots existed
            StringTable _table = Table(_collection, OtherCode);
            _table.AddEntry(_collection.SharedData.GetEntry(_line.Id).Id, "Hola");
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));

            // Writing the same text is no change
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");
            Assert.IsNull(DialogueTranslations.PreviousSource(_collection, _line.Id, Other));

            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");

            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));

            DialogueTranslations.MarkReviewed(_collection, _line.Id, Other);
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.AreEqual("Changed", DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
        }

        [Test]
        public void LegacyTranslation_SourceChangeUndone_StampUndoneToo() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            Table(_collection, OtherCode).AddEntry(_collection.SharedData.GetEntry(_line.Id).Id, "Hola");
            Undo.IncrementCurrentGroup();

            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            Undo.PerformUndo();

            Assert.AreEqual("Hello", DialogueTranslations.SourceTextOf(_collection, _line.Id));
            Assert.IsNull(DialogueTranslations.PreviousSource(_collection, _line.Id, Other));
            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
        }

        [UnityTest]
        public IEnumerator Duplicate_KeepsOutdatedMarks_NotSheetRecord() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Changed");
            StringTableCollection _collection = DialogueTables.Linked(_asset);
            StringTable _table = Table(_collection, OtherCode);
            _table.GetEntry(_collection.SharedData.GetEntry(_line.Id).Id).AddMetadata(new SheetSyncSnapshot { SyncedText = "Hola" });
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
            TestAddressables.SaveOwn(_collection, _asset);

            string _copyPath = AssetsFolder + "/TranslationsCopy.asset";
            Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(_asset), _copyPath));
            DialogueAsset _copy = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_copyPath);
            for (int _i = 0; _i < MaxWaitFrames && string.IsNullOrEmpty(_copy.TableCollectionGuid); _i++) yield return null;

            LineNode _copyLine = (LineNode) _copy.Nodes[1];
            StringTableCollection _copyCollection = DialogueTables.Linked(_copy);
            Assert.IsNotNull(_copyCollection);
            Assert.AreNotSame(_collection, _copyCollection);
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_copyCollection, _copyLine.Id, Other));
            Assert.AreEqual("Hello", DialogueTranslations.PreviousSource(_copyCollection, _copyLine.Id, Other));
            StringTableEntry _copyEntry = Table(_copyCollection, OtherCode).GetEntry(_copyCollection.SharedData.GetEntry(_copyLine.Id).Id);
            Assert.IsNull(_copyEntry.GetMetadata<SheetSyncSnapshot>(), "the copy has a tab of its own");

            // Each copy tracks on its own
            DialogueTranslations.MarkReviewed(_copyCollection, _copyLine.Id, Other);
            Assert.AreEqual(TranslationState.Outdated, DialogueTranslations.State(_collection, _line.Id, Other));
        }

        [Test]
        public void Stamp_AndComment_DoNotInterfere() {
            LineNode _line = NewLine(out DialogueAsset _asset, "Hello");
            DialogueTables.SetText(_asset, _line.Text, _line.Id, Other, "Hola");
            StringTableCollection _collection = DialogueTables.Linked(_asset);

            DialogueTableLayout.Refresh(_asset);

            Assert.AreEqual(TranslationState.UpToDate, DialogueTranslations.State(_collection, _line.Id, Other));
            Assert.IsNotNull(_collection.SharedData.GetEntry(_line.Id).Metadata.GetMetadata<Comment>());
            Assert.IsNull(_collection.SharedData.GetEntry(_line.Id).Metadata.GetMetadata<SourceSnapshot>());
        }
        #endregion
    }
}
