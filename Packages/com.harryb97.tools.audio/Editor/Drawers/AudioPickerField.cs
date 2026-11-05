using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Audio.Editor {
    /// <summary> Shared field drawing and item lookup of the asset and ref drawers. </summary>
    internal static class AudioPickerField {
        // ==================== TYPES ===================
        private sealed class ItemCache {
            public readonly List<AudioCatalogItem> Items = new List<AudioCatalogItem>();
            public readonly Dictionary<string, AudioCatalogItem> ById = new Dictionary<string, AudioCatalogItem>(StringComparer.Ordinal);
            public readonly Dictionary<AudioCatalogItem, string> Text = new Dictionary<AudioCatalogItem, string>();
        }

        // ==================== VARIABLES ===================
        private const float PlayWidth = 24f;
        private static readonly Dictionary<Type, ItemCache> cache = new Dictionary<Type, ItemCache>();
        private static readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();
        private static GUIStyle missingStyle;

        // ==================== ITEMS ===================
        /// <summary> Assets assignable to the type: the settings catalog, or an asset search when the project has no catalog. Cached until the catalog or the project changes. </summary>
        internal static IReadOnlyList<AudioCatalogItem> GetItems(Type _type) => GetCache(_type).Items;

        internal static AudioCatalogItem Find(Type _type, string _id) {
            if (_type == null || string.IsNullOrEmpty(_id)) return null;
            return GetCache(_type).ById.TryGetValue(_id, out AudioCatalogItem _item) ? _item : null;
        }

        /// <summary> Field type, or the element type when the field is an array or a list. </summary>
        internal static Type ElementType(Type _fieldType) {
            if (_fieldType == null) return null;
            if (_fieldType.IsArray) return _fieldType.GetElementType();
            if (_fieldType.IsGenericType && _fieldType.GetGenericTypeDefinition() == typeof(List<>)) return _fieldType.GetGenericArguments()[0];
            return _fieldType;
        }

        /// <summary> Asset type a ref struct points at, from the serialized type name ("SoundRef"); null when it is not a ref. </summary>
        internal static Type AssetTypeOfRef(string _propertyType) {
            switch (_propertyType) {
                case "SoundRef": return typeof(SoundAsset);
                case "MusicRef": return typeof(MusicAsset);
                case "VoiceRef": return typeof(VoiceAsset);
                case "ConversationRef": return typeof(ConversationAsset);
                default: return null;
            }
        }

        /// <summary> Asset type of an object reference field: the field type when known, else the serialized type name ("PPtr&lt;$SoundAsset&gt;"), else AudioCatalogItem. </summary>
        internal static Type AssetTypeOfObject(Type _fieldType, string _propertyType) {
            Type _element = ElementType(_fieldType);
            if (_element != null && typeof(AudioCatalogItem).IsAssignableFrom(_element)) return _element;
            if (!string.IsNullOrEmpty(_propertyType) && _propertyType.StartsWith("PPtr<$", StringComparison.Ordinal) && _propertyType.EndsWith(">", StringComparison.Ordinal)) {
                string _name = _propertyType.Substring(6, _propertyType.Length - 7);
                foreach (Type _type in TypeCache.GetTypesDerivedFrom<AudioCatalogItem>()) {
                    if (_type.Name == _name) return _type;
                }
            }
            return typeof(AudioCatalogItem);
        }

        // ==================== DRAWING ===================
        /// <summary> Draws label, picker button and preview button. _refId is the id child of a ref struct, or null when the property is an object reference. </summary>
        internal static void Draw(Rect _position, SerializedProperty _property, GUIContent _label, Type _assetType, SerializedProperty _refId) {
            if (_assetType == null) _assetType = typeof(AudioCatalogItem);
            bool _isRef = _refId != null;
            _position = EditorGUI.PrefixLabel(_position, GUIUtility.GetControlID(FocusType.Passive), _label);
            var _playRect = new Rect(_position.xMax - PlayWidth, _position.y, PlayWidth, _position.height);
            var _fieldRect = new Rect(_position.x, _position.y, _position.width - PlayWidth - 2f, _position.height);

            bool _mixed = _isRef ? _refId.hasMultipleDifferentValues : _property.hasMultipleDifferentValues;
            string _id = _isRef ? _refId.stringValue : null;
            AudioCatalogItem _asset = _isRef ? Find(_assetType, _id) : _property.objectReferenceValue as AudioCatalogItem;
            bool _missing = _isRef && !_mixed && !string.IsNullOrEmpty(_id) && _asset == null;

            string _text;
            if (_mixed) _text = "—";
            else if (_missing) _text = $"Missing ({_id})";
            else if (_asset == null) _text = "None";
            else _text = DisplayText(_assetType, _asset);

            Event _event = Event.current;
            if (_event.type == EventType.DragUpdated || _event.type == EventType.DragPerform) HandleDrop(_fieldRect, _property, _assetType, _refId, _event);

            GUIStyle _style = EditorStyles.popup;
            if (_missing) {
                if (missingStyle == null) {
                    missingStyle = new GUIStyle(EditorStyles.popup);
                    missingStyle.normal.textColor = new Color(0.9f, 0.25f, 0.25f);
                    missingStyle.hover.textColor = missingStyle.normal.textColor;
                }
                _style = missingStyle;
            }
            if (GUI.Button(_fieldRect, _text, _style)) Open(_fieldRect, _property, _assetType, _isRef);

            bool _playable = !_mixed && _asset is AudioAsset;
            using (new EditorGUI.DisabledScope(!_playable)) {
                bool _active = _playable && AudioPreviewer.Current == _asset;
                if (GUI.Button(_playRect, _active ? "■" : "▶", EditorStyles.miniButton)) {
                    if (_active) AudioPreviewer.Stop();
                    else AudioPreviewer.Play((AudioAsset)_asset);
                }
            }
        }

        /// <summary> Wraps a drawer's OnGUI in an IMGUIContainer for UI Toolkit inspectors. Draws nothing once the target is destroyed or the object disposed. </summary>
        internal static VisualElement CreateGUI(SerializedProperty _property, Action<Rect, SerializedProperty, GUIContent> _draw) {
            SerializedProperty _copy = _property.Copy();
            var _label = new GUIContent(_property.displayName, _property.tooltip);
            return new IMGUIContainer(() => {
                try {
                    if (_copy.serializedObject.targetObject == null) return;
                    _copy.serializedObject.Update();
                    Rect _rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                    _draw(_rect, _copy, _label);
                    _copy.serializedObject.ApplyModifiedProperties();
                } catch (ObjectDisposedException) {
                } catch (ArgumentNullException) {
                } catch (NullReferenceException) {
                }
            });
        }

        // ==================== PRIVATE ===================
        private static ItemCache GetCache(Type _type) {
            if (cache.TryGetValue(_type, out ItemCache _cached)) return _cached;
            var _entry = new ItemCache();
            AudioProjectSettings _settings = AudioSettingsProvider.FindSettings();
            AudioCatalog _catalog = _settings != null ? _settings.Catalog : null;
            if (_catalog != null) {
                for (int _i = 0; _i < _catalog.Items.Count; _i++) {
                    if (_catalog.Items[_i] != null && _type.IsInstanceOfType(_catalog.Items[_i])) _entry.Items.Add(_catalog.Items[_i]);
                }
            } else {
                string[] _guids = AssetDatabase.FindAssets("t:" + _type.Name);
                for (int _i = 0; _i < _guids.Length; _i++) {
                    var _item = AssetDatabase.LoadAssetAtPath<AudioCatalogItem>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                    if (_item != null && _type.IsInstanceOfType(_item)) _entry.Items.Add(_item);
                }
            }
            for (int _i = 0; _i < _entry.Items.Count; _i++) {
                if (!string.IsNullOrEmpty(_entry.Items[_i].Id)) _entry.ById[_entry.Items[_i].Id] = _entry.Items[_i];
            }
            cache[_type] = _entry;
            return _entry;
        }

        private static string DisplayText(Type _type, AudioCatalogItem _asset) {
            ItemCache _entry = GetCache(_type);
            if (_entry.Text.TryGetValue(_asset, out string _text)) return _text;
            string _group = AudioCodeGenerator.GetGroupPath(_asset, AudioCodeGenerator.GetOptions().Root);
            _text = string.IsNullOrEmpty(_group) ? _asset.name : $"{_asset.name}   ({_group})";
            _entry.Text[_asset] = _text;
            return _text;
        }

        private static void Open(Rect _rect, SerializedProperty _property, Type _assetType, bool _isRef) {
            UnityEngine.Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;
            var _dropdown = new AudioPickerDropdown(dropdownState, _assetType, GetItems(_assetType), _selected => Assign(_targets, _path, _isRef, _selected));
            _dropdown.Show(_rect);
        }

        // The dropdown outlives the drawer call, so it edits through a fresh SerializedObject (keeps Undo and multi-object editing).
        // In Play mode the item is not saved with a new id: the reference takes the id it already has.
        private static void Assign(UnityEngine.Object[] _targets, string _path, bool _isRef, AudioCatalogItem _selected) {
            if (_selected != null && _isRef && !EditorApplication.isPlayingOrWillChangePlaymode) AudioIdAssigner.EnsureId(_selected);
            using (var _object = new SerializedObject(_targets)) {
                SerializedProperty _property = _object.FindProperty(_path);
                if (_property == null) return;
                if (_isRef) _property.FindPropertyRelative("id").stringValue = _selected != null ? _selected.Id : string.Empty;
                else _property.objectReferenceValue = _selected;
                _object.ApplyModifiedProperties();
            }
        }

        private static void HandleDrop(Rect _rect, SerializedProperty _property, Type _assetType, SerializedProperty _refId, Event _event) {
            if (!_rect.Contains(_event.mousePosition)) return;
            AudioCatalogItem _dropped = null;
            foreach (UnityEngine.Object _object in DragAndDrop.objectReferences) {
                if (_object is AudioCatalogItem _item && _assetType.IsInstanceOfType(_item)) { _dropped = _item; break; }
            }
            if (_dropped == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (_event.type != EventType.DragPerform) return;
            DragAndDrop.AcceptDrag();
            if (_refId != null) {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) AudioIdAssigner.EnsureId(_dropped);
                _refId.stringValue = _dropped.Id;
            } else _property.objectReferenceValue = _dropped;
            GUI.changed = true;
            _event.Use();
        }

        [InitializeOnLoadMethod]
        private static void Register() {
            AudioCatalogMaintainer.Rebuilt += cache.Clear;
            EditorApplication.projectChanged += cache.Clear;
        }
    }
}
