using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Dialogue.Editor;
using HarryTools.TextEffects;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueValidatorTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string LocalesFolder = TempFolder + "/Locales";
        private const string AssetsFolder = TempFolder + "/Validator";
        private const string TablesFolder = TempFolder + "/ValidatorTables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        private readonly List<DialogueIssue> issues = new List<DialogueIssue>();
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/ValidatorLocalizationSettings.asset");
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
            issues.Clear();
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

        private static LineNode AddLine(DialogueAsset _asset, string _text) {
            LineNode _line = _asset.AddNode<LineNode>();
            _asset.EnsureIds();
            if (_text != null) Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, _text));
            return _line;
        }

        /// <summary> Start, one line and an end node, all connected, with nothing to report </summary>
        private static DialogueAsset Clean(string _name, out LineNode _line) {
            DialogueAsset _asset = NewAsset(_name);
            _line = AddLine(_asset, "Hello");
            EndNode _end = _asset.AddNode<EndNode>();
            _asset.EnsureIds();
            _asset.Start.Next.Target = _line.Id;
            _line.Next.Target = _end.Id;
            return _asset;
        }

        private List<DialogueIssue> Run(DialogueAsset _asset, ISet<DialogueAsset> _ambient = null) {
            issues.Clear();
            DialogueValidator.Validate(_asset, _ambient, issues);
            return issues;
        }

        private static bool Has(List<DialogueIssue> _issues, string _nodeId, string _fragment, DialogueIssueSeverity? _severity = null) {
            for (int _i = 0; _i < _issues.Count; _i++) {
                if (_nodeId != null && _issues[_i].NodeId != _nodeId) continue;
                if (_severity.HasValue && _issues[_i].Severity != _severity.Value) continue;
                if (_issues[_i].Message.Contains(_fragment)) return true;
            }
            return false;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void CleanDialogue_NoIssues() {
            DialogueAsset _asset = Clean("Clean", out _);

            Assert.IsEmpty(Run(_asset));
        }

        [Test]
        public void NoStart_Or_StartUnconnected() {
            DialogueAsset _noStart = Clean("NoStart", out _);
            _noStart.NodeList.Remove(_noStart.Start);
            Assert.IsTrue(Has(Run(_noStart), "", "no Start node"));

            DialogueAsset _loose = Clean("LooseStart", out _);
            _loose.Start.Next.Target = "";
            List<DialogueIssue> _found = Run(_loose);
            Assert.IsTrue(Has(_found, _loose.Start.Id, "Start is not connected", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void Unreachable_Node() {
            DialogueAsset _asset = Clean("Unreachable", out _);
            LineNode _island = AddLine(_asset, "Island");

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, _island.Id, "not reachable", DialogueIssueSeverity.Warning));
            Assert.IsFalse(Has(_found, _asset.Start.Id, "not reachable"));
        }

        [Test]
        public void UnconnectedOutput_IsInfo() {
            DialogueAsset _asset = Clean("Unconnected", out LineNode _line);
            _line.Next.Target = "";

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, _line.Id, "Output is not connected", DialogueIssueSeverity.Info));
        }

        [Test]
        public void NoAnswerUnconnected_OnlyWithNoAnswerRule() {
            DialogueAsset _asset = Clean("NoAnswer", out LineNode _line);
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _option = new ChoiceOption();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            DialogueTables.SetSourceText(_asset, _option.Text, _option.Id, "Yes");
            _option.IsDefault = true;
            _option.Next.Target = _line.Next.Target;
            _line.Next.Target = _choice.Id;

            _choice.UseTimer = true;
            _choice.TimeoutRule = TimeoutRule.ChooseSelected;
            Assert.IsFalse(Has(Run(_asset), _choice.Id, "No Answer"));

            _choice.TimeoutRule = TimeoutRule.NoAnswer;
            Assert.IsTrue(Has(Run(_asset), _choice.Id, "No Answer output is not connected", DialogueIssueSeverity.Info));

            _choice.UseTimer = false;
            Assert.IsFalse(Has(Run(_asset), _choice.Id, "No Answer"));

            _choice.UseTimer = true;
            _choice.NoAnswer.Target = _option.Next.Target;
            Assert.IsFalse(Has(Run(_asset), _choice.Id, "No Answer"));
        }

        [Test]
        public void OutputToDeletedNode_Warns() {
            DialogueAsset _asset = Clean("Dangling", out LineNode _line);
            _line.Next.Target = "gone";

            Assert.IsTrue(Has(Run(_asset), _line.Id, "no longer exists", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void BrokenCharacterReference() {
            DialogueAsset _asset = Clean("Character", out LineNode _line);
            DialogueCharacter _character = ScriptableObject.CreateInstance<DialogueCharacter>();
            string _path = AssetsFolder + "/Speaker.asset";
            AssetDatabase.CreateAsset(_character, _path);
            _line.Character = _character;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            Assert.IsFalse(Has(Run(_asset), _line.Id, "Character"));

            AssetDatabase.DeleteAsset(_path);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(_asset), ImportAssetOptions.ForceUpdate);
            DialogueAsset _reloaded = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GetAssetPath(_asset));

            Assert.IsTrue(Has(Run(_reloaded), _line.Id, "Character asset is missing", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void ChoiceInAmbientLaunchedDialogue() {
            DialogueAsset _asset = Clean("Ambient", out LineNode _line);
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _asset.EnsureIds();
            _line.Next.Target = _choice.Id;

            Assert.IsFalse(Has(Run(_asset, new HashSet<DialogueAsset>()), _choice.Id, "ambient"));
            Assert.IsFalse(Has(Run(_asset, null), _choice.Id, "ambient"));
            Assert.IsTrue(Has(Run(_asset, new HashSet<DialogueAsset> { _asset }), _choice.Id, "ambient", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void TimerChooseSelected_NoDefault() {
            DialogueAsset _asset = Clean("Timer", out LineNode _line);
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _option = new ChoiceOption();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            DialogueTables.SetSourceText(_asset, _option.Text, _option.Id, "Yes");
            _line.Next.Target = _choice.Id;
            _choice.UseTimer = true;
            _choice.TimeoutRule = TimeoutRule.ChooseSelected;

            Assert.IsTrue(Has(Run(_asset), _choice.Id, "default", DialogueIssueSeverity.Warning));

            _option.IsDefault = true;
            Assert.IsFalse(Has(Run(_asset), _choice.Id, "default"));

            _option.IsDefault = false;
            _choice.UseTimer = false;
            Assert.IsFalse(Has(Run(_asset), _choice.Id, "default"));
        }

        [Test]
        public void EmptyLineText_EmptyOptionText() {
            DialogueAsset _asset = Clean("EmptyText", out LineNode _line);
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "   ");
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _option = new ChoiceOption();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            _line.Next.Target = _choice.Id;

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, _line.Id, "Line has no text", DialogueIssueSeverity.Warning));
            Assert.IsTrue(Has(_found, _choice.Id, "Option has no text", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void UnknownTag_And_MissingEvent() {
            DialogueAsset _asset = Clean("Tags", out LineNode _line);
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hi <voice=zzNoSuchVoice>there<event=ghost>");

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, _line.Id, "zzNoSuchVoice"));
            Assert.IsTrue(Has(_found, _line.Id, "ghost"));

            _line.Events = new TextEventTable(new List<TextEventEntry> { new TextEventEntry("ghost", new ActionList()) });
            Assert.IsFalse(Has(Run(_asset), _line.Id, "ghost"));
        }

        [Test]
        public void JumpWithoutTarget() {
            DialogueAsset _asset = Clean("Jump", out LineNode _line);
            JumpNode _jump = _asset.AddNode<JumpNode>();
            _asset.EnsureIds();
            _jump.Next.Target = _line.Next.Target;
            _line.Next.Target = _jump.Id;

            Assert.IsTrue(Has(Run(_asset), _jump.Id, "Jump has no target", DialogueIssueSeverity.Warning));

            _jump.Target = _asset;
            Assert.IsFalse(Has(Run(_asset), _jump.Id, "Jump has no target"));
        }

        [Test]
        public void MissingTableEntry() {
            DialogueAsset _asset = Clean("Missing", out LineNode _line);
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            _collection.SharedData.RemoveKey(_line.Id);

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, _line.Id, "table entry", DialogueIssueSeverity.Warning));
            Assert.IsFalse(Has(_found, _line.Id, "no text"));
        }

        [Test]
        public void NullNode_SkippedWithIssue() {
            DialogueAsset _asset = Clean("NullNode", out _);
            _asset.NodeList.Add(null);

            List<DialogueIssue> _found = Run(_asset);

            Assert.IsTrue(Has(_found, "", "missing type", DialogueIssueSeverity.Warning));
        }

        [Test]
        public void FindAmbientLaunches_PrefabLaunchers_OnlyNonMain() {
            DialogueAsset _ambient = Clean("AmbientPrefab", out _);
            DialogueAsset _main = Clean("MainPrefab", out _);
            GameObject _object = new GameObject("Launchers");
            DialogueLauncher _first = _object.AddComponent<DialogueLauncher>();
            _first.Dialogue = _ambient;
            _first.IsMain = false;
            DialogueLauncher _second = _object.AddComponent<DialogueLauncher>();
            _second.Dialogue = _main;
            _second.IsMain = true;
            PrefabUtility.SaveAsPrefabAsset(_object, AssetsFolder + "/Launchers.prefab");
            Object.DestroyImmediate(_object);

            HashSet<DialogueAsset> _found = DialogueValidator.FindAmbientLaunches();

            Assert.IsTrue(_found.Contains(_ambient));
            Assert.IsFalse(_found.Contains(_main));
        }
        #endregion
    }
}
