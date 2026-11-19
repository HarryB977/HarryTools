using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Keeps the names of the characters in one string table collection of their own, under the stable id of each character, and writes their context comments and order </summary>
    internal static class DialogueCharacterTables {
        // ==================== VARIABLES ===================
        #region Private Variables
        /// <summary> Name of the characters collection </summary>
        internal const string CollectionName = "DialogueCharacters";

        private const string UndoName = "Edit Character Name";
        private const string MoveUndoName = "Move Character Name";
        private const string RefreshUndoName = "Refresh Character Table Layout";
        private const string UsedInPrefix = "Used in: ";
        private const string UsedInSeparator = ", ";

        private const string TestCollectionName = "DialogueCharactersTest";
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The name of the characters collection in use: <see cref="CollectionName"/>, or a test-only name while test settings are injected, so tests never reach the project's own collection </summary>
        private static string ActiveName => DialogueSettings.IsTestInjected ? TestCollectionName : CollectionName;

        /// <summary> The characters collection. Null when it does not exist yet </summary>
        internal static StringTableCollection Find() {
            return LocalizationEditorSettings.GetStringTableCollection(ActiveName);
        }

        /// <summary> The characters collection, created in the settings tables folder with one table per project locale when missing. Null when the project has no locales </summary>
        internal static StringTableCollection EnsureCollection() {
            StringTableCollection _collection = Find();
            if (_collection == null) {
                if (LocalizationEditorSettings.GetLocales().Count == 0) return null;
                _collection = LocalizationEditorSettings.CreateStringTableCollection(ActiveName, DialogueSettings.Current.TablesFolder);
                if (_collection == null) return null;
            }
            DialogueTables.EnsureLocaleTables(_collection);
            return _collection;
        }

        /// <summary> The id of the character, assigned from its asset GUID when it has none yet. Empty for an instance that is not an asset </summary>
        internal static string EnsureId(DialogueCharacter _character) {
            if (_character == null) return "";
            if (string.IsNullOrEmpty(_character.CharacterId)) {
                string _path = AssetDatabase.GetAssetPath(_character);
                if (!string.IsNullOrEmpty(_path)) DialogueAssetIdPostprocessor.Sync(_character, _path);
            }
            return _character.CharacterId ?? "";
        }

        /// <summary> True when the name of the character is the entry of the characters collection keyed by its own id </summary>
        internal static bool IsOwnCollection(DialogueCharacter _character) {
            if (_character == null || string.IsNullOrEmpty(_character.CharacterId) || _character.DisplayName == null || _character.DisplayName.IsEmpty) return false;

            StringTableCollection _own = Find();
            if (_own == null || LocalizationEditorSettings.GetStringTableCollection(_character.DisplayName.TableReference) != _own) return false;
            SharedTableData.SharedTableEntry _entry = _own.SharedData.GetEntryFromReference(_character.DisplayName.TableEntryReference);
            return _entry != null && _entry.Key == _character.CharacterId;
        }

        /// <summary> Writes the name in the source locale. Translations without a snapshot are stamped against the previous name first, so they show as outdated when the name changes. False when there is no source locale, the character is not an asset or its name lives in another table </summary>
        internal static bool SetSourceName(DialogueCharacter _character, string _name) {
            Locale _source = DialogueTables.SourceLocale();
            return _source != null && SetName(_character, _source.Identifier, _name);
        }

        /// <summary> Writes the name in the locale, keyed by the character id, and points the character to the entry. Translations are stamped with the source name they were written against. False when the character is not an asset or its name lives in another table </summary>
        internal static bool SetName(DialogueCharacter _character, LocaleIdentifier _locale, string _name) {
            string _id = EnsureId(_character);
            if (string.IsNullOrEmpty(_id) || string.IsNullOrEmpty(_locale.Code)) return false;
            if (!IsOwnCollection(_character) && !_character.DisplayName.IsEmpty) return false;

            StringTableCollection _collection = EnsureCollection();
            if (_collection == null) return false;
            StringTable _table = _collection.GetTable(_locale) as StringTable ?? _collection.AddNewTable(_locale) as StringTable;
            if (_table == null) return false;

            Locale _source = DialogueTables.SourceLocale();
            bool _isSource = _source != null && _source.Identifier == _locale;
            Undo.IncrementCurrentGroup();
            int _group = Undo.GetCurrentGroup();
            if (_isSource) DialogueTranslations.StampUnstamped(_collection, _id);

            SharedTableData _shared = _collection.SharedData;
            Undo.RecordObjects(new Object[] { _shared, _table }, UndoName);
            SharedTableData.SharedTableEntry _entry = _shared.GetEntry(_id) ?? _shared.AddKey(_id);
            _table.AddEntry(_entry.Id, _name ?? "");
            EditorUtility.SetDirty(_shared);
            EditorUtility.SetDirty(_table);
            if (!_isSource) DialogueTranslations.Stamp(_collection, _id, _locale);

            PointTo(_character, _shared.TableCollectionNameGuid, _entry.Id, UndoName);
            Undo.CollapseUndoOperations(_group);
            return true;
        }

        /// <summary> Copies the name of every locale from the entry the character points to into its own entry of the characters collection and points the character there. The original entry is left as it is. Nothing when the name is already there or the character has none </summary>
        internal static void MoveToOwnCollection(DialogueCharacter _character) {
            string _id = EnsureId(_character);
            if (string.IsNullOrEmpty(_id) || _character.DisplayName == null || _character.DisplayName.IsEmpty || IsOwnCollection(_character)) return;

            StringTableCollection _from = LocalizationEditorSettings.GetStringTableCollection(_character.DisplayName.TableReference);
            SharedTableData.SharedTableEntry _fromEntry = _from != null && _from.SharedData != null ? _from.SharedData.GetEntryFromReference(_character.DisplayName.TableEntryReference) : null;
            if (_fromEntry == null) return;
            StringTableCollection _own = EnsureCollection();
            if (_own == null) return;

            Undo.RecordObject(_own.SharedData, MoveUndoName);
            SharedTableData.SharedTableEntry _entry = _own.SharedData.GetEntry(_id) ?? _own.SharedData.AddKey(_id);
            EditorUtility.SetDirty(_own.SharedData);
            DialogueTables.CopyValues(_from, _fromEntry.Id, _own, _entry.Id, true);
            PointTo(_character, _own.SharedData.TableCollectionNameGuid, _entry.Id, MoveUndoName);
        }

        /// <summary> The context comment of the character entry: its type and the dialogues that use it as a speaker </summary>
        internal static string BuildComment(DialogueCharacter _character) {
            if (_character == null) return null;
            Users().TryGetValue(_character, out List<string> _dialogues);
            return Compose(_dialogues);
        }

        /// <summary> Writes the comment of the entry of every project character and puts the entries in name order, with undo. Only marks the collection dirty when something changed. True when the shared table data was changed </summary>
        internal static bool Refresh() {
            StringTableCollection _collection = Find();
            SharedTableData _shared = _collection != null ? _collection.SharedData : null;
            if (_shared == null) return false;

            Dictionary<DialogueCharacter, List<string>> _users = Users();
            List<(string Name, SharedTableData.SharedTableEntry Entry)> _owned = new List<(string, SharedTableData.SharedTableEntry)>();
            bool _recorded = false;

            foreach (string _guid in AssetDatabase.FindAssets("t:DialogueCharacter")) {
                DialogueCharacter _character = AssetDatabase.LoadAssetAtPath<DialogueCharacter>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_character == null || string.IsNullOrEmpty(_character.CharacterId)) continue;
                SharedTableData.SharedTableEntry _entry = _shared.GetEntry(_character.CharacterId);
                if (_entry == null) continue;

                _users.TryGetValue(_character, out List<string> _dialogues);
                string _comment = Compose(_dialogues);
                Comment _existing = _entry.Metadata.GetMetadata<Comment>();
                if (_existing == null || _existing.CommentText != _comment) {
                    Record(_shared, ref _recorded);
                    if (_existing != null) _existing.CommentText = _comment;
                    else _entry.Metadata.AddMetadata(new Comment { CommentText = _comment });
                }
                _owned.Add((NameOf(_character), _entry));
            }

            _owned.Sort((_a, _b) => string.Compare(_a.Name, _b.Name, StringComparison.OrdinalIgnoreCase));
            List<SharedTableData.SharedTableEntry> _ordered = new List<SharedTableData.SharedTableEntry>(_shared.Entries.Count);
            HashSet<SharedTableData.SharedTableEntry> _placed = new HashSet<SharedTableData.SharedTableEntry>();
            for (int _i = 0; _i < _owned.Count; _i++) {
                if (_placed.Add(_owned[_i].Entry)) _ordered.Add(_owned[_i].Entry);
            }
            for (int _i = 0; _i < _shared.Entries.Count; _i++) {
                if (_placed.Add(_shared.Entries[_i])) _ordered.Add(_shared.Entries[_i]);
            }

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
        private static void PointTo(DialogueCharacter _character, Guid _collectionGuid, long _keyId, string _undoName) {
            LocalizedString _name = _character.DisplayName;
            bool _pointed = _name.TableReference.ReferenceType == TableReference.Type.Guid && _name.TableReference.TableCollectionNameGuid == _collectionGuid
                && _name.TableEntryReference.ReferenceType == TableEntryReference.Type.Id && _name.TableEntryReference.KeyId == _keyId;
            if (_pointed) return;
            Undo.RecordObject(_character, _undoName);
            _name.TableReference = _collectionGuid;
            _name.TableEntryReference = _keyId;
            EditorUtility.SetDirty(_character);
        }

        private static void Record(SharedTableData _shared, ref bool _recorded) {
            if (_recorded) return;
            Undo.RecordObject(_shared, RefreshUndoName);
            _recorded = true;
        }

        private static bool SameOrder(List<SharedTableData.SharedTableEntry> _a, List<SharedTableData.SharedTableEntry> _b) {
            if (_a.Count != _b.Count) return false;
            for (int _i = 0; _i < _a.Count; _i++) {
                if (_a[_i] != _b[_i]) return false;
            }
            return true;
        }

        private static string Compose(List<string> _dialogues) {
            if (_dialogues == null || _dialogues.Count == 0) return "Character";
            return "Character\n" + UsedInPrefix + string.Join(UsedInSeparator, _dialogues);
        }

        /// <summary> The source locale name of the character, or the asset name when it has none </summary>
        private static string NameOf(DialogueCharacter _character) {
            string _name = DialogueTables.GetSourceText(_character.DisplayName);
            return string.IsNullOrWhiteSpace(_name) ? _character.name : _name.Replace("\r", "").Replace('\n', ' ');
        }

        /// <summary> For every character used as the speaker of a line in any dialogue of the project, the sorted names of those dialogues </summary>
        private static Dictionary<DialogueCharacter, List<string>> Users() {
            Dictionary<DialogueCharacter, List<string>> _users = new Dictionary<DialogueCharacter, List<string>>();
            foreach (string _guid in AssetDatabase.FindAssets("t:DialogueAsset")) {
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_asset == null) continue;
                for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                    DialogueCharacter _character = (_asset.Nodes[_i] as LineNode)?.Character;
                    if (_character == null) continue;
                    if (!_users.TryGetValue(_character, out List<string> _names)) {
                        _names = new List<string>();
                        _users[_character] = _names;
                    }
                    if (!_names.Contains(_asset.name)) _names.Add(_asset.name);
                }
            }
            foreach (List<string> _names in _users.Values) _names.Sort(StringComparer.OrdinalIgnoreCase);
            return _users;
        }
        #endregion
    }
}
