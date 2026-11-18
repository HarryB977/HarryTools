using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Copy and paste of nodes, groups and notes as text, so it works between dialogues </summary>
    internal static class DialogueClipboard {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string Header = "HarryToolsDialogue:";
        private const string PasteUndo = "Paste";
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The selection as text: the nodes (never the start node), the notes, and the groups that are selected or whose nodes are all copied. Empty when there is nothing to copy </summary>
        internal static string Copy(DialogueAsset _asset, IReadOnlyCollection<string> _ids) {
            if (_asset == null || _ids == null || _ids.Count == 0) return "";

            DialogueAsset _copy = ScriptableObject.CreateInstance<DialogueAsset>();
            try {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_asset), _copy);

                HashSet<string> _selected = new HashSet<string>(_ids);
                _copy.NodeList.RemoveAll(_node => _node == null || _node is StartNode || !_selected.Contains(_node.Id));
                HashSet<string> _copied = new HashSet<string>();
                for (int _i = 0; _i < _copy.NodeList.Count; _i++) _copied.Add(_copy.NodeList[_i].Id);

                _copy.NoteList.RemoveAll(_note => _note == null || !_selected.Contains(_note.Id));
                _copy.GroupList.RemoveAll(_group => {
                    if (_group == null) return true;
                    _group.NodeIdList.RemoveAll(_id => !_copied.Contains(_id));
                    bool _allCopied = _group.NodeIdList.Count > 0 && _group.NodeIdList.Count == GroupSize(_asset, _group.Id);
                    return !_selected.Contains(_group.Id) && !_allCopied;
                });

                if (!HasContent(_copy)) return "";
                return Header + EditorJsonUtility.ToJson(_copy);
            } finally {
                UnityEngine.Object.DestroyImmediate(_copy);
            }
        }

        /// <summary> True when the text holds something Paste can add </summary>
        internal static bool CanPaste(string _data) {
            DialogueAsset _parsed = Parse(_data);
            if (_parsed == null) return false;
            UnityEngine.Object.DestroyImmediate(_parsed);
            return true;
        }

        /// <summary> Adds the copied nodes, groups and notes to the asset, moved by the offset, and returns their new ids in that order. Empty when the text is not valid. The existing ids of the asset are never touched </summary>
        internal static IReadOnlyList<string> Paste(DialogueAsset _asset, string _data, Vector2 _offset) {
            if (_asset == null) return Array.Empty<string>();
            DialogueAsset _pasted = Parse(_data);
            if (_pasted == null) return Array.Empty<string>();

            try {
                // New ids on the copy, so links between the copied nodes follow and links to anything else are cleared
                _pasted.RegenerateIds();

                Undo.RecordObject(_asset, PasteUndo);
                List<string> _newIds = new List<string>();

                for (int _i = 0; _i < _pasted.NodeList.Count; _i++) {
                    DialogueNode _node = _pasted.NodeList[_i];
                    _node.Position += _offset;
                    _asset.NodeList.Add(_node);
                    _newIds.Add(_node.Id);
                }
                for (int _i = 0; _i < _pasted.GroupList.Count; _i++) {
                    _asset.GroupList.Add(_pasted.GroupList[_i]);
                    _newIds.Add(_pasted.GroupList[_i].Id);
                }
                for (int _i = 0; _i < _pasted.NoteList.Count; _i++) {
                    DialogueNote _note = _pasted.NoteList[_i];
                    Rect _rect = _note.Rect;
                    _rect.position += _offset;
                    _note.Rect = _rect;
                    _asset.NoteList.Add(_note);
                    _newIds.Add(_note.Id);
                }

                EditorUtility.SetDirty(_asset);
                DialogueTables.AdoptTexts(_asset);
                return _newIds;
            } finally {
                UnityEngine.Object.DestroyImmediate(_pasted);
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        /// <summary> The copied content as a temporary asset without start nodes or null entries. Null when the text is not clipboard data or holds nothing. The caller destroys it </summary>
        private static DialogueAsset Parse(string _data) {
            if (string.IsNullOrEmpty(_data) || !_data.StartsWith(Header, StringComparison.Ordinal)) return null;

            DialogueAsset _parsed = ScriptableObject.CreateInstance<DialogueAsset>();
            try {
                EditorJsonUtility.FromJsonOverwrite(_data.Substring(Header.Length), _parsed);
                _parsed.NodeList.RemoveAll(_node => _node == null || _node is StartNode);
                _parsed.GroupList.RemoveAll(_group => _group == null);
                _parsed.NoteList.RemoveAll(_note => _note == null);
                if (HasContent(_parsed)) return _parsed;
            } catch (Exception) {
                // Anything that is not valid clipboard data is simply not pasteable
            }
            UnityEngine.Object.DestroyImmediate(_parsed);
            return null;
        }

        private static bool HasContent(DialogueAsset _asset) {
            return _asset.NodeList.Count > 0 || _asset.GroupList.Count > 0 || _asset.NoteList.Count > 0;
        }

        private static int GroupSize(DialogueAsset _asset, string _groupId) {
            for (int _i = 0; _i < _asset.Groups.Count; _i++) {
                if (_asset.Groups[_i] != null && _asset.Groups[_i].Id == _groupId) return _asset.Groups[_i].NodeIds.Count;
            }
            return 0;
        }
        #endregion
    }
}
