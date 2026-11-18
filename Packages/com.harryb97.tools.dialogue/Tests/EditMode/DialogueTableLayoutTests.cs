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
    public class DialogueTableLayoutTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/Layout";
        private const string TablesFolder = TempFolder + "/LayoutTables";
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
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/LayoutLocalizationSettings.asset");
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
            _asset.EnsureIds();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        private static string LineKey(LineNode _line) {
            return _line.Id + DialogueTables.KeySuffix(typeof(LineNode), "text");
        }

        private static string OptionKey(ChoiceOption _option) {
            return _option.Id + DialogueTables.KeySuffix(typeof(ChoiceOption), "text");
        }

        private static string LockedKey(ChoiceOption _option) {
            return _option.Id + DialogueTables.KeySuffix(typeof(ChoiceOption), "lockedReason");
        }

        private static LineNode AddLine(DialogueAsset _asset, string _text) {
            LineNode _line = _asset.AddNode<LineNode>();
            _asset.EnsureIds();
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, LineKey(_line), _text));
            return _line;
        }

        private static ChoiceOption AddOption(DialogueAsset _asset, ChoiceNode _choice, string _text, string _locked) {
            ChoiceOption _option = new ChoiceOption();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _option.Text, OptionKey(_option), _text));
            if (_locked != null) Assert.IsTrue(DialogueTables.SetSourceText(_asset, _option.LockedReason, LockedKey(_option), _locked));
            return _option;
        }

        private static string CommentOf(DialogueAsset _asset, string _key) {
            return DialogueTables.EnsureCollection(_asset).SharedData.GetEntry(_key).Metadata.GetMetadata<Comment>()?.CommentText;
        }

        private static List<string> EntryKeys(DialogueAsset _asset) {
            List<string> _keys = new List<string>();
            foreach (SharedTableData.SharedTableEntry _entry in DialogueTables.EnsureCollection(_asset).SharedData.Entries) _keys.Add(_entry.Key);
            return _keys;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Comment_Line_HasTypeDialogueSpeakerOriginNote() {
            DialogueAsset _asset = NewAsset("CommentLine");
            DialogueCharacter _character = ScriptableObject.CreateInstance<DialogueCharacter>();
            AssetDatabase.CreateAsset(_character, AssetsFolder + "/Speaker.asset");
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _character.DisplayName, "speaker.name", "Ada"));
            LineNode _line = AddLine(_asset, "Hello");
            _line.Character = _character;
            _line.TranslatorNote = "Formal register";
            _line.WriterNote = "Secret";
            _asset.Start.Next.Target = _line.Id;

            string _comment = DialogueTableLayout.BuildComment(_asset, LineKey(_line));

            Assert.AreEqual("Line\nDialogue: CommentLine\nSpeaker: Ada\nAfter: start\nNote: Formal register", _comment);
        }

        [Test]
        public void Comment_Option_AfterLine_And_LockedReason() {
            DialogueAsset _asset = NewAsset("CommentOption");
            LineNode _line = AddLine(_asset, "Question");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _option = AddOption(_asset, _choice, "Yes", "Not yet");
            _option.TranslatorNote = "Short";
            _asset.Start.Next.Target = _line.Id;
            _line.Next.Target = _choice.Id;

            Assert.AreEqual("Option\nDialogue: CommentOption\nAfter: start\nNote: Short", DialogueTableLayout.BuildComment(_asset, OptionKey(_option)));
            Assert.AreEqual("Locked reason\nDialogue: CommentOption\nAfter: start", DialogueTableLayout.BuildComment(_asset, LockedKey(_option)));
        }

        [Test]
        public void Comment_MultipleOrigins_JoinedWithSemicolon() {
            DialogueAsset _asset = NewAsset("CommentOrigins");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _yes = AddOption(_asset, _choice, "Yes", null);
            BranchNode _branch = _asset.AddNode<BranchNode>();
            _branch.Cases.Add(new BranchCase());
            LineNode _target = AddLine(_asset, "Shared");
            _asset.Start.Next.Target = _choice.Id;
            _yes.Next.Target = _branch.Id;
            _branch.Cases[0].Next.Target = _target.Id;
            _branch.Otherwise.Target = _target.Id;

            string _comment = DialogueTableLayout.BuildComment(_asset, LineKey(_target));

            Assert.AreEqual("Line\nDialogue: CommentOrigins\nBranch case 1; Otherwise", _comment);
        }

        [Test]
        public void Comment_NoWriterNote() {
            DialogueAsset _asset = NewAsset("CommentWriter");
            LineNode _line = AddLine(_asset, "Hello");
            _line.WriterNote = "Never exported";
            _asset.Start.Next.Target = _line.Id;

            string _comment = DialogueTableLayout.BuildComment(_asset, LineKey(_line));

            StringAssert.DoesNotContain("Never exported", _comment);
            StringAssert.DoesNotContain("Note:", _comment);
        }

        [Test]
        public void Order_BreadthFirst_OptionsAfterChoice_UnreachableLast() {
            DialogueAsset _asset = NewAsset("Order");
            LineNode _unreachable = AddLine(_asset, "Lost");
            LineNode _first = AddLine(_asset, "First");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _a = AddOption(_asset, _choice, "A", "Locked A");
            ChoiceOption _b = AddOption(_asset, _choice, "B", "Locked B");
            LineNode _afterA = AddLine(_asset, "After A");
            LineNode _afterB = AddLine(_asset, "After B");
            _asset.Start.Next.Target = _first.Id;
            _first.Next.Target = _choice.Id;
            _a.Next.Target = _afterA.Id;
            _b.Next.Target = _afterB.Id;

            IReadOnlyList<string> _order = DialogueTableLayout.ScriptOrder(_asset);

            CollectionAssert.AreEqual(new[] { LineKey(_first), OptionKey(_a), OptionKey(_b), LockedKey(_a), LockedKey(_b), LineKey(_afterA), LineKey(_afterB), LineKey(_unreachable) }, _order);

            DialogueTableLayout.Refresh(_asset);
            List<string> _keys = EntryKeys(_asset);
            CollectionAssert.AreEqual(_order, _keys);
        }

        [Test]
        public void Refresh_NoChange_DoesNotDirty() {
            DialogueAsset _asset = NewAsset("RefreshClean");
            LineNode _line = AddLine(_asset, "Hello");
            _asset.Start.Next.Target = _line.Id;
            DialogueTableLayout.Refresh(_asset);
            SharedTableData _shared = DialogueTables.EnsureCollection(_asset).SharedData;
            AssetDatabase.SaveAssets();
            Assert.IsFalse(EditorUtility.IsDirty(_shared));

            DialogueTableLayout.Refresh(_asset);

            Assert.IsFalse(EditorUtility.IsDirty(_shared));
            Assert.AreEqual("Line\nDialogue: RefreshClean\nAfter: start", CommentOf(_asset, LineKey(_line)));
        }

        [Test]
        public void SaveHook_RefreshesSavedDialogue() {
            DialogueAsset _asset = NewAsset("SaveHook");
            LineNode _line = AddLine(_asset, "Hello");
            _asset.Start.Next.Target = _line.Id;
            Assert.IsNull(CommentOf(_asset, LineKey(_line)));

            DialogueTableSaveHook.OnWillSaveAssets(new[] { AssetDatabase.GetAssetPath(_asset), "Assets/Other.txt" });

            Assert.AreEqual("Line\nDialogue: SaveHook\nAfter: start", CommentOf(_asset, LineKey(_line)));
        }
        #endregion
    }
}
