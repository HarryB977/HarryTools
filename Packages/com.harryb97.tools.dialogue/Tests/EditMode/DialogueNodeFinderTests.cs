using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueNodeFinderTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/NodeFinder";
        private const string TablesFolder = TempFolder + "/NodeFinderTables";
        private const string LocalesFolder = TempFolder + "/NodeFinderLocales";
        private const string SourceCode = "en";

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        private DialogueCharacter character;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/NodeFinderLocalization.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = createdSettings;
            }
            if (LocalizationEditorSettings.GetLocale(SourceCode) == null) {
                EnsureFolder(LocalesFolder);
                Locale _locale = Locale.CreateLocale(SourceCode);
                AssetDatabase.CreateAsset(_locale, LocalesFolder + "/" + SourceCode + ".asset");
                LocalizationEditorSettings.AddLocale(_locale);
                addedLocales.Add(_locale);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            for (int _i = 0; _i < addedLocales.Count; _i++) {
                if (addedLocales[_i] != null) LocalizationEditorSettings.RemoveLocale(addedLocales[_i]);
            }
            addedLocales.Clear();
            if (createdSettings != null) LocalizationEditorSettings.ActiveLocalizationSettings = null;
            createdSettings = null;

            if (AssetDatabase.IsValidFolder(LocalesFolder)) AssetDatabase.DeleteAsset(LocalesFolder);
            if (AssetDatabase.IsValidFolder(TempFolder) && AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0) AssetDatabase.DeleteAsset(TempFolder);
        }

        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(TempFolder);
            EnsureFolder(AssetsFolder);
            character = ScriptableObject.CreateInstance<DialogueCharacter>();
            character.name = "Narrator";
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(character);
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            if (AssetDatabase.IsValidFolder(TablesFolder)) {
                foreach (string _guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { TablesFolder })) {
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(_guid));
                }
                AssetDatabase.DeleteAsset(TablesFolder);
            }
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
            if (AssetDatabase.IsValidFolder(TempFolder) && AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0) AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
        }

        private static DialogueAsset NewAsset(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            _asset.EnsureIds();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Finder_MatchesLineOptionCharacterNoteAndType() {
            DialogueAsset _asset = NewAsset("Finder");
            LineNode _spoken = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            DialogueTables.SetSourceText(_asset, _spoken.Text, _spoken.Id, "The old Lighthouse is dark");
            _spoken.Character = character;
            LineNode _noted = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            _noted.WriterNote = "Rewrite this later";
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateNode(_asset, typeof(ChoiceNode), Vector2.zero);
            ChoiceOption _option = DialogueGraphOps.AddOption(_asset, _choice);
            DialogueTables.SetSourceText(_asset, _option.Text, _option.Id, "Go north");
            DialogueNode _actions = DialogueGraphOps.CreateNode(_asset, typeof(ActionsNode), Vector2.zero);

            CollectionAssert.AreEqual(new[] { _spoken }, DialogueNodeFinder.Find(_asset, "LIGHTHOUSE"), "line text");
            CollectionAssert.AreEqual(new[] { _spoken }, DialogueNodeFinder.Find(_asset, "narrator"), "character");
            CollectionAssert.AreEqual(new[] { _noted }, DialogueNodeFinder.Find(_asset, "rewrite"), "writer note");
            CollectionAssert.AreEqual(new[] { _choice }, DialogueNodeFinder.Find(_asset, "North"), "option text");
            CollectionAssert.AreEqual(new[] { _actions }, DialogueNodeFinder.Find(_asset, "actions"), "type");
            CollectionAssert.AreEqual(new DialogueNode[] { _spoken, _noted }, DialogueNodeFinder.Find(_asset, "line"), "type, in asset order");
            Assert.AreEqual(0, DialogueNodeFinder.Find(_asset, "nothing like this").Count);
        }

        [Test]
        public void Finder_EmptyQuery_ReturnsNothing() {
            DialogueAsset _asset = NewAsset("FinderEmpty");
            DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);

            Assert.AreEqual(0, DialogueNodeFinder.Find(_asset, "").Count);
            Assert.AreEqual(0, DialogueNodeFinder.Find(_asset, "   ").Count);
            Assert.AreEqual(0, DialogueNodeFinder.Find(_asset, null).Count);
            Assert.AreEqual(0, DialogueNodeFinder.Find(null, "line").Count);
        }
        #endregion
    }
}
