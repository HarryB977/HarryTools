using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Writes the context comment of every entry of a dialogue's string table collection and keeps the entries in script order </summary>
    internal static class DialogueTableLayout {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string UndoName = "Refresh Dialogue Table Layout";
        private const string StartOrigin = "After: start";
        private const string OtherwiseOrigin = "Otherwise";
        private const string OriginSeparator = "; ";

        private enum EntryKind {
            Line,
            Option,
            LockedReason
        }

        private sealed class Entry {
            public EntryKind Kind;
            public string Key;
            public string Note;
            public string Speaker;
            public DialogueNode Node;
        }

        private sealed class Plan {
            public readonly List<Entry> Entries = new List<Entry>();
            public readonly Dictionary<DialogueNode, List<string>> Origins = new Dictionary<DialogueNode, List<string>>();
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The comment of the entry under the key: its type, the dialogue, the speaker, where it comes from and the translator note. Null when the dialogue has no such entry </summary>
        internal static string BuildComment(DialogueAsset _asset, string _key) {
            if (_asset == null || string.IsNullOrEmpty(_key)) return null;
            Plan _plan = Analyze(_asset);
            for (int _i = 0; _i < _plan.Entries.Count; _i++) {
                if (_plan.Entries[_i].Key == _key) return Compose(_asset, _plan.Entries[_i], _plan);
            }
            return null;
        }

        /// <summary> Every text key of the dialogue in script order: breadth first from Start following the outputs in order, a choice followed by its options and then their locked reasons, and what Start does not reach last in node order </summary>
        internal static IReadOnlyList<string> ScriptOrder(DialogueAsset _asset) {
            List<string> _keys = new List<string>();
            if (_asset == null) return _keys;

            Plan _plan = Analyze(_asset);
            for (int _i = 0; _i < _plan.Entries.Count; _i++) _keys.Add(_plan.Entries[_i].Key);
            return _keys;
        }

        /// <summary> Writes the comment of every entry of the dialogue's collection and puts the entries in script order, with undo. Only marks the collection dirty when something changed. Does nothing when the dialogue has no collection. True when the shared table data was changed </summary>
        internal static bool Refresh(DialogueAsset _asset) {
            StringTableCollection _collection = _asset != null ? DialogueTables.Linked(_asset) : null;
            SharedTableData _shared = _collection != null ? _collection.SharedData : null;
            if (_shared == null) return false;

            Plan _plan = Analyze(_asset);
            bool _recorded = false;

            for (int _i = 0; _i < _plan.Entries.Count; _i++) {
                SharedTableData.SharedTableEntry _entry = _shared.GetEntry(_plan.Entries[_i].Key);
                if (_entry == null) continue;

                string _comment = Compose(_asset, _plan.Entries[_i], _plan);
                Comment _existing = _entry.Metadata.GetMetadata<Comment>();
                if (_existing != null && _existing.CommentText == _comment) continue;

                Record(_shared, ref _recorded);
                if (_existing != null) _existing.CommentText = _comment;
                else _entry.Metadata.AddMetadata(new Comment { CommentText = _comment });
            }

            List<SharedTableData.SharedTableEntry> _ordered = Ordered(_shared, _plan);
            if (!SameOrder(_shared.Entries, _ordered)) {
                Record(_shared, ref _recorded);
                _shared.Entries = _ordered;
            }

            if (_recorded) EditorUtility.SetDirty(_shared);
            return _recorded;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Record(SharedTableData _shared, ref bool _recorded) {
            if (_recorded) return;
            Undo.RecordObject(_shared, UndoName);
            _recorded = true;
        }

        /// <summary> The entries of the shared data with the dialogue's keys first in script order, then any other entry in the order it already had </summary>
        private static List<SharedTableData.SharedTableEntry> Ordered(SharedTableData _shared, Plan _plan) {
            List<SharedTableData.SharedTableEntry> _ordered = new List<SharedTableData.SharedTableEntry>(_shared.Entries.Count);
            HashSet<SharedTableData.SharedTableEntry> _placed = new HashSet<SharedTableData.SharedTableEntry>();

            for (int _i = 0; _i < _plan.Entries.Count; _i++) {
                SharedTableData.SharedTableEntry _entry = _shared.GetEntry(_plan.Entries[_i].Key);
                if (_entry != null && _placed.Add(_entry)) _ordered.Add(_entry);
            }
            for (int _i = 0; _i < _shared.Entries.Count; _i++) {
                if (_placed.Add(_shared.Entries[_i])) _ordered.Add(_shared.Entries[_i]);
            }
            return _ordered;
        }

        private static bool SameOrder(List<SharedTableData.SharedTableEntry> _a, List<SharedTableData.SharedTableEntry> _b) {
            if (_a.Count != _b.Count) return false;
            for (int _i = 0; _i < _a.Count; _i++) {
                if (_a[_i] != _b[_i]) return false;
            }
            return true;
        }

        /// <summary> Lists the entries in script order and works out where each node comes from </summary>
        private static Plan Analyze(DialogueAsset _asset) {
            Plan _plan = new Plan();
            Dictionary<string, DialogueNode> _byId = new Dictionary<string, DialogueNode>();
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                DialogueNode _node = _asset.Nodes[_i];
                if (_node != null && !string.IsNullOrEmpty(_node.Id)) _byId[_node.Id] = _node;
            }

            List<DialogueNode> _reached = new List<DialogueNode>();
            HashSet<DialogueNode> _seen = new HashSet<DialogueNode>();
            StartNode _start = _asset.Start;
            if (_start != null) {
                _reached.Add(_start);
                _seen.Add(_start);
            }

            List<NodeOutput> _outputs = new List<NodeOutput>();
            for (int _i = 0; _i < _reached.Count; _i++) {
                _outputs.Clear();
                DialogueValidator.CollectUsedOutputs(_reached[_i], _outputs);
                for (int _o = 0; _o < _outputs.Count; _o++) {
                    NodeOutput _output = _outputs[_o];
                    if (_output == null || !_output.IsConnected || !_byId.TryGetValue(_output.Target, out DialogueNode _next)) continue;
                    if (_seen.Add(_next)) _reached.Add(_next);
                }
            }

            for (int _i = 0; _i < _reached.Count; _i++) AddEntries(_plan, _reached[_i]);
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] != null && !_seen.Contains(_asset.Nodes[_i])) AddEntries(_plan, _asset.Nodes[_i]);
            }

            FindOrigins(_plan, _start, _byId);
            return _plan;
        }

        private static void AddEntries(Plan _plan, DialogueNode _node) {
            switch (_node) {
                case LineNode _line:
                    if (string.IsNullOrEmpty(_line.Id)) return;
                    _plan.Entries.Add(new Entry {
                        Kind = EntryKind.Line,
                        Key = _line.Id + DialogueTables.KeySuffix(typeof(LineNode), "text"),
                        Note = _line.TranslatorNote,
                        Speaker = SpeakerName(_line.Character),
                        Node = _line
                    });
                    break;
                case ChoiceNode _choice:
                    for (int _o = 0; _o < _choice.Options.Count; _o++) {
                        ChoiceOption _option = _choice.Options[_o];
                        if (_option == null || string.IsNullOrEmpty(_option.Id)) continue;
                        _plan.Entries.Add(new Entry {
                            Kind = EntryKind.Option,
                            Key = _option.Id + DialogueTables.KeySuffix(typeof(ChoiceOption), "text"),
                            Note = _option.TranslatorNote,
                            Node = _choice
                        });
                    }
                    for (int _o = 0; _o < _choice.Options.Count; _o++) {
                        ChoiceOption _option = _choice.Options[_o];
                        if (_option == null || string.IsNullOrEmpty(_option.Id)) continue;
                        _plan.Entries.Add(new Entry {
                            Kind = EntryKind.LockedReason,
                            Key = _option.Id + DialogueTables.KeySuffix(typeof(ChoiceOption), "lockedReason"),
                            Node = _choice
                        });
                    }
                    break;
            }
        }

        /// <summary> Gives each node reached from Start the list of places it can be entered from. A node whose output has no origin of its own (a line, an actions node, a jump, the no answer output of a choice) passes on the origins it was entered with </summary>
        private static void FindOrigins(Plan _plan, StartNode _start, Dictionary<string, DialogueNode> _byId) {
            if (_start == null) return;

            Queue<DialogueNode> _pending = new Queue<DialogueNode>();
            _pending.Enqueue(_start);
            List<NodeOutput> _outputs = new List<NodeOutput>();
            List<string> _carried = new List<string>();

            while (_pending.Count > 0) {
                DialogueNode _node = _pending.Dequeue();
                _plan.Origins.TryGetValue(_node, out List<string> _own);
                _outputs.Clear();
                DialogueValidator.CollectUsedOutputs(_node, _outputs);

                for (int _o = 0; _o < _outputs.Count; _o++) {
                    NodeOutput _output = _outputs[_o];
                    if (_output == null || !_output.IsConnected || !_byId.TryGetValue(_output.Target, out DialogueNode _next)) continue;

                    _carried.Clear();
                    string _label = OriginOf(_node, _output);
                    if (_label != null) _carried.Add(_label);
                    else if (_own != null) _carried.AddRange(_own);

                    if (!_plan.Origins.TryGetValue(_next, out List<string> _known)) {
                        _known = new List<string>();
                        _plan.Origins[_next] = _known;
                    }
                    bool _grew = false;
                    for (int _c = 0; _c < _carried.Count; _c++) {
                        if (_known.Contains(_carried[_c])) continue;
                        _known.Add(_carried[_c]);
                        _grew = true;
                    }
                    if (_grew) _pending.Enqueue(_next);
                }
            }
        }

        /// <summary> What leaving the node through the output means to the node it leads to. Null when it adds nothing of its own </summary>
        private static string OriginOf(DialogueNode _node, NodeOutput _output) {
            switch (_node) {
                case StartNode _:
                    return StartOrigin;
                case ChoiceNode _choice:
                    for (int _i = 0; _i < _choice.Options.Count; _i++) {
                        if (_choice.Options[_i] != null && _choice.Options[_i].Next == _output) return "After option \"" + OneLine(DialogueTables.GetSourceText(_choice.Options[_i].Text)) + "\"";
                    }
                    return null;
                case BranchNode _branch:
                    if (_branch.Otherwise == _output) return OtherwiseOrigin;
                    for (int _i = 0; _i < _branch.Cases.Count; _i++) {
                        if (_branch.Cases[_i] != null && _branch.Cases[_i].Next == _output) return "Branch case " + (_i + 1);
                    }
                    return null;
            }
            return null;
        }

        private static string Compose(DialogueAsset _asset, Entry _entry, Plan _plan) {
            StringBuilder _text = new StringBuilder();
            switch (_entry.Kind) {
                case EntryKind.Line:
                    _text.Append("Line");
                    break;
                case EntryKind.Option:
                    _text.Append("Option");
                    break;
                default:
                    _text.Append("Locked reason");
                    break;
            }

            _text.Append("\nDialogue: ").Append(_asset.name);
            if (!string.IsNullOrEmpty(_entry.Speaker)) _text.Append("\nSpeaker: ").Append(_entry.Speaker);
            if (_plan.Origins.TryGetValue(_entry.Node, out List<string> _origins) && _origins.Count > 0) {
                _text.Append('\n').Append(string.Join(OriginSeparator, _origins));
            }
            if (!string.IsNullOrWhiteSpace(_entry.Note)) _text.Append("\nNote: ").Append(_entry.Note.Trim());
            return _text.ToString();
        }

        /// <summary> The name of the character in the source locale, or the asset name when it has none </summary>
        private static string SpeakerName(DialogueCharacter _character) {
            if (_character == null) return null;
            string _name = DialogueTables.GetSourceText(_character.DisplayName);
            return string.IsNullOrWhiteSpace(_name) ? _character.name : OneLine(_name);
        }

        private static string OneLine(string _text) {
            return string.IsNullOrEmpty(_text) ? "" : _text.Replace("\r", "").Replace('\n', ' ');
        }
        #endregion
    }
}
