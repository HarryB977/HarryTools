using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Audio.Editor {
    /// <summary> Shared field drawing and item lookup of the asset and ref drawers. </summary>
    internal static class AudioPickerField {
        // ==================== VARIABLES ===================
        private const float PlayWidth = 24f;
        private static readonly Dictionary<Type, List<AudioCatalogItem>> cache = new Dictionary<Type, List<AudioCatalogItem>>();
        private static readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();
        private static GUIStyle missingStyle;

        // ==================== ITEMS ===================
        /// <summary> Assets assignable to the type: the settings catalog, or an asset search when the project has no catalog. Cached until the catalog or the project changes. </summary>
        internal static IReadOnlyList<AudioCatalogItem> GetItems(Type _type) {
            if (cache.TryGetValue(_type, out List<AudioCatalogItem> _cached)) return _cached;
            var _list = new List<AudioCatalogItem>();
            AudioProjectSettings _settings = AudioSettingsProvider.FindSettings();
            AudioCatalog _catalog = _settings != null ? _settings.Catalog : null;
            if (_catalog != null) {
                for (int _i = 0; _i < _catalog.Items.Count; _i++) {
                    if (_catalog.Items[_i] != null && _type.IsInstanceOfType(_catalog.Items[_i])) _list.Add(_catalog.Items[_i]);
                }
            } else {
                string[] _guids = AssetDatabase.FindAssets("t:" + _type.Name);
                for (int _i = 0; _i < _guids.Length; _i++) {
                    var _item = AssetDatabase.LoadAssetAtPath<AudioCatalogItem>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                    if (_item != null && _type.IsInstanceOfType(_item)) _list.Add(_item);
                }
            }
            cache[_type] = _list;
            return _list;
        }

        internal static AudioCatalogItem Find(Type _type, string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            IReadOnlyList<AudioCatalogItem> _items = GetItems(_type);
            for (int _i = 0; _i < _items.Count; _i++) {
                if (_items[_i] != null && _items[_i].Id == _id) return _items[_i];
            }
            return null;
        }

        /// <summary> Field type, or the element type when the field is an array or a list. </summary>
        internal static Type ElementType(Type _fieldType) {
            if (_fieldType == null) return null;
            if (_fieldType.IsArray) return _fieldType.GetElementType();
            if (_fieldType.IsGenericType && _fieldType.GetGenericTypeDefinition() == typeof(List<>)) return _fieldType.GetGenericArguments()[0];
            return _fieldType;
        }

        // ==================== DRAWING ===================
        /// <summary> Draws label, picker button and preview button. _refId is the id child of a ref struct, or null when the property is an object reference. </summary>
        internal static void Draw(Rect _position, SerializedProperty _property, GUIContent _label, Type _assetType, SerializedProperty _refId) {
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
            else {
                string _group = AudioCodeGenerator.GetGroupPath(_asset, AudioCodeGenerator.GetOptions().Root);
                _text = string.IsNullOrEmpty(_group) ? _asset.name : $"{_asset.name}   ({_group})";
            }

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

        // ==================== PRIVATE ===================
        private static void Open(Rect _rect, SerializedProperty _property, Type _assetType, bool _isRef) {
            UnityEngine.Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;
            var _dropdown = new AudioPickerDropdown(dropdownState, _assetType, GetItems(_assetType), _selected => Assign(_targets, _path, _isRef, _selected));
            _dropdown.Show(_rect);
        }

        // The dropdown outlives the drawer call, so it edits through a fresh SerializedObject (keeps Undo and multi-object editing).
        private static void Assign(UnityEngine.Object[] _targets, string _path, bool _isRef, AudioCatalogItem _selected) {
            if (_selected != null && _isRef) AudioIdAssigner.EnsureId(_selected);
            var _object = new SerializedObject(_targets);
            SerializedProperty _property = _object.FindProperty(_path);
            if (_property == null) return;
            if (_isRef) _property.FindPropertyRelative("id").stringValue = _selected != null ? _selected.Id : string.Empty;
            else _property.objectReferenceValue = _selected;
            _object.ApplyModifiedProperties();
            _object.Dispose();
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
                AudioIdAssigner.EnsureId(_dropped);
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
