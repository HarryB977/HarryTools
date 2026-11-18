using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Finds the nodes of a dialogue by their text in the source locale, character, writer note or type </summary>
    internal static class DialogueNodeFinder {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> The nodes that contain the query, ignoring case, in the order of the asset. Empty for an empty query </summary>
        internal static List<DialogueNode> Find(DialogueAsset _asset, string _query) {
            List<DialogueNode> _found = new List<DialogueNode>();
            if (_asset == null || string.IsNullOrWhiteSpace(_query)) return _found;

            string _trimmed = _query.Trim();
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                DialogueNode _node = _asset.Nodes[_i];
                if (_node != null && Matches(_node, _trimmed)) _found.Add(_node);
            }
            return _found;
        }

        /// <summary> The node type as shown to the user, without the "Node" suffix </summary>
        internal static string TypeLabel(DialogueNode _node) {
            string _name = _node.GetType().Name;
            return _name.EndsWith("Node", StringComparison.Ordinal) && _name.Length > 4 ? _name.Substring(0, _name.Length - 4) : _name;
        }

        /// <summary> One line that tells the node apart in a list of results </summary>
        internal static string Describe(DialogueNode _node) {
            string _detail = null;
            switch (_node) {
                case LineNode _line:
                    string _text = DialogueTables.GetSourceText(_line.Text);
                    if (string.IsNullOrEmpty(_text) && !string.IsNullOrEmpty(_line.WriterNote)) _text = "(" + _line.WriterNote + ")";
                    string _speaker = CharacterName(_line.Character);
                    _detail = string.IsNullOrEmpty(_speaker) ? _text : _speaker + ": " + _text;
                    break;
                case ChoiceNode _choice:
                    List<string> _options = new List<string>();
                    for (int _o = 0; _o < _choice.Options.Count; _o++) {
                        string _option = _choice.Options[_o] != null ? DialogueTables.GetSourceText(_choice.Options[_o].Text) : null;
                        if (!string.IsNullOrEmpty(_option)) _options.Add(_option);
                    }
                    _detail = string.Join(" / ", _options);
                    break;
            }
            string _label = TypeLabel(_node);
            return string.IsNullOrEmpty(_detail) ? _label : _label + " · " + _detail.Replace('\n', ' ');
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static bool Matches(DialogueNode _node, string _query) {
            if (Contains(TypeLabel(_node), _query)) return true;

            switch (_node) {
                case LineNode _line:
                    return Contains(DialogueTables.GetSourceText(_line.Text), _query) || Contains(_line.WriterNote, _query) || Contains(CharacterName(_line.Character), _query)
                        || (_line.Character != null && Contains(_line.Character.name, _query));
                case ChoiceNode _choice:
                    for (int _o = 0; _o < _choice.Options.Count; _o++) {
                        if (_choice.Options[_o] != null && Contains(DialogueTables.GetSourceText(_choice.Options[_o].Text), _query)) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary> The character's display name in the source locale, or its asset name when it has none </summary>
        private static string CharacterName(DialogueCharacter _character) {
            if (_character == null) return null;
            string _display = DialogueTables.GetSourceText(_character.DisplayName);
            return string.IsNullOrEmpty(_display) ? _character.name : _display;
        }

        private static bool Contains(string _text, string _query) {
            return !string.IsNullOrEmpty(_text) && _text.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion
    }

    /// <summary> Search field of the graph window's bar with the list of nodes found. Choosing a result raises <see cref="Picked"/> </summary>
    internal sealed class DialogueNodeFinderField : VisualElement {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const int MaxResults = 100;
        private const float ResultsWidth = 380f;
        private const float ItemHeight = 20f;
        private const long SearchDelayMilliseconds = 150;

        private readonly ToolbarSearchField field;
        private readonly ListView list;
        private readonly Label empty;
        private readonly List<DialogueNode> results = new List<DialogueNode>();
        private IVisualElementScheduledItem pendingSearch;
        #endregion

        #region Public Variables
        /// <summary> Where the nodes are searched </summary>
        internal Func<DialogueAsset> AssetSource { get; set; }

        /// <summary> Raised with the id of the node chosen in the results </summary>
        internal event Action<string> Picked;

        /// <summary> The box with the results. The window places it above the canvas </summary>
        internal VisualElement Popup { get; }

        internal IReadOnlyList<DialogueNode> Results => results;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueNodeFinderField() {
            field = new ToolbarSearchField { tooltip = "Find nodes by text, character, writer note or type (Ctrl+F)" };
            field.style.width = 220f;
            field.RegisterValueChangedCallback(_event => ScheduleSearch());
            field.RegisterCallback<KeyDownEvent>(OnFieldKeyDown, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusInEvent>(_ => {
                if (!string.IsNullOrWhiteSpace(field.value)) ShowResults();
            });
            Add(field);

            list = new ListView(results, ItemHeight, () => new Label(), (_element, _index) => {
                ((Label)_element).text = _index < results.Count && results[_index] != null ? DialogueNodeFinder.Describe(results[_index]) : "";
            }) { selectionType = SelectionType.Single };
            list.AddToClassList("dialogue-finder__list");
            list.selectionChanged += _ => {
                if (list.selectedIndex >= 0) PickResult(list.selectedIndex, false);
            };
            list.itemsChosen += _ => HideResults();
            list.RegisterCallback<KeyDownEvent>(_event => {
                if (_event.keyCode != KeyCode.Escape) return;
                HideResults();
                field.Focus();
                _event.StopPropagation();
            });

            empty = new Label("No nodes found");
            empty.AddToClassList("dialogue-finder__empty");

            Popup = new VisualElement();
            StyleSheet _style = AssetDatabase.LoadAssetAtPath<StyleSheet>(DialogueGraphView.StyleSheetPath);
            if (_style != null) Popup.styleSheets.Add(_style);
            Popup.AddToClassList("dialogue-finder");
            Popup.style.position = Position.Absolute;
            Popup.style.width = ResultsWidth;
            Popup.Add(list);
            Popup.Add(empty);
            Popup.style.display = DisplayStyle.None;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Searches at once and shows the results </summary>
        internal void Search(string _query) {
            field.SetValueWithoutNotify(_query ?? "");
            RunSearch();
        }

        /// <summary> Selects and frames the result at the index, then closes the results </summary>
        internal void Pick(int _index) {
            PickResult(_index, true);
        }

        internal void FocusField() {
            field.Focus();
            field.Q<TextField>()?.SelectAll();
            if (!string.IsNullOrWhiteSpace(field.value)) ShowResults();
        }

        /// <summary> Forgets the query and its results, for example when another dialogue is shown </summary>
        internal void ClearQuery() {
            field.SetValueWithoutNotify("");
            results.Clear();
            list.RefreshItems();
            HideResults();
        }

        internal void HideResults() {
            Popup.style.display = DisplayStyle.None;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void ScheduleSearch() {
            // Searching reads the text tables, so it waits until typing pauses
            pendingSearch?.Pause();
            pendingSearch = schedule.Execute(RunSearch);
            pendingSearch.ExecuteLater(SearchDelayMilliseconds);
        }

        private void RunSearch() {
            pendingSearch?.Pause();
            results.Clear();
            List<DialogueNode> _found = DialogueNodeFinder.Find(AssetSource?.Invoke(), field.value);
            for (int _i = 0; _i < _found.Count && _i < MaxResults; _i++) results.Add(_found[_i]);
            list.ClearSelection();
            list.RefreshItems();

            if (string.IsNullOrWhiteSpace(field.value)) HideResults();
            else ShowResults();
        }

        private void ShowResults() {
            list.style.display = results.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.height = Mathf.Min(results.Count, 15) * ItemHeight;
            empty.style.display = results.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;

            // Under the field, kept inside the window
            VisualElement _parent = Popup.parent;
            if (_parent != null && !float.IsNaN(worldBound.x) && !float.IsNaN(_parent.worldBound.width)) {
                Popup.style.top = worldBound.yMax - _parent.worldBound.y;
                Popup.style.left = Mathf.Max(0f, Mathf.Min(worldBound.x - _parent.worldBound.x, _parent.worldBound.width - ResultsWidth));
            }
            Popup.style.display = DisplayStyle.Flex;
        }

        private void PickResult(int _index, bool _close) {
            if (_index < 0 || _index >= results.Count || results[_index] == null) return;
            Picked?.Invoke(results[_index].Id);
            if (_close) HideResults();
        }

        private void OnFieldKeyDown(KeyDownEvent _event) {
            switch (_event.keyCode) {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (pendingSearch != null && pendingSearch.isActive) RunSearch();
                    Pick(0);
                    _event.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    if (results.Count == 0) return;
                    ShowResults();
                    list.Focus();
                    list.SetSelection(0);
                    _event.StopPropagation();
                    break;
                case KeyCode.Escape:
                    ClearQuery();
                    _event.StopPropagation();
                    break;
            }
        }
        #endregion
    }
}
