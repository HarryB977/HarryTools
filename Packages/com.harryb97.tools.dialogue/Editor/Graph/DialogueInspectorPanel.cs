using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Side panel of the graph window: the properties of the selected node, or the dialogue options when no node is selected. Fields are drawn with the property drawers of the asset, so changes record undo through the serialized object </summary>
    internal sealed class DialogueInspectorPanel : VisualElement {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string PanelClass = "dialogue-panel";
        private const string HeaderClass = "dialogue-panel__header";
        private const string ItemClass = "dialogue-panel__item";
        private const string ItemButtonsClass = "dialogue-panel__item-buttons";
        private const string NodesPath = "nodes.Array.data[";
        private const string NodeOutputType = "NodeOutput";
        private const string OptionsField = "options";
        private const string CasesField = "cases";
        private const string NodeSuffix = "Node";
        private const int MaxTitleLength = 32;

        private readonly Label header;
        private readonly ScrollView scroll;
        // Option and case foldouts the user opened, by option id or case output id
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly List<(Foldout Foldout, int Index)> itemFoldouts = new List<(Foldout, int)>();

        private DialogueAsset asset;
        private string signature;
        private SerializedObject serializedObject;
        private VisualElement content;
        #endregion

        #region Public Variables
        /// <summary> The node shown. Null when the panel shows the dialogue options </summary>
        internal string NodeId { get; private set; }

        /// <summary> Number of options of the choice or cases of the branch shown. Zero for other nodes </summary>
        internal int ItemCount => itemFoldouts.Count;

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
            header = new Label();
            header.AddToClassList(HeaderClass);
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            Add(header);
            Add(scroll);

            RegisterCallback<AttachToPanelEvent>(_ => {
                DialogueTables.SourceTextWritten += OnSourceTextWritten;
                // Detaching dropped the fields; a panel moved to another place shows its content again
                if (asset != null && content == null) Show(asset, NodeId);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => {
                DialogueTables.SourceTextWritten -= OnSourceTextWritten;
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
            if (_asset == asset && _signature == signature && content != null) return;

            asset = _asset;
            NodeId = _node?.Id;
            signature = _signature;
            Build(_node, _index);
        }

        private void Build(DialogueNode _node, int _index) {
            content?.RemoveFromHierarchy();
            serializedObject?.Dispose();
            serializedObject = null;
            itemFoldouts.Clear();

            content = new VisualElement();
            scroll.Add(content);

            if (asset == null) {
                header.text = "No dialogue";
                return;
            }

            serializedObject = new SerializedObject(asset);
            if (_node == null) BuildDialogueOptions();
            else BuildNode(_node, serializedObject.FindProperty(NodesPath + _index + "]"));

            content.Bind(serializedObject);
            content.TrackSerializedObjectValue(serializedObject, _ => OnChanged());
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
        #endregion

        // ==================== DIALOGUE OPTIONS ====================
        #region Dialogue Options
        private void BuildDialogueOptions() {
            header.text = asset.name;

            PropertyField _overrideTime = AddField("overrideTime");
            PropertyField _time = AddField("time");
            PropertyField _overrideAmbient = AddField("overrideAmbientPolicy");
            PropertyField _ambient = AddField("ambientPolicy");
            AddField("allowSkip");

            // The values only apply while their override is on
            void Sync() {
                if (serializedObject == null) return;
                _time.SetEnabled(serializedObject.FindProperty("overrideTime").boolValue);
                _ambient.SetEnabled(serializedObject.FindProperty("overrideAmbientPolicy").boolValue);
            }
            _overrideTime.RegisterValueChangeCallback(_ => Sync());
            _overrideAmbient.RegisterValueChangeCallback(_ => Sync());
            Sync();
        }

        private PropertyField AddField(string _path) {
            PropertyField _field = new PropertyField(serializedObject.FindProperty(_path));
            content.Add(_field);
            return _field;
        }
        #endregion

        // ==================== NODE ====================
        #region Node
        private void BuildNode(DialogueNode _node, SerializedProperty _property) {
            header.text = TypeName(_node);

            SerializedProperty _child = _property.Copy();
            SerializedProperty _end = _property.GetEndProperty();
            bool _enter = true;
            while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                _enter = false;
                // Connections are made on the canvas
                if (_child.type == NodeOutputType) continue;
                if (_node is ChoiceNode && _child.name == OptionsField) BuildItems(_child.Copy(), "Option", "Add Option");
                else if (_node is BranchNode && _child.name == CasesField) BuildItems(_child.Copy(), "Case", "Add Case");
                else content.Add(new PropertyField(_child.Copy()));
            }
        }

        /// <summary> The options of a choice or the cases of a branch: one foldout each, with buttons that go through <see cref="DialogueGraphOps"/> </summary>
        private void BuildItems(SerializedProperty _list, string _itemName, string _addLabel) {
            Label _title = new Label(_list.displayName);
            _title.AddToClassList(HeaderClass);
            content.Add(_title);

            for (int _i = 0; _i < _list.arraySize; _i++) {
                int _index = _i;
                SerializedProperty _item = _list.GetArrayElementAtIndex(_i);
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
                Button _up = new Button(() => MoveItem(_index, _index - 1)) { text = "Move Up", tooltip = "Move this " + _itemName.ToLowerInvariant() + " up" };
                Button _down = new Button(() => MoveItem(_index, _index + 1)) { text = "Move Down", tooltip = "Move this " + _itemName.ToLowerInvariant() + " down" };
                _up.SetEnabled(_index > 0);
                _down.SetEnabled(_index < _list.arraySize - 1);
                _buttons.Add(_up);
                _buttons.Add(_down);
                _buttons.Add(new Button(() => RemoveItem(_index)) { text = "Remove", tooltip = "Remove this " + _itemName.ToLowerInvariant() + " and its output" });
                _foldout.Add(_buttons);

                SerializedProperty _child = _item.Copy();
                SerializedProperty _end = _item.GetEndProperty();
                bool _enter = true;
                while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                    _enter = false;
                    if (_child.type == NodeOutputType) continue;
                    _foldout.Add(new PropertyField(_child.Copy()));
                }

                content.Add(_foldout);
                itemFoldouts.Add((_foldout, _index));
            }

            Button _add = new Button(AddItem) { text = _addLabel };
            content.Add(_add);
            RefreshItemTitles();
        }

        private void RefreshItemTitles() {
            DialogueNode _node = asset != null ? asset.Find(NodeId) : null;
            for (int _i = 0; _i < itemFoldouts.Count; _i++) {
                int _index = itemFoldouts[_i].Index;
                string _title;
                switch (_node) {
                    case ChoiceNode _choice when _index < _choice.Options.Count:
                        _title = "Option " + (_index + 1) + Summary(_choice.Options[_index] != null ? DialogueTables.GetSourceText(_choice.Options[_index].Text) : null);
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
        #endregion

        // ==================== ITEMS ====================
        #region Items
        /// <summary> Adds an option to the choice or a case to the branch shown </summary>
        internal void AddItem() {
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

        /// <summary> " · " and the first line of the text, shortened. Empty when there is no text </summary>
        private static string Summary(string _text) {
            if (string.IsNullOrEmpty(_text)) return "";
            int _break = _text.IndexOf('\n');
            if (_break >= 0) _text = _text.Substring(0, _break);
            return " · " + (_text.Length <= MaxTitleLength ? _text : _text.Substring(0, MaxTitleLength) + "…");
        }
        #endregion
    }
}
