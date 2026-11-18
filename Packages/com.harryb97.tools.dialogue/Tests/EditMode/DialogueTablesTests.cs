using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class DialogueTablesTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/Dialogues";
        private const string TablesFolder = TempFolder + "/Tables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private const int MaxWaitFrames = 200;

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);

            // Reuse the project's localization setup; add only what is missing and undo it afterwards
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/LocalizationSettings.asset");
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

        private void EnsureLocale(string _code) {
            if (LocalizationEditorSettings.GetLocale(_code) != null) return;

            EnsureFolder(LocalesFolder);
            Locale _locale = Locale.CreateLocale(_code);
            AssetDatabase.CreateAsset(_locale, LocalesFolder + "/" + _code + ".asset");
            LocalizationEditorSettings.AddLocale(_locale);
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
        [Test]
        public void EnsureCollection_CreatesOncePerAsset() {
            DialogueAsset _asset = NewAsset("TablesOnce");

            StringTableCollection _first = DialogueTables.EnsureCollection(_asset);
            StringTableCollection _second = DialogueTables.EnsureCollection(_asset);

            Assert.IsNotNull(_first);
            Assert.AreSame(_first, _second);
            Assert.AreEqual("Dialogue_TablesOnce", _first.TableCollectionName);
            StringAssert.StartsWith(TablesFolder + "/", AssetDatabase.GetAssetPath(_first));
            Assert.AreEqual(LocalizationEditorSettings.GetLocales().Count, _first.StringTables.Count);
            Assert.AreEqual(_first.SharedData.TableCollectionNameGuid.ToString("N"), _asset.TableCollectionGuid);
        }

        [Test]
        public void SetSourceText_CreatesKeyAndEntry_AssignsReference() {
            DialogueAsset _asset = NewAsset("TablesWrite");
            LineNode _line = _asset.AddNode<LineNode>();

            bool _written = DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");

            Assert.IsTrue(_written);
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            SharedTableData.SharedTableEntry _entry = _collection.SharedData.GetEntry(_line.Id);
            Assert.IsNotNull(_entry);
            Assert.AreEqual("Hello", Table(_collection, SourceCode).GetEntry(_entry.Id)?.Value);
            Assert.IsNull(Table(_collection, OtherCode).GetEntry(_entry.Id));
            Assert.AreEqual(_collection.SharedData.TableCollectionNameGuid, _line.Text.TableReference.TableCollectionNameGuid);
            Assert.AreEqual(_entry.Id, _line.Text.TableEntryReference.KeyId);
        }

        [Test]
        public void SetSourceText_Again_UpdatesSameEntry() {
            DialogueAsset _asset = NewAsset("TablesUpdate");
            LineNode _line = _asset.AddNode<LineNode>();

            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "First");
            long _firstId = _line.Text.TableEntryReference.KeyId;
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Second");

            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            Assert.AreEqual(1, _collection.SharedData.Entries.Count);
            Assert.AreEqual(_firstId, _line.Text.TableEntryReference.KeyId);
            Assert.AreEqual("Second", Table(_collection, SourceCode).GetEntry(_firstId)?.Value);
        }

        [Test]
        public void Key_IsOwnerIdPlusSuffix() {
            DialogueAsset _asset = NewAsset("TablesKeys");
            LineNode _line = _asset.AddNode<LineNode>();
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.Add(new ChoiceOption());
            _asset.EnsureIds();
            ChoiceOption _option = _choice.Options[0];

            using (SerializedObject _object = new SerializedObject(_asset)) {
                SerializedProperty _lineText = _object.FindProperty("nodes.Array.data[1].text");
                SerializedProperty _optionText = _object.FindProperty("nodes.Array.data[2].options.Array.data[0].text");
                SerializedProperty _locked = _object.FindProperty("nodes.Array.data[2].options.Array.data[0].lockedReason");

                Assert.AreEqual(_line.Id, DialogueTables.KeyFor(_lineText, SuffixOf(typeof(LineNode), "text")));
                Assert.AreEqual(_option.Id, DialogueTables.KeyFor(_optionText, SuffixOf(typeof(ChoiceOption), "text")));
                Assert.AreEqual(_option.Id + ".locked", DialogueTables.KeyFor(_locked, SuffixOf(typeof(ChoiceOption), "lockedReason")));
            }
        }

        [Test]
        public void RenameAsset_KeepsCollection() {
            DialogueAsset _asset = NewAsset("TablesBefore");
            StringTableCollection _before = DialogueTables.EnsureCollection(_asset);
            int _count = LocalizationEditorSettings.GetStringTableCollections().Count;

            string _error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_asset), "TablesAfter");
            Assert.IsEmpty(_error);
            StringTableCollection _after = DialogueTables.EnsureCollection(_asset);

            Assert.AreEqual("TablesAfter", _asset.name);
            Assert.AreSame(_before, _after);
            Assert.AreEqual("Dialogue_TablesBefore", _after.TableCollectionName);
            Assert.AreEqual(_count, LocalizationEditorSettings.GetStringTableCollections().Count);
        }

        [Test]
        public void GetSourceText_RoundTrip() {
            DialogueAsset _asset = NewAsset("TablesRead");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.Add(new ChoiceOption());
            _asset.EnsureIds();
            ChoiceOption _option = _choice.Options[0];

            Assert.IsNull(DialogueTables.GetSourceText(_option.Text));
            DialogueTables.SetSourceText(_asset, _option.Text, _option.Id, "Yes");
            DialogueTables.SetSourceText(_asset, _option.LockedReason, _option.Id + ".locked", "Not yet");

            Assert.AreEqual("Yes", DialogueTables.GetSourceText(_option.Text));
            Assert.AreEqual("Not yet", DialogueTables.GetSourceText(_option.LockedReason));
        }

        [Test]
        public void SetSourceText_UnknownSourceLocale_CreatesNothing() {
            DialogueAsset _asset = NewAsset("TablesNoLocale");
            LineNode _line = _asset.AddNode<LineNode>();
            settings.SourceLocaleCode = "zz";

            bool _written = DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");

            Assert.IsFalse(_written);
            Assert.IsNull(DialogueTables.SourceLocale());
            Assert.IsEmpty(_asset.TableCollectionGuid);
            Assert.IsTrue(_line.Text.IsEmpty);
            Assert.IsNull(LocalizationEditorSettings.GetStringTableCollection("Dialogue_TablesNoLocale"));
        }

        [UnityTest]
        public IEnumerator Duplicate_GetsOwnCollectionAndIds_TextCopied() {
            DialogueAsset _original = NewAsset("TablesSource");
            LineNode _line = _original.AddNode<LineNode>();
            ChoiceNode _choice = _original.AddNode<ChoiceNode>();
            _choice.Options.Add(new ChoiceOption());
            _original.EnsureIds();
            ChoiceOption _option = _choice.Options[0];
            _original.Start.Next.Target = _line.Id;
            _line.Next.Target = _choice.Id;
            DialogueTables.SetSourceText(_original, _line.Text, _line.Id, "Hello");
            DialogueTables.SetSourceText(_original, _option.Text, _option.Id, "Yes");
            DialogueTables.SetSourceText(_original, _option.LockedReason, _option.Id + ".locked", "Not yet");
            StringTableCollection _originalCollection = DialogueTables.EnsureCollection(_original);
            long _lineKeyId = _line.Text.TableEntryReference.KeyId;
            Table(_originalCollection, OtherCode).AddEntry(_lineKeyId, "Hola");
            EditorUtility.SetDirty(Table(_originalCollection, OtherCode));
            AssetDatabase.SaveAssets();

            string _copyPath = AssetsFolder + "/TablesCopy.asset";
            Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(_original), _copyPath));
            DialogueAsset _copy = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_copyPath);
            for (int _i = 0; _i < MaxWaitFrames && string.IsNullOrEmpty(_copy.TableCollectionGuid); _i++) yield return null;

            LineNode _copyLine = (LineNode) _copy.Nodes[1];
            ChoiceNode _copyChoice = (ChoiceNode) _copy.Nodes[2];
            ChoiceOption _copyOption = _copyChoice.Options[0];
            StringTableCollection _copyCollection = DialogueTables.EnsureCollection(_copy);

            Assert.AreEqual(AssetDatabase.AssetPathToGUID(_copyPath), _copy.AssetId);
            Assert.AreNotEqual(_line.Id, _copyLine.Id);
            Assert.AreNotEqual(_choice.Id, _copyChoice.Id);
            Assert.AreNotEqual(_option.Id, _copyOption.Id);
            Assert.AreNotEqual(_line.Next.Id, _copyLine.Next.Id);
            Assert.AreEqual(_copyLine.Id, _copy.Start.Next.Target);
            Assert.AreEqual(_copyChoice.Id, _copyLine.Next.Target);

            Assert.IsNotNull(_copyCollection);
            Assert.AreNotSame(_originalCollection, _copyCollection);
            Assert.AreEqual("Dialogue_TablesCopy", _copyCollection.TableCollectionName);
            Assert.AreEqual(_copyCollection.SharedData.TableCollectionNameGuid, _copyLine.Text.TableReference.TableCollectionNameGuid);
            Assert.AreEqual(_copyCollection.SharedData.GetEntry(_copyLine.Id).Id, _copyLine.Text.TableEntryReference.KeyId);
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_copyLine.Text));
            Assert.AreEqual("Hola", Table(_copyCollection, OtherCode).GetEntry(_copyLine.Text.TableEntryReference.KeyId)?.Value);
            Assert.AreEqual("Yes", DialogueTables.GetSourceText(_copyOption.Text));
            Assert.AreEqual("Not yet", DialogueTables.GetSourceText(_copyOption.LockedReason));
            Assert.IsNotNull(_copyCollection.SharedData.GetEntry(_copyOption.Id + ".locked"));

            DialogueTables.SetSourceText(_copy, _copyLine.Text, _copyLine.Id, "Changed");

            Assert.AreEqual("Changed", DialogueTables.GetSourceText(_copyLine.Text));
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_line.Text));
            Assert.AreEqual("Yes", DialogueTables.GetSourceText(_option.Text));
            Assert.AreEqual(_originalCollection.SharedData.TableCollectionNameGuid, _line.Text.TableReference.TableCollectionNameGuid);
        }

        [UnityTest]
        public IEnumerator Rename_DoesNotDuplicateCollection() {
            DialogueAsset _asset = NewAsset("TablesRenameA");
            LineNode _line = _asset.AddNode<LineNode>();
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");
            AssetDatabase.SaveAssets();
            string _lineId = _line.Id;
            string _link = _asset.TableCollectionGuid;
            string _assetId = _asset.AssetId;
            int _count = LocalizationEditorSettings.GetStringTableCollections().Count;

            string _path = AssetDatabase.GetAssetPath(_asset);
            Assert.IsEmpty(AssetDatabase.RenameAsset(_path, "TablesRenameB"));
            AssetDatabase.ImportAsset(AssetsFolder + "/TablesRenameB.asset", ImportAssetOptions.ForceUpdate);
            for (int _i = 0; _i < 10; _i++) yield return null;

            Assert.AreEqual(_assetId, _asset.AssetId);
            Assert.AreEqual(_lineId, _asset.Nodes[1].Id);
            Assert.AreEqual(_link, _asset.TableCollectionGuid);
            Assert.AreEqual(_count, LocalizationEditorSettings.GetStringTableCollections().Count);
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(((LineNode) _asset.Nodes[1]).Text));
        }

        [Test]
        public void SetText_WritesEntryInTargetLocale() {
            DialogueAsset _asset = NewAsset("TablesTranslate");
            LineNode _line = _asset.AddNode<LineNode>();
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");

            bool _written = DialogueTables.SetText(_asset, _line.Text, _line.Id, new LocaleIdentifier(OtherCode), "Hola");

            Assert.IsTrue(_written);
            Assert.AreEqual("Hola", DialogueTables.GetText(_line.Text.TableReference, _line.Text.TableEntryReference, new LocaleIdentifier(OtherCode)));
            Assert.AreEqual(1, DialogueTables.EnsureCollection(_asset).SharedData.Entries.Count, "same key as the source text");
        }

        [Test]
        public void SetText_MissingEntry_CreatesItAndPointsTarget() {
            DialogueAsset _asset = NewAsset("TablesTranslateNew");
            LineNode _line = _asset.AddNode<LineNode>();
            Assert.IsTrue(_line.Text.IsEmpty);

            bool _written = DialogueTables.SetText(_asset, _line.Text, _line.Id, new LocaleIdentifier(OtherCode), "Hola");

            Assert.IsTrue(_written);
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            SharedTableData.SharedTableEntry _entry = _collection.SharedData.GetEntry(_line.Id);
            Assert.IsNotNull(_entry, "entry keyed by the node id");
            Assert.AreEqual("Hola", Table(_collection, OtherCode).GetEntry(_entry.Id)?.Value);
            Assert.AreEqual(_entry.Id, _line.Text.TableEntryReference.KeyId);
        }

        [Test]
        public void SetText_TargetInOtherCollection_RepointsAndKeepsItsText() {
            DialogueAsset _other = NewAsset("TablesTranslateOther");
            LineNode _otherLine = _other.AddNode<LineNode>();
            DialogueTables.SetSourceText(_other, _otherLine.Text, _otherLine.Id, "Shared");
            DialogueAsset _asset = NewAsset("TablesTranslateOwn");
            LineNode _line = _asset.AddNode<LineNode>();
            // Points at the other dialogue's entry, as a duplicate does before its texts are adopted
            _line.Text.TableReference = _otherLine.Text.TableReference;
            _line.Text.TableEntryReference = _otherLine.Text.TableEntryReference;
            LocaleIdentifier _es = new LocaleIdentifier(OtherCode);

            Assert.IsTrue(DialogueTables.SetText(_asset, _line.Text, _line.Id, _es, "Compartido"));

            StringTableCollection _own = DialogueTables.EnsureCollection(_asset);
            Assert.AreEqual(_own.SharedData.TableCollectionNameGuid, _line.Text.TableReference.TableCollectionNameGuid, "points at the dialogue's own collection");
            Assert.AreEqual("Compartido", DialogueTables.GetText(_line.Text.TableReference, _line.Text.TableEntryReference, _es), "the read follows the write");
            Assert.AreEqual("Shared", DialogueTables.GetSourceText(_line.Text), "the source text came along");
            Assert.IsNull(DialogueTables.GetText(_otherLine.Text.TableReference, _otherLine.Text.TableEntryReference, _es), "the other dialogue is untouched");
        }

        [Test]
        public void SetText_LeavesSourceLocaleUntouched() {
            DialogueAsset _asset = NewAsset("TablesTranslateSource");
            LineNode _line = _asset.AddNode<LineNode>();
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");
            int _raised = 0;
            void Count(DialogueAsset _) => _raised++;
            DialogueTables.SourceTextWritten += Count;
            try {
                DialogueTables.SetText(_asset, _line.Text, _line.Id, new LocaleIdentifier(OtherCode), "Hola");
            } finally {
                DialogueTables.SourceTextWritten -= Count;
            }

            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_line.Text));
            Assert.AreEqual(0, _raised, "no source text was written");
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static string SuffixOf(System.Type _type, string _field) {
            FieldInfo _info = _type.GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic);
            DialogueTextAttribute _attribute = _info?.GetCustomAttribute<DialogueTextAttribute>();
            Assert.IsNotNull(_attribute, _type.Name + "." + _field + " has no [DialogueText]");
            return _attribute.KeySuffix;
        }
        #endregion
    }
}
