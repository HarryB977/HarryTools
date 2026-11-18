using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Keeps one string table collection per dialogue and writes its text to the source locale table under keys derived from node and option ids </summary>
    internal static class DialogueTables {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string CollectionPrefix = "Dialogue_";
        private const string GuidTag = "GUID:";
        private const string IdField = "id";
        private const string UndoName = "Edit Dialogue Text";
        #endregion

        #region Public Variables
        /// <summary> Raised after a text of the dialogue is written to its source locale table </summary>
        internal static event Action<DialogueAsset> SourceTextWritten;
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The locale the dialogue text is written in: the one set in the dialogue settings, otherwise the project locale. Null when it cannot be found </summary>
        internal static Locale SourceLocale() {
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) return null;

            string _code = DialogueSettings.Current.SourceLocaleCode;
            if (!string.IsNullOrEmpty(_code)) return LocalizationEditorSettings.GetLocale(_code);

            Locale _project = LocalizationSettings.ProjectLocale;
            if (_project != null) return _project;

            IReadOnlyList<Locale> _locales = LocalizationEditorSettings.GetLocales();
            return _locales.Count > 0 ? _locales[0] : null;
        }

        /// <summary> The collection linked to the dialogue, created in the settings tables folder with one table per project locale when missing. Null when the project has no locales </summary>
        internal static StringTableCollection EnsureCollection(DialogueAsset _asset) {
            if (_asset == null) return null;

            StringTableCollection _linked = Linked(_asset);
            if (_linked != null) return _linked;
            if (LocalizationEditorSettings.GetLocales().Count == 0) return null;

            StringTableCollection _created = LocalizationEditorSettings.CreateStringTableCollection(UniqueName(_asset.name), DialogueSettings.Current.TablesFolder);
            if (_created == null) return null;

            // The link is not recorded for undo: reverting an edit must not orphan the collection it created
            _asset.TableCollectionGuid = _created.SharedData.TableCollectionNameGuid.ToString("N");
            EditorUtility.SetDirty(_asset);
            return _created;
        }

        /// <summary> Writes the text to the source locale table under the key, then points the target to that entry. False when there is no source locale </summary>
        internal static bool SetSourceText(DialogueAsset _asset, LocalizedString _target, string _key, string _text) {
            if (_target == null || !WriteSourceText(_asset, _key, _text, out Guid _collection, out long _keyId)) return false;

            Undo.RecordObject(_asset, UndoName);
            _target.TableReference = _collection;
            _target.TableEntryReference = _keyId;
            EditorUtility.SetDirty(_asset);
            NotifySourceTextWritten(_asset);
            return true;
        }

        /// <summary> Writes the text to the table of the locale under the key, creating the table and the entry when missing, then points the target to that entry when it points nowhere. Only the source locale raises <see cref="SourceTextWritten"/>. False when the project has no locales </summary>
        internal static bool SetText(DialogueAsset _asset, LocalizedString _target, string _key, LocaleIdentifier _locale, string _text) {
            Locale _source = SourceLocale();
            if (_source != null && _source.Identifier == _locale) return SetSourceText(_asset, _target, _key, _text);
            if (_target == null || !WriteText(_asset, _key, _locale, _text, out Guid _collection, out long _keyId)) return false;

            // A translation written before the source text still needs the reference
            if (_target.IsEmpty) {
                Undo.RecordObject(_asset, UndoName);
                _target.TableReference = _collection;
                _target.TableEntryReference = _keyId;
                EditorUtility.SetDirty(_asset);
            }
            return true;
        }

        /// <summary> Raises <see cref="SourceTextWritten"/>. Called once the text and the reference to it are both in place </summary>
        internal static void NotifySourceTextWritten(DialogueAsset _asset) {
            SourceTextWritten?.Invoke(_asset);
        }

        /// <summary> The text of the target in the source locale table. Null when it has none </summary>
        internal static string GetSourceText(LocalizedString _target) {
            if (_target == null || _target.IsEmpty) return null;
            Locale _source = SourceLocale();
            return _source != null ? GetText(_target.TableReference, _target.TableEntryReference, _source.Identifier) : null;
        }
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        /// <summary> Adds the key when missing and writes the text to the source locale table, with undo. False when there is no source locale </summary>
        internal static bool WriteSourceText(DialogueAsset _asset, string _key, string _text, out Guid _collectionGuid, out long _keyId) {
            Locale _source = SourceLocale();
            if (_source != null) return WriteText(_asset, _key, _source.Identifier, _text, out _collectionGuid, out _keyId);
            _collectionGuid = Guid.Empty;
            _keyId = 0;
            return false;
        }

        /// <summary> Adds the key when missing and writes the text to the table of the locale, with undo. False when the dialogue has no collection and none can be made </summary>
        private static bool WriteText(DialogueAsset _asset, string _key, LocaleIdentifier _locale, string _text, out Guid _collectionGuid, out long _keyId) {
            _collectionGuid = Guid.Empty;
            _keyId = 0;
            if (_asset == null || string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_locale.Code)) return false;

            StringTableCollection _collection = EnsureCollection(_asset);
            if (_collection == null) return false;

            // The locale may have been added after the collection was created
            StringTable _table = _collection.GetTable(_locale) as StringTable;
            if (_table == null) _table = _collection.AddNewTable(_locale) as StringTable;
            if (_table == null) return false;

            SharedTableData _shared = _collection.SharedData;
            Undo.RecordObjects(new Object[] { _shared, _table }, UndoName);
            SharedTableData.SharedTableEntry _entry = _shared.GetEntry(_key) ?? _shared.AddKey(_key);
            _table.AddEntry(_entry.Id, _text ?? "");
            EditorUtility.SetDirty(_shared);
            EditorUtility.SetDirty(_table);

            _collectionGuid = _shared.TableCollectionNameGuid;
            _keyId = _entry.Id;
            return true;
        }

        /// <summary> Moves every dialogue text that points outside the dialogue's own collection into it, under the dialogue's own keys, copying the value of every locale. Used after duplicating an asset. Creates the collection only when there is text to move </summary>
        internal static void AdoptTexts(DialogueAsset _asset) {
            if (_asset == null) return;
            List<(LocalizedString Target, string Key)> _texts = Texts(_asset);
            StringTableCollection _own = Linked(_asset);
            bool _changed = false;

            for (int _i = 0; _i < _texts.Count; _i++) {
                LocalizedString _target = _texts[_i].Target;
                if (_target == null || _target.IsEmpty || string.IsNullOrEmpty(_texts[_i].Key)) continue;
                StringTableCollection _source = LocalizationEditorSettings.GetStringTableCollection(_target.TableReference);
                if (_source == null || _source.SharedData == null) continue;
                SharedTableData.SharedTableEntry _sourceEntry = _source.SharedData.GetEntryFromReference(_target.TableEntryReference);
                if (_sourceEntry == null) continue;
                if (_own != null && _source == _own && _sourceEntry.Key == _texts[_i].Key) continue;

                if (_own == null) _own = EnsureCollection(_asset);
                if (_own == null) return;

                SharedTableData.SharedTableEntry _entry = _own.SharedData.GetEntry(_texts[_i].Key) ?? _own.SharedData.AddKey(_texts[_i].Key);
                IReadOnlyList<StringTable> _tables = _source.StringTables;
                for (int _t = 0; _t < _tables.Count; _t++) {
                    string _value = _tables[_t] != null ? _tables[_t].GetEntry(_sourceEntry.Id)?.Value : null;
                    if (_value == null) continue;
                    StringTable _destination = _own.GetTable(_tables[_t].LocaleIdentifier) as StringTable;
                    if (_destination == null) _destination = _own.AddNewTable(_tables[_t].LocaleIdentifier) as StringTable;
                    if (_destination == null) continue;
                    _destination.AddEntry(_entry.Id, _value);
                    EditorUtility.SetDirty(_destination);
                }

                _target.TableReference = _own.SharedData.TableCollectionNameGuid;
                _target.TableEntryReference = _entry.Id;
                _changed = true;
            }

            if (!_changed) return;
            EditorUtility.SetDirty(_own.SharedData);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary> The text of an entry in the table of the given locale. Null when the collection, entry or table is missing </summary>
        internal static string GetText(TableReference _table, TableEntryReference _entry, LocaleIdentifier _locale) {
            if (_table.ReferenceType == TableReference.Type.Empty || _entry.ReferenceType == TableEntryReference.Type.Empty) return null;

            StringTableCollection _collection = LocalizationEditorSettings.GetStringTableCollection(_table);
            if (_collection == null || _collection.SharedData == null) return null;
            SharedTableData.SharedTableEntry _shared = _collection.SharedData.GetEntryFromReference(_entry);
            if (_shared == null) return null;

            StringTable _stringTable = _collection.GetTable(_locale) as StringTable;
            return _stringTable != null ? _stringTable.GetEntry(_shared.Id)?.Value : null;
        }

        /// <summary> Id of the node or option that holds the LocalizedString property, plus the suffix. Null when the owner has no id </summary>
        internal static string KeyFor(SerializedProperty _localizedString, string _suffix) {
            string _path = _localizedString.propertyPath;
            int _dot = _path.LastIndexOf('.');
            string _idPath = (_dot >= 0 ? _path.Substring(0, _dot + 1) : string.Empty) + IdField;
            SerializedProperty _id = _localizedString.serializedObject.FindProperty(_idPath);
            if (_id == null || _id.propertyType != SerializedPropertyType.String || string.IsNullOrEmpty(_id.stringValue)) return null;
            return _id.stringValue + (_suffix ?? "");
        }

        /// <summary> Reads the table and entry a serialized LocalizedString points to </summary>
        internal static void ReadReference(SerializedProperty _localizedString, out TableReference _table, out TableEntryReference _entry) {
            string _name = _localizedString.FindPropertyRelative("m_TableReference.m_TableCollectionName")?.stringValue;
            if (string.IsNullOrEmpty(_name)) _table = default;
            else if (_name.StartsWith(GuidTag, StringComparison.OrdinalIgnoreCase) && Guid.TryParse(_name.Substring(GuidTag.Length), out Guid _guid)) _table = _guid;
            else _table = _name;

            SerializedProperty _keyId = _localizedString.FindPropertyRelative("m_TableEntryReference.m_KeyId");
            string _key = _localizedString.FindPropertyRelative("m_TableEntryReference.m_Key")?.stringValue;
            if (_keyId != null && _keyId.longValue != 0) _entry = _keyId.longValue;
            else if (!string.IsNullOrEmpty(_key)) _entry = _key;
            else _entry = default;
        }

        /// <summary> Points a serialized LocalizedString to a collection by GUID and an entry by key id </summary>
        internal static void WriteReference(SerializedProperty _localizedString, Guid _collectionGuid, long _keyId) {
            _localizedString.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = GuidTag + _collectionGuid.ToString("N");
            _localizedString.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = _keyId;
            _localizedString.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static StringTableCollection Linked(DialogueAsset _asset) {
            if (string.IsNullOrEmpty(_asset.TableCollectionGuid) || !Guid.TryParse(_asset.TableCollectionGuid, out Guid _guid)) return null;
            return LocalizationEditorSettings.GetStringTableCollection(_guid);
        }

        /// <summary> Every dialogue text of the asset with the key it belongs under </summary>
        private static List<(LocalizedString, string)> Texts(DialogueAsset _asset) {
            List<(LocalizedString, string)> _texts = new List<(LocalizedString, string)>();
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                switch (_asset.Nodes[_i]) {
                    case LineNode _line:
                        _texts.Add((_line.Text, _line.Id));
                        break;
                    case ChoiceNode _choice:
                        for (int _o = 0; _o < _choice.Options.Count; _o++) {
                            ChoiceOption _option = _choice.Options[_o];
                            if (_option == null) continue;
                            _texts.Add((_option.Text, _option.Id));
                            _texts.Add((_option.LockedReason, _option.Id + ChoiceOption.LockedSuffix));
                        }
                        break;
                }
            }
            return _texts;
        }

        private static string UniqueName(string _assetName) {
            // Addressables reject names holding both brackets
            string _base = CollectionPrefix + _assetName.Replace('[', '(').Replace(']', ')').Trim();
            string _name = _base;
            for (int _i = 2; LocalizationEditorSettings.GetStringTableCollection(_name) != null; _i++) _name = _base + "_" + _i;
            return _name;
        }
        #endregion
    }
}
