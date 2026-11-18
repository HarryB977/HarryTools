using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Collapsible list of the validator's findings, shown at the bottom of the graph window. Validates the shown dialogue shortly after each change and, on request, every dialogue of the project </summary>
    internal sealed class DialogueIssueList : VisualElement {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const long ValidationDelayMilliseconds = 300;
        private const float ListHeight = 130f;
        private const string ListClass = "dialogue-issues";
        private const string RowClass = "dialogue-issues__row";
        private const string WarningRowClass = "dialogue-issues__row--warning";
        private const string SummaryRowClass = "dialogue-issues__row--summary";
        // The dialogues launched as ambient, kept for the editor session as asset GUIDs after the marker
        internal const string AmbientLaunchesKey = "HarryTools.Dialogue.AmbientLaunches";
        private const string StoredMarker = "#";
        private const char GuidSeparator = ';';

        private readonly List<DialogueIssue> issues = new List<DialogueIssue>();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Button toggle;
        private readonly ListView list;

        // Found on the first validation of the editor session and on "Revalidate all"; scanning prefabs on every change, reload or Play would be slow
        private HashSet<DialogueAsset> ambientLaunches;
        private IVisualElementScheduledItem pending;
        private bool expanded = true;
        #endregion

        #region Public Variables
        /// <summary> The dialogue to validate </summary>
        internal Func<DialogueAsset> AssetSource { get; set; }

        /// <summary> What the list shows: the findings of the shown dialogue, or the total of each dialogue after "Revalidate all" </summary>
        internal IReadOnlyList<Entry> Entries => entries;

        /// <summary> The findings of the shown dialogue </summary>
        internal IReadOnlyList<DialogueIssue> Issues => issues;

        /// <summary> Whether a validation is waiting for its delay </summary>
        internal bool HasPending => pending != null;

        /// <summary> Raised after the findings of the shown dialogue changed </summary>
        internal event Action IssuesChanged;

        /// <summary> A finding of a node was clicked: the id of the node </summary>
        internal event Action<string> NodeChosen;

        /// <summary> A dialogue row of the project summary was clicked </summary>
        internal event Action<DialogueAsset> DialogueChosen;

        /// <summary> One row of the list. A row with an asset is a dialogue of the project summary </summary>
        internal readonly struct Entry {
            public readonly string Text;
            public readonly string NodeId;
            public readonly DialogueAsset Asset;
            public readonly bool IsWarning;

            public Entry(string _text, string _nodeId, DialogueAsset _asset, bool _isWarning) {
                Text = _text;
                NodeId = _nodeId;
                Asset = _asset;
                IsWarning = _isWarning;
            }
        }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueIssueList() {
            AddToClassList(ListClass);

            VisualElement _header = new VisualElement();
            _header.style.flexDirection = FlexDirection.Row;
            toggle = new Button(() => SetExpanded(!expanded)) { tooltip = "Show or hide the list of issues" };
            toggle.style.flexGrow = 1f;
            toggle.style.unityTextAlign = TextAnchor.MiddleLeft;
            Button _all = new Button(RevalidateAll) { text = "Revalidate all", tooltip = "Check every dialogue of the project and look for dialogues launched as ambient again" };
            _header.Add(toggle);
            _header.Add(_all);
            Add(_header);

            list = new ListView {
                itemsSource = entries,
                fixedItemHeight = 18f,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            list.style.height = ListHeight;
            Add(list);

            RefreshHeader();
        }

        private VisualElement MakeRow() {
            Label _row = new Label();
            _row.AddToClassList(RowClass);
            _row.RegisterCallback<PointerDownEvent>(_event => {
                if (_event.button == 0 && _row.userData is int _index) Activate(_index);
            });
            return _row;
        }

        private void BindRow(VisualElement _element, int _index) {
            Label _row = (Label) _element;
            Entry _entry = entries[_index];
            _row.userData = _index;
            _row.text = _entry.Text;
            _row.tooltip = _entry.Text;
            _row.EnableInClassList(WarningRowClass, _entry.IsWarning);
            _row.EnableInClassList(SummaryRowClass, _entry.Asset != null);
        }
        #endregion

        // ==================== VALIDATION ====================
        #region Validation
        /// <summary> Validates after a short delay. A new request restarts the wait, so typing causes one run </summary>
        internal void ScheduleValidation() {
            pending?.Pause();
            pending = schedule.Execute(ValidateNow);
            pending.ExecuteLater(ValidationDelayMilliseconds);
        }

        /// <summary> Validates the shown dialogue at once, dropping a pending request </summary>
        internal void ValidateNow() {
            pending?.Pause();
            pending = null;

            issues.Clear();
            DialogueAsset _asset = AssetSource?.Invoke();
            if (_asset != null) DialogueValidator.Validate(_asset, AmbientLaunches(), issues);
            ShowCurrent();
            IssuesChanged?.Invoke();
        }

        /// <summary> Looks for ambient launchers again, validates every dialogue of the project and shows the total of each </summary>
        internal void RevalidateAll() {
            pending?.Pause();
            pending = null;
            ambientLaunches = ScanAmbientLaunches();

            // The shown dialogue gets its own findings again, with the fresh launchers
            issues.Clear();
            DialogueAsset _shown = AssetSource?.Invoke();
            if (_shown != null) DialogueValidator.Validate(_shown, ambientLaunches, issues);

            entries.Clear();
            List<DialogueIssue> _buffer = new List<DialogueIssue>();
            string[] _guids = AssetDatabase.FindAssets("t:DialogueAsset");
            for (int _i = 0; _i < _guids.Length; _i++) {
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                if (_asset == null) continue;
                _buffer.Clear();
                DialogueValidator.Validate(_asset, ambientLaunches, _buffer);
                entries.Add(new Entry(_asset.name + ": " + Summary(_buffer), "", _asset, CountWarnings(_buffer) > 0));
            }
            if (entries.Count == 0) entries.Add(new Entry("The project has no dialogues.", "", null, false));
            list.Rebuild();
            SetExpanded(true);
            RefreshHeader();
            IssuesChanged?.Invoke();
        }

        private HashSet<DialogueAsset> AmbientLaunches() {
            if (ambientLaunches == null) ambientLaunches = LoadAmbientLaunches() ?? ScanAmbientLaunches();
            return ambientLaunches;
        }

        private static HashSet<DialogueAsset> ScanAmbientLaunches() {
            HashSet<DialogueAsset> _found = DialogueValidator.FindAmbientLaunches();
            StoreAmbientLaunches(_found);
            return _found;
        }

        /// <summary> Keeps the dialogues launched as ambient for the rest of the editor session </summary>
        internal static void StoreAmbientLaunches(HashSet<DialogueAsset> _launches) {
            List<string> _guids = new List<string>();
            foreach (DialogueAsset _asset in _launches) {
                if (_asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_asset, out string _guid, out long _)) _guids.Add(_guid);
            }
            SessionState.SetString(AmbientLaunchesKey, StoredMarker + string.Join(GuidSeparator.ToString(), _guids));
        }

        /// <summary> The dialogues launched as ambient kept earlier in the editor session. Null when nothing was kept </summary>
        internal static HashSet<DialogueAsset> LoadAmbientLaunches() {
            string _stored = SessionState.GetString(AmbientLaunchesKey, "");
            if (!_stored.StartsWith(StoredMarker, StringComparison.Ordinal)) return null;

            HashSet<DialogueAsset> _launches = new HashSet<DialogueAsset>();
            string[] _guids = _stored.Substring(StoredMarker.Length).Split(new[] { GuidSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (int _i = 0; _i < _guids.Length; _i++) {
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                if (_asset != null) _launches.Add(_asset);
            }
            return _launches;
        }

        private void ShowCurrent() {
            DialogueAsset _asset = AssetSource?.Invoke();
            List<Entry> _next = new List<Entry>(issues.Count);
            for (int _i = 0; _i < issues.Count; _i++) {
                DialogueIssue _issue = issues[_i];
                _next.Add(new Entry(Describe(_asset, _issue), _issue.NodeId, null, _issue.Severity == DialogueIssueSeverity.Warning));
            }

            RefreshHeader();
            if (SameEntries(_next)) return;
            entries.Clear();
            entries.AddRange(_next);
            list.Rebuild();
        }

        // Rebuilding the list resets its scroll, so it is left alone when nothing changed
        private bool SameEntries(List<Entry> _other) {
            if (_other.Count != entries.Count) return false;
            for (int _i = 0; _i < _other.Count; _i++) {
                if (_other[_i].Text != entries[_i].Text || _other[_i].NodeId != entries[_i].NodeId || _other[_i].Asset != entries[_i].Asset) return false;
            }
            return true;
        }

        private static string Describe(DialogueAsset _asset, DialogueIssue _issue) {
            string _where = "Dialogue";
            if (_asset != null && !string.IsNullOrEmpty(_issue.NodeId)) {
                for (int _i = 0; _i < _asset.NodeList.Count; _i++) {
                    DialogueNode _node = _asset.NodeList[_i];
                    if (_node != null && _node.Id == _issue.NodeId) {
                        _where = _node.GetType().Name;
                        break;
                    }
                }
            }
            return (_issue.Severity == DialogueIssueSeverity.Info ? "Note: " : "") + _where + ": " + _issue.Message;
        }
        #endregion

        // ==================== SUMMARY ====================
        #region Summary
        internal static int CountWarnings(IReadOnlyList<DialogueIssue> _issues) {
            int _count = 0;
            for (int _i = 0; _i < _issues.Count; _i++) {
                if (_issues[_i].Severity == DialogueIssueSeverity.Warning) _count++;
            }
            return _count;
        }

        /// <summary> Short text with the number of warnings and notes </summary>
        internal static string Summary(IReadOnlyList<DialogueIssue> _issues) {
            if (_issues.Count == 0) return "no issues";
            int _warnings = CountWarnings(_issues);
            int _notes = _issues.Count - _warnings;
            string _text = _warnings + (_warnings == 1 ? " warning" : " warnings");
            if (_notes > 0) _text += ", " + _notes + (_notes == 1 ? " note" : " notes");
            return _text;
        }

        private void RefreshHeader() {
            toggle.text = (expanded ? "v " : "> ") + "Issues (" + Summary(issues) + ")";
        }

        private void SetExpanded(bool _expanded) {
            expanded = _expanded;
            list.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshHeader();
        }
        #endregion

        // ==================== CLICK ====================
        #region Click
        /// <summary> Acts on the row: selects and frames the node of a finding, or opens the dialogue of a summary row </summary>
        internal void Activate(int _index) {
            if (_index < 0 || _index >= entries.Count) return;
            Entry _entry = entries[_index];
            if (_entry.Asset != null) DialogueChosen?.Invoke(_entry.Asset);
            else if (!string.IsNullOrEmpty(_entry.NodeId)) NodeChosen?.Invoke(_entry.NodeId);
        }
        #endregion
    }
}
