using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Side panel of the graph window: the properties of the selected node in collapsible sections, or the dialogue options when no node is selected. Fields are drawn with the property drawers of the asset, so changes record undo through the serialized object; translations are written to the tables of their locales </summary>
    internal sealed class DialogueInspectorPanel : VisualElement {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string PanelClass = "dialogue-panel";
        private const string HeaderClass = "dialogue-panel__header";
        private const string HeaderTypeClassPrefix = "dialogue-panel__header--";
        private const string HeaderTitleClass = "dialogue-panel__header-title";
        private const string HeaderIdClass = "dialogue-panel__header-id";
        private const string SectionClass = "dialogue-panel__section";
        private const string SubSectionClass = "dialogue-panel__subsection";
        private const string ItemClass = "dialogue-panel__item";
        private const string ItemButtonsClass = "dialogue-panel__item-buttons";
        private const string TranslationClass = "dialogue-panel__translation";
        private const string DescriptionClass = "dialogue-panel__description";
        private const string NodesPath = "nodes.Array.data[";
        private const string NodeOutputType = "NodeOutput";
        private const string NodeSuffix = "Node";
        private const string SectionKeyPrefix = "HarryTools.Dialogue.Panel.Section.";
        private const string NotTranslated = "Not translated";
        private const int ShortIdLength = 8;
        private const int MaxTitleLength = 32;

        private const string TextSection = "Text";
        private const string TranslationsSection = "Translations";
        private const string TimingSection = "Timing";
        private const string ActionsSection = "Actions";
        private const string EventsSection = "Text Events";
        private const string TimerSection = "Timer";
        private const string OptionsSection = "Options";
        private const string TextTranslations = "Text Translations";
        private const string LockedTranslations = "Locked Reason Translations";

        // Sections open the first time a node type is shown
        private static readonly HashSet<string> OpenByDefault = new HashSet<string> { TextSection, TranslationsSection, TimerSection, OptionsSection, TextTranslations };

        private readonly VisualElement header;
        private readonly Label headerTitle;
        private readonly Label headerId;
        private readonly ScrollView scroll;
        // Option and case foldouts the user opened, by option id or case output id
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly List<(Foldout Foldout, int Index)> itemFoldouts = new List<(Foldout, int)>();
        private readonly List<Foldout> sections = new List<Foldout>();
        private readonly List<string> sectionTitles = new List<string>();
        // By table key and locale code
        private readonly Dictionary<string, Translation> translations = new Dictionary<string, Translation>();

        private DialogueAsset asset;
        private string signature;
        private string headerTypeClass;
        private SerializedObject serializedObject;
        private VisualElement content;
        private bool readOnly;
        #endregion

        #region Public Variables
        /// <summary> The node shown. Null when the panel shows the dialogue options </summary>
        internal string NodeId { get; private set; }

        /// <summary> Number of options of the choice or cases of the branch shown. Zero for other nodes </summary>
        internal int ItemCount => itemFoldouts.Count;

        /// <summary> Titles of the sections shown, in order </summary>
        internal IReadOnlyList<string> SectionTitles => sectionTitles;

        /// <summary> Fields and buttons are shown disabled. Changing it builds the fields again </summary>
        internal bool ReadOnly {
            get => readOnly;
            set {
                if (value == readOnly) return;
                readOnly = value;
                if (content == null) return;
                signature = null;
                Show(asset, NodeId);
            }
        }

        /// <summary> Raised with the node id after a field of the node changed </summary>
        internal event Action<string> NodeEdited;

        /// <summary> Raised with the node id after its options or cases were added, removed or reordered, so the ports have to be rebuilt </summary>
        internal event Action<string> OutputsEdited;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueInspectorPanel() {
            StyleSheet _style = AssetDatabase.LoadAssetAtPath<StyleSheet>(DialogueGraphView.StyleSheetPath);
            if (_style != null) styleSheets.Add(_style);
            AddToClassList(PanelClass);

            header = new VisualElement();
            header.AddToClassList(HeaderClass);
            headerTitle = new Label();
            headerTitle.AddToClassList(HeaderTitleClass);
            headerId = new Label();
            headerId.AddToClassList(HeaderIdClass);
            header.Add(headerTitle);
            header.Add(headerId);

            scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1f;
            Add(header);
            Add(scroll);

            RegisterCallback<AttachToPanelEvent>(_ => {
                DialogueTables.SourceTextWritten += OnSourceTextWritten;
                Undo.undoRedoPerformed += RefreshTranslations;
                // Detaching dropped the fields; a panel moved to another place shows its content again
                if (asset != null && content == null) Show(asset, NodeId);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => {
                DialogueTables.SourceTextWritten -= OnSourceTextWritten;
                Undo.undoRedoPerformed -= RefreshTranslations;
                // The native object would otherwise outlive the closed window
                content?.RemoveFromHierarchy();
                content = null;
                serializedObject?.Dispose();
                serializedObject = null;
                signature = null;
            });
        }
        #endregion

        // ==================== SHOW ====================
        #region Show
        /// <summary> Shows the node with the id, or the dialogue options when it is null or not found. Does nothing when the same content is already shown, so a field being edited keeps its focus </summary>
        internal void Show(DialogueAsset _asset, string _nodeId) {
            DialogueNode _node = _asset != null ? _asset.Find(_nodeId) : null;
            int _index = _node != null ? IndexOf(_asset, _node) : -1;
            if (_index < 0) _node = null;
            string _signature = Signature(_asset, _node, _index);
            if (_asset == asset && _signature == signature && content != null) {
                RefreshTranslations();
                return;
            }

            asset = _asset;
            NodeId = _node?.Id;
            signature = _signature;
            Build(_node, _index);
        }

        /// <summary> The section with the title. Null when it is not shown </summary>
        internal Foldout Section(string _title) {
            int _index = sectionTitles.IndexOf(_title);
            return _index >= 0 ? sections[_index] : null;
        }

        /// <summary> The translation field of the text with the table key, for the locale. Null when it is not shown </summary>
        internal TextField TranslationField(string _key, string _localeCode) {
            return translations.TryGetValue(TranslationKey(_key, _localeCode), out Translation _translation) ? _translation.Field : null;
        }

        /// <summary> The session key that remembers whether a section is open, per node type </summary>
        internal static string SectionStateKey(Type _nodeType, string _title) => SectionKeyPrefix + (_nodeType != null ? _nodeType.Name : "") + "." + _title;

        private void Build(DialogueNode _node, int _index) {
            content?.RemoveFromHierarchy();
            serializedObject?.Dispose();
            serializedObject = null;
            itemFoldouts.Clear();
            sections.Clear();
            sectionTitles.Clear();
            translations.Clear();

            content = new VisualElement();
            scroll.Add(content);

            if (asset == null) {
                SetHeader("No dialogue", null, null);
                return;
            }

            serializedObject = new SerializedObject(asset);
            if (_node == null) BuildDialogueOptions();
            else BuildNode(_node, serializedObject.FindProperty(NodesPath + _index + "]"));

            content.Bind(serializedObject);
            content.TrackSerializedObjectValue(serializedObject, _ => OnChanged());
            if (!ReadOnly) return;
            // Foldouts still open and close, so everything can be read
            content.Query<PropertyField>().ForEach(_field => _field.SetEnabled(false));
            content.Query<Button>().ForEach(_button => _button.SetEnabled(false));
            foreach (Translation _translation in translations.Values) _translation.Field.SetEnabled(false);
        }

        /// <summary> What the panel shows: the node, its position in the asset and its options or cases. A change means the fields must be built again </summary>
        private static string Signature(DialogueAsset _asset, DialogueNode _node, int _index) {
            if (_asset == null) return "";
            if (_node == null) return "dialogue";

            StringBuilder _builder = new StringBuilder();
            _builder.Append(_index).Append('|').Append(_node.Id).Append('|').Append(_node.GetType().FullName);
            switch (_node) {
                case ChoiceNode _choice:
                    for (int _i = 0; _i < _choice.Options.Count; _i++) _builder.Append('|').Append(_choice.Options[_i]?.Id);
                    break;
                case BranchNode _branch:
                    for (int _i = 0; _i < _branch.Cases.Count; _i++) _builder.Append('|').Append(_branch.Cases[_i]?.Next.Id);
                    break;
            }
            return _builder.ToString();
        }

        private static int IndexOf(DialogueAsset _asset, DialogueNode _node) {
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] == _node) return _i;
            }
            return -1;
        }

        private void SetHeader(string _title, string _typeClass, string _id) {
            headerTitle.text = _title;
            headerId.text = string.IsNullOrEmpty(_id) ? "" : "#" + (_id.Length > ShortIdLength ? _id.Substring(0, ShortIdLength) : _id);
            headerId.tooltip = string.IsNullOrEmpty(_id) ? "" : "Node id " + _id;
            headerId.style.display = string.IsNullOrEmpty(_id) ? DisplayStyle.None : DisplayStyle.Flex;
            if (headerTypeClass != null) header.RemoveFromClassList(headerTypeClass);
            headerTypeClass = _typeClass;
            if (headerTypeClass != null) header.AddToClassList(headerTypeClass);
        }
        #endregion

        // ==================== DIALOGUE OPTIONS ====================
        #region Dialogue Options
        private void BuildDialogueOptions() {
            SetHeader(asset.name, null, null);

            PropertyField _overrideTime = AddField(content, serializedObject.FindProperty("overrideTime"), "Override Time");
            PropertyField _time = AddField(content, serializedObject.FindProperty("time"), "Time", "The clock this dialogue runs on");
            PropertyField _overrideAmbient = AddField(content, serializedObject.FindProperty("overrideAmbientPolicy"), "Override Ambient Policy");
            PropertyField _ambient = AddField(content, serializedObject.FindProperty("ambientPolicy"), "Ambient Policy", "What ambient dialogues do while this one runs");
            AddField(content, serializedObject.FindProperty("allowSkip"), "Allow Skip");

            EnableWhen(_overrideTime, "overrideTime", _time);
            EnableWhen(_overrideAmbient, "overrideAmbientPolicy", _ambient);
        }
        #endregion

        // ==================== NODE ====================
        #region Node
        private void BuildNode(DialogueNode _node, SerializedProperty _property) {
            string _type = TypeName(_node);
            SetHeader(_type, HeaderTypeClassPrefix + _type.ToLowerInvariant(), _node.Id);

            switch (_node) {
                case LineNode _line: BuildLine(_line, _property); break;
                case ChoiceNode _choice: BuildChoice(_choice, _property); break;
                case BranchNode _: BuildBranch(_property); break;
                case ActionsNode _:
                    AddField(content, _property.FindPropertyRelative("actions"), "Actions", "Run when the dialogue reaches this node");
                    AddField(content, _property.FindPropertyRelative("waitForCompletion"), "Wait For Completion");
                    AddField(content, _property.FindPropertyRelative("runWhenSkipped"), "Run When Skipped");
                    break;
                case JumpNode _:
                    AddField(content, _property.FindPropertyRelative("target"), "Target", "The dialogue to move into");
                    AddField(content, _property.FindPropertyRelative("mode"), "Mode", "Whether the conversation comes back here when the target ends");
                    break;
                case StartNode _: AddDescription(content, "Where the dialogue begins. Connect its output to the first node."); break;
                case EndNode _: AddDescription(content, "Ends the dialogue here. An unconnected output also ends it."); break;
                default: BuildPlain(_property); break;
            }
        }

        private void BuildLine(LineNode _line, SerializedProperty _property) {
            Type _type = typeof(LineNode);
            string _id = _line.Id;

            Foldout _text = AddSection(content, _type, TextSection);
            AddField(_text, _property.FindPropertyRelative("character"), "Character", "The character who says the line");
            AddField(_text, _property.FindPropertyRelative("text"), "Text", SourceTooltip());
            AddField(_text, _property.FindPropertyRelative("translatorNote"), "Translator Note");
            AddField(_text, _property.FindPropertyRelative("writerNote"), "Writer Note");

            VisualElement _translations = new VisualElement();
            if (AddTranslations(_translations, () => (asset?.Find(_id) as LineNode)?.Text, TextKey(_property.FindPropertyRelative("text"), typeof(LineNode), "text")) > 0) AddSection(content, _type, TranslationsSection).Add(_translations);

            Foldout _timing = AddSection(content, _type, TimingSection);
            AddField(_timing, _property.FindPropertyRelative("delayBefore"), "Delay Before");
            AddField(_timing, _property.FindPropertyRelative("delayAfter"), "Delay After");
            PropertyField _overrideLifetime = AddField(_timing, _property.FindPropertyRelative("overrideLifetime"), "Override Lifetime", "Use this line's own lifetime instead of the presenter's");
            PropertyField _lifetime = AddField(_timing, _property.FindPropertyRelative("lifetime"), "Lifetime", "How long the line stays shown");
            PropertyField _overrideAdvance = AddField(_timing, _property.FindPropertyRelative("overrideAdvance"), "Override Advance", "Use this line's own advance mode instead of the presenter's");
            PropertyField _advance = AddField(_timing, _property.FindPropertyRelative("advance"), "Advance", "How the line moves on to the next node");
            EnableWhen(_overrideLifetime, _property.propertyPath + ".overrideLifetime", _lifetime);
            EnableWhen(_overrideAdvance, _property.propertyPath + ".overrideAdvance", _advance);

            Foldout _actions = AddSection(content, _type, ActionsSection);
            AddField(_actions, _property.FindPropertyRelative("before"), "Before");
            AddField(_actions, _property.FindPropertyRelative("onStart"), "On Start");
            AddField(_actions, _property.FindPropertyRelative("onEnd"), "On End");

            Foldout _events = AddSection(content, _type, EventsSection);
            AddField(_events, _property.FindPropertyRelative("events"), "Events");
        }

        private void BuildChoice(ChoiceNode _choice, SerializedProperty _property) {
            Type _type = typeof(ChoiceNode);

            Foldout _timer = AddSection(content, _type, TimerSection);
            PropertyField _useTimer = AddField(_timer, _property.FindPropertyRelative("useTimer"), "Use Timer", "The choice ends on its own after some seconds");
            PropertyField _seconds = AddField(_timer, _property.FindPropertyRelative("timerSeconds"), "Seconds", "Seconds the player has to choose");
            PropertyField _rule = AddField(_timer, _property.FindPropertyRelative("timeoutRule"), "Timeout Rule", "What happens when the timer ends");
            EnableWhen(_useTimer, _property.propertyPath + ".useTimer", _seconds, _rule);

            Foldout _options = AddSection(content, _type, OptionsSection);
            SerializedProperty _list = _property.FindPropertyRelative("options");
            for (int _i = 0; _i < _list.arraySize; _i++) {
                ChoiceOption _option = _i < _choice.Options.Count ? _choice.Options[_i] : null;
                SerializedProperty _item = _list.GetArrayElementAtIndex(_i);
                Foldout _block = AddItem(_options, _i, _list.arraySize, "option");
                string _optionId = _option?.Id;
                string _nodeId = _choice.Id;

                AddField(_block, _item.FindPropertyRelative("text"), "Text", SourceTooltip());
                if (_optionId != null) AddSubTranslations(_block, _type, TextTranslations, () => FindOption(_nodeId, _optionId)?.Text, TextKey(_item.FindPropertyRelative("text"), typeof(ChoiceOption), "text"));
                AddField(_block, _item.FindPropertyRelative("lockedReason"), "Locked Reason");
                if (_optionId != null) AddSubTranslations(_block, _type, LockedTranslations, () => FindOption(_nodeId, _optionId)?.LockedReason, TextKey(_item.FindPropertyRelative("lockedReason"), typeof(ChoiceOption), "lockedReason"));
                AddField(_block, _item.FindPropertyRelative("isDefault"), "Default", "Selected when the choice is shown");
                AddField(_block, _item.FindPropertyRelative("afterChosen"), "After Chosen", "What happens to the option once it is chosen");
                AddField(_block, _item.FindPropertyRelative("tags"), "Tags", "Markers the presenter can show next to the option");
                AddField(_block, _item.FindPropertyRelative("visible"), "Visible Conditions");
                AddField(_block, _item.FindPropertyRelative("available"), "Available Conditions");
                AddField(_block, _item.FindPropertyRelative("onChosen"), "On Chosen", "Run when the option is chosen");
                AddField(_block, _item.FindPropertyRelative("translatorNote"), "Translator Note");
            }
            _options.Add(new Button(AddItem) { text = "Add Option" });
            RefreshItemTitles();
        }

        private void BuildBranch(SerializedProperty _property) {
            SerializedProperty _list = _property.FindPropertyRelative("cases");
            for (int _i = 0; _i < _list.arraySize; _i++) {
                Foldout _block = AddItem(content, _i, _list.arraySize, "case");
                AddField(_block, _list.GetArrayElementAtIndex(_i).FindPropertyRelative("conditions"), "Conditions", "The case is taken when all these conditions pass");
            }
            content.Add(new Button(AddItem) { text = "Add Case" });
            AddDescription(content, "Otherwise is taken when no case passes.");
            RefreshItemTitles();
        }

        /// <summary> A node type without its own layout: every visible field but its outputs </summary>
        private void BuildPlain(SerializedProperty _property) {
            SerializedProperty _child = _property.Copy();
            SerializedProperty _end = _property.GetEndProperty();
            bool _enter = true;
            while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                _enter = false;
                if (_child.type == NodeOutputType) continue;
                content.Add(new PropertyField(_child.Copy()));
            }
        }

        /// <summary> A foldout for one option or case, with the buttons that go through <see cref="DialogueGraphOps"/> </summary>
        private Foldout AddItem(VisualElement _into, int _index, int _count, string _itemName) {
            string _key = ItemKey(_index);
            Foldout _foldout = new Foldout { value = _key != null && expanded.Contains(_key) };
            _foldout.AddToClassList(ItemClass);
            _foldout.RegisterValueChangedCallback(_event => {
                if (_event.target != _foldout || _key == null) return;
                if (_event.newValue) expanded.Add(_key);
                else expanded.Remove(_key);
            });

            VisualElement _buttons = new VisualElement();
            _buttons.AddToClassList(ItemButtonsClass);
            Button _up = new Button(() => MoveItem(_index, _index - 1)) { text = "Move Up", tooltip = "Move this " + _itemName + " up" };
            Button _down = new Button(() => MoveItem(_index, _index + 1)) { text = "Move Down", tooltip = "Move this " + _itemName + " down" };
            _up.SetEnabled(_index > 0);
            _down.SetEnabled(_index < _count - 1);
            _buttons.Add(_up);
            _buttons.Add(_down);
            _buttons.Add(new Button(() => RemoveItem(_index)) { text = "Remove", tooltip = "Remove this " + _itemName + " and its output" });
            _foldout.Add(_buttons);

            _into.Add(_foldout);
            itemFoldouts.Add((_foldout, _index));
            return _foldout;
        }

        private void RefreshItemTitles() {
            DialogueNode _node = asset != null ? asset.Find(NodeId) : null;
            for (int _i = 0; _i < itemFoldouts.Count; _i++) {
                int _index = itemFoldouts[_i].Index;
                string _title;
                switch (_node) {
                    case ChoiceNode _choice when _index < _choice.Options.Count:
                        _title = "Option " + (_index + 1) + Summary(_choice.Options[_index] != null ? DialogueNodeView.PreviewText(_choice.Options[_index].Text, true) : null);
                        break;
                    case BranchNode _branch when _index < _branch.Cases.Count:
                        int _conditions = _branch.Cases[_index]?.Conditions?.Conditions?.Length ?? 0;
                        _title = "Case " + (_index + 1) + " · " + _conditions + (_conditions == 1 ? " condition" : " conditions");
                        break;
                    default:
                        _title = "Item " + (_index + 1);
                        break;
                }
                itemFoldouts[_i].Foldout.text = _title;
            }
        }

        /// <summary> The id that survives reordering: the option id or the case output id </summary>
        private string ItemKey(int _index) {
            switch (asset.Find(NodeId)) {
                case ChoiceNode _choice when _index < _choice.Options.Count: return _choice.Options[_index]?.Id;
                case BranchNode _branch when _index < _branch.Cases.Count: return _branch.Cases[_index]?.Next.Id;
                default: return null;
            }
        }

        private ChoiceOption FindOption(string _nodeId, string _optionId) {
            if (!(asset?.Find(_nodeId) is ChoiceNode _choice)) return null;
            for (int _i = 0; _i < _choice.Options.Count; _i++) {
                if (_choice.Options[_i] != null && _choice.Options[_i].Id == _optionId) return _choice.Options[_i];
            }
            return null;
        }
        #endregion

        // ==================== SECTIONS ====================
        #region Sections
        /// <summary> A section of the node, open or closed as it was last time for this node type </summary>
        private Foldout AddSection(VisualElement _into, Type _nodeType, string _title) {
            Foldout _section = CreateFoldout(_nodeType, _title, SectionClass);
            _into.Add(_section);
            sections.Add(_section);
            sectionTitles.Add(_title);
            return _section;
        }

        private Foldout CreateFoldout(Type _nodeType, string _title, string _class) {
            string _key = SectionStateKey(_nodeType, _title);
            Foldout _foldout = new Foldout { text = _title, value = SessionState.GetBool(_key, OpenByDefault.Contains(_title)) };
            _foldout.AddToClassList(_class);
            _foldout.RegisterValueChangedCallback(_event => {
                // Foldouts and toggles inside send their own changes up
                if (_event.target == _foldout) SessionState.SetBool(_key, _event.newValue);
            });
            return _foldout;
        }

        private static PropertyField AddField(VisualElement _into, SerializedProperty _property, string _label, string _tooltip = null) {
            if (_property == null) return null;
            PropertyField _field = new PropertyField(_property, _label);
            // Lists and custom drawers do not always show the [Tooltip] of the field, so it is set on the whole row
            _field.tooltip = !string.IsNullOrEmpty(_tooltip) ? _tooltip : _property.tooltip;
            _into.Add(_field);
            return _field;
        }

        private static void AddDescription(VisualElement _into, string _text) {
            Label _label = new Label(_text);
            _label.AddToClassList(DescriptionClass);
            _into.Add(_label);
        }

        /// <summary> Enables the fields only while the toggle at the path is on </summary>
        private void EnableWhen(PropertyField _toggle, string _togglePath, params PropertyField[] _fields) {
            void Sync() {
                if (serializedObject == null) return;
                SerializedProperty _value = serializedObject.FindProperty(_togglePath);
                bool _enabled = !ReadOnly && _value != null && _value.boolValue;
                for (int _i = 0; _i < _fields.Length; _i++) _fields[_i]?.SetEnabled(_enabled);
            }
            _toggle?.RegisterValueChangeCallback(_ => Sync());
            Sync();
        }

        private static string SourceTooltip() {
            Locale _source = DialogueTables.SourceLocale();
            return _source != null ? "The text in the source locale, " + LocaleName(_source) + ". Written to the dialogue's table" : "The text in the source locale";
        }
        #endregion

        // ==================== TRANSLATIONS ====================
        #region Translations
        /// <summary> One text field per project locale but the source one, written to that locale's table. Returns how many were added </summary>
        private int AddTranslations(VisualElement _into, Func<LocalizedString> _target, string _key) {
            Locale _source = DialogueTables.SourceLocale();
            if (_source == null || string.IsNullOrEmpty(_key)) return 0;

            IReadOnlyList<Locale> _locales = LocalizationEditorSettings.GetLocales();
            int _added = 0;
            for (int _i = 0; _i < _locales.Count; _i++) {
                Locale _locale = _locales[_i];
                if (_locale == null || _locale.Identifier == _source.Identifier) continue;

                LocaleIdentifier _identifier = _locale.Identifier;
                TextField _field = new TextField(LocaleName(_locale)) { multiline = true, tooltip = "The text in " + LocaleName(_locale) + ". Written to its table" };
                _field.textEdition.placeholder = NotTranslated;
                _field.AddToClassList(TranslationClass);
                _field.SetValueWithoutNotify(ReadTranslation(_target(), _identifier));
                _field.RegisterValueChangedCallback(_event => WriteTranslation(_target(), _key, _identifier, _event.newValue));
                // The table may have been edited elsewhere, such as the Localization Tables window, since the field was filled
                _field.RegisterCallback<FocusInEvent>(_ => _field.SetValueWithoutNotify(ReadTranslation(_target(), _identifier)));

                _into.Add(_field);
                translations[TranslationKey(_key, _identifier.Code)] = new Translation(_field, _target, _identifier);
                _added++;
            }
            return _added;
        }

        /// <summary> The translations of an option text, in a foldout of their own </summary>
        private void AddSubTranslations(VisualElement _into, Type _nodeType, string _title, Func<LocalizedString> _target, string _key) {
            Foldout _foldout = CreateFoldout(_nodeType, _title, SubSectionClass);
            if (AddTranslations(_foldout, _target, _key) > 0) _into.Add(_foldout);
        }

        private void WriteTranslation(LocalizedString _target, string _key, LocaleIdentifier _locale, string _text) {
            if (ReadOnly || asset == null || _target == null) return;
            DialogueTables.SetText(asset, _target, _key, _locale, _text);
        }

        /// <summary> Shows the stored translations again, after an undo or redo changed the tables. A field being typed in keeps its text </summary>
        private void RefreshTranslations() {
            foreach (Translation _translation in translations.Values) {
                if (_translation.Field.focusController?.focusedElement == _translation.Field) continue;
                _translation.Field.SetValueWithoutNotify(ReadTranslation(_translation.Target(), _translation.Locale));
            }
        }

        private static string ReadTranslation(LocalizedString _target, LocaleIdentifier _locale) {
            if (_target == null || _target.IsEmpty) return "";
            return DialogueTables.GetText(_target.TableReference, _target.TableEntryReference, _locale) ?? "";
        }

        private static string TranslationKey(string _key, string _localeCode) => _key + "|" + _localeCode;

        /// <summary> The table key of a dialogue text, built like the source text drawer builds it: the owner's id plus the suffix of the field's [DialogueText] </summary>
        private static string TextKey(SerializedProperty _text, Type _ownerType, string _field) {
            return _text != null ? DialogueTables.KeyFor(_text, DialogueTables.KeySuffix(_ownerType, _field)) : null;
        }

        private static string LocaleName(Locale _locale) => string.IsNullOrEmpty(_locale.LocaleName) ? _locale.Identifier.Code : _locale.LocaleName;

        /// <summary> A translation field with the text it writes, looked up each time so an undo that replaced the node is followed </summary>
        private readonly struct Translation {
            internal readonly TextField Field;
            internal readonly Func<LocalizedString> Target;
            internal readonly LocaleIdentifier Locale;

            internal Translation(TextField _field, Func<LocalizedString> _target, LocaleIdentifier _locale) {
                Field = _field;
                Target = _target;
                Locale = _locale;
            }
        }
        #endregion

        // ==================== ITEMS ====================
        #region Items
        /// <summary> Adds an option to the choice or a case to the branch shown </summary>
        internal void AddItem() {
            if (ReadOnly) return;
            switch (asset != null ? asset.Find(NodeId) : null) {
                case ChoiceNode _choice:
                    ChoiceOption _option = DialogueGraphOps.AddOption(asset, _choice);
                    if (_option != null) expanded.Add(_option.Id);
                    break;
                case BranchNode _branch:
                    BranchCase _case = DialogueGraphOps.AddCase(asset, _branch);
                    if (_case != null) expanded.Add(_case.Next.Id);
                    break;
                default:
                    return;
            }
            AfterItemsEdited();
        }

        internal void RemoveItem(int _index) {
            if (ReadOnly) return;
            switch (asset != null ? asset.Find(NodeId) : null) {
                case ChoiceNode _choice when _index >= 0 && _index < _choice.Options.Count && _choice.Options[_index] != null:
                    DialogueGraphOps.RemoveOption(asset, _choice, _choice.Options[_index].Id);
                    break;
                case BranchNode _branch:
                    DialogueGraphOps.RemoveCase(asset, _branch, _index);
                    break;
                default:
                    return;
            }
            AfterItemsEdited();
        }

        internal void MoveItem(int _from, int _to) {
            if (ReadOnly) return;
            switch (asset != null ? asset.Find(NodeId) : null) {
                case ChoiceNode _choice:
                    DialogueGraphOps.MoveOption(asset, _choice, _from, _to);
                    break;
                case BranchNode _branch:
                    DialogueGraphOps.MoveCase(asset, _branch, _from, _to);
                    break;
                default:
                    return;
            }
            AfterItemsEdited();
        }

        private void AfterItemsEdited() {
            string _nodeId = NodeId;
            OutputsEdited?.Invoke(_nodeId);
            // The window usually shows the node again while rebuilding the canvas; this covers a panel used on its own
            Show(asset, _nodeId);
        }
        #endregion

        // ==================== CHANGES ====================
        #region Changes
        private void OnChanged() {
            if (asset == null) return;
            RefreshItemTitles();
            if (NodeId != null) NodeEdited?.Invoke(NodeId);
        }

        private void OnSourceTextWritten(DialogueAsset _asset) {
            if (_asset != asset || content == null) return;
            RefreshItemTitles();
            // The text drawers read the table when they draw
            content.Query<IMGUIContainer>().ForEach(_container => _container.MarkDirtyLayout());
        }
        #endregion

        // ==================== LABELS ====================
        #region Labels
        /// <summary> The node type without the "Node" suffix </summary>
        private static string TypeName(DialogueNode _node) {
            string _name = _node.GetType().Name;
            return _name.EndsWith(NodeSuffix) && _name.Length > NodeSuffix.Length ? _name.Substring(0, _name.Length - NodeSuffix.Length) : _name;
        }

        /// <summary> " · " and the text, shortened. Empty when there is no text </summary>
        private static string Summary(string _text) {
            if (string.IsNullOrEmpty(_text)) return "";
            return " · " + (_text.Length <= MaxTitleLength ? _text : _text.Substring(0, MaxTitleLength).TrimEnd() + "…");
        }
        #endregion
    }
}
