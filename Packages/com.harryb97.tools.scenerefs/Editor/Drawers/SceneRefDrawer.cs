using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// One line drawer for <see cref="SceneRef{T}"/>. A live direct reference shows as a regular object field; a reference by guid shows
    /// the current id and scene from the index, in red when the guid is not in it. Drops anywhere on the line, ▾ opens the project wide
    /// picker and right click clears.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneRefBase), true)]
    internal sealed class SceneRefDrawer : PropertyDrawer
    {
        // ==================== VARIABLES ====================

        private const float ButtonWidth = 20f;
        private const float Spacing = 2f;
        private const string Separator = " · ";
        private const string LabelSeparator = " in ";
        private const string MissingText = "missing";
        private const double MissRebuildInterval = 1.0;

        private static readonly Color MissingColor = new Color(0.95f, 0.3f, 0.3f);
        private static readonly GUIContent ButtonContent = new GUIContent("▾", "Pick any object of the id index, closed scenes included.");

        [ClearOnReload(AssignNewInstance = true)] private static AdvancedDropdownState dropdownState = new AdvancedDropdownState();
        [ClearOnReload] private static GUIStyle missingStyle;

        // Index lookup: guid → position in the entries. Rebuilt after a full rebuild or when the entry count changes;
        // a guid found at another position, or a miss (at most once per interval), also rebuilds it.
        [ClearOnReload(AssignNewInstance = true)] private static Dictionary<string, int> positionByGuid = new Dictionary<string, int>(StringComparer.Ordinal);
        [ClearOnReload(AssignNewInstance = true)] private static Dictionary<string, CachedText> textByGuid = new Dictionary<string, CachedText>(StringComparer.Ordinal);
        [ClearOnReload] private static SceneIdIndex lookupIndex;
        [ClearOnReload] private static int lookupCount;
        [ClearOnReload] private static bool lookupDirty;
        [ClearOnReload] private static double lastMissRebuild;

        private readonly GUIContent idContent = new GUIContent();
        private Type targetType;
        private Texture icon;

        private struct CachedText
        {
            public string Id;
            public string Scene;
            public string Label;
            public string Text;
            public bool Missing;
        }

        // ==================== SETUP ====================

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            SceneIdIndexMaintainer.Rebuilt -= MarkLookupDirty;
            SceneIdIndexMaintainer.Rebuilt += MarkLookupDirty;
            lookupDirty = true;
        }

        // ==================== DRAWER ====================

        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
        {
            SerializedProperty _direct = _property.FindPropertyRelative(SceneRefPropertyWalker.DirectField);
            SerializedProperty _guid = _property.FindPropertyRelative(SceneRefPropertyWalker.GuidField);
            if (_direct == null || _guid == null)
            {
                EditorGUI.LabelField(_position, _label.text, "Unsupported SceneRef");
                return;
            }

            if (targetType == null)
            {
                targetType = TargetTypeOf(fieldInfo?.FieldType) ?? typeof(GameObject);
                icon = EditorGUIUtility.ObjectContent(null, targetType).image;
            }

            _position.height = EditorGUIUtility.singleLineHeight;
            _label = EditorGUI.BeginProperty(_position, _label, _property);
            Rect _field = EditorGUI.PrefixLabel(_position, _label);
            Rect _button = new Rect(_field.xMax - ButtonWidth, _field.y, ButtonWidth, _field.height);
            _field.xMax -= ButtonWidth + Spacing;

            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            HandleDrag(_position, _property);
            HandleContextClick(_field, _property);

            Object _live = _direct.objectReferenceValue;
            bool _mixed = _direct.hasMultipleDifferentValues || _guid.hasMultipleDifferentValues;
            if (_live != null || _mixed || string.IsNullOrEmpty(_guid.stringValue)) DrawObjectField(_field, _property, _live, _mixed);
            else DrawIdField(_field, _property, _guid.stringValue);

            if (GUI.Button(_button, ButtonContent, EditorStyles.miniButton)) OpenPicker(_button, _property, targetType);

            EditorGUI.indentLevel = _indent;
            EditorGUI.EndProperty();
        }

        // ==================== INTERNAL ====================

        /// <summary>T of a SceneRef&lt;T&gt; field, also for arrays and lists of them. Null for other types.</summary>
        internal static Type TargetTypeOf(Type _fieldType)
        {
            if (_fieldType == null) return null;
            if (_fieldType.IsArray) _fieldType = _fieldType.GetElementType();
            else if (_fieldType.IsGenericType && _fieldType.GetGenericTypeDefinition() == typeof(List<>)) _fieldType = _fieldType.GetGenericArguments()[0];

            for (Type _type = _fieldType; _type != null; _type = _type.BaseType)
                if (_type.IsGenericType && _type.GetGenericTypeDefinition() == typeof(SceneRef<>)) return _type.GetGenericArguments()[0];
            return null;
        }

        /// <summary>
        /// "&lt;id&gt; · &lt;scene&gt;" with the current names from the index. When the guid is not in it, "&lt;id&gt; · missing"
        /// with the id from the stored label, or "missing" without a label.
        /// </summary>
        internal static string DisplayText(string _guid, string _label, out bool _missing)
        {
            bool _found = TryGetEntry(_guid, out SceneIdIndexEntry _entry);
            string _id = _found ? _entry.Id : null;
            string _scene = _found ? _entry.SceneName : null;

            if (_guid != null && textByGuid.TryGetValue(_guid, out CachedText _cached)
                && _cached.Missing == !_found && _cached.Id == _id && _cached.Scene == _scene && _cached.Label == _label)
            {
                _missing = _cached.Missing;
                return _cached.Text;
            }

            _missing = !_found;
            string _text;
            if (_found) _text = _id + Separator + _scene;
            else
            {
                string _lastId = IdFromLabel(_label);
                _text = string.IsNullOrEmpty(_lastId) ? MissingText : _lastId + Separator + MissingText;
            }

            if (_guid != null) textByGuid[_guid] = new CachedText { Id = _id, Scene = _scene, Label = _label, Text = _text, Missing = _missing };
            return _text;
        }

        /// <summary>The index entry of a guid through the cached lookup.</summary>
        internal static bool TryGetEntry(string _guid, out SceneIdIndexEntry _entry)
        {
            _entry = default;
            SceneIdIndex _index = SceneIdIndex.Existing;
            if (string.IsNullOrEmpty(_guid) || _index == null) return false;

            IReadOnlyList<SceneIdIndexEntry> _entries = _index.Entries;
            if (lookupDirty || !ReferenceEquals(_index, lookupIndex) || _entries.Count != lookupCount) RebuildLookup(_index);

            if (!positionByGuid.TryGetValue(_guid, out int _position))
            {
                double _now = EditorApplication.timeSinceStartup;
                if (_now - lastMissRebuild < MissRebuildInterval) return false;
                lastMissRebuild = _now;
                RebuildLookup(_index);
                if (!positionByGuid.TryGetValue(_guid, out _position)) return false;
            }

            if (_position >= _entries.Count || _entries[_position].Guid != _guid)
            {
                RebuildLookup(_index);
                if (!positionByGuid.TryGetValue(_guid, out _position)) return false;
            }

            _entry = _entries[_position];
            return true;
        }

        // ==================== PRIVATE ====================

        #region Fields

        private void DrawObjectField(Rect _field, SerializedProperty _property, Object _live, bool _mixed)
        {
            EditorGUI.showMixedValue = _mixed;
            EditorGUI.BeginChangeCheck();
            Object _picked = EditorGUI.ObjectField(_field, GUIContent.none, _live, targetType, true);
            if (EditorGUI.EndChangeCheck())
            {
                if (_picked == null) SceneRefAssign.Clear(_property);
                else SceneRefAssign.TryAssign(_property, _picked, targetType);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawIdField(Rect _field, SerializedProperty _property, string _guid)
        {
            string _label = _property.FindPropertyRelative(SceneRefPropertyWalker.LabelField).stringValue;
            idContent.text = DisplayText(_guid, _label, out bool _missing);
            idContent.image = icon;
            idContent.tooltip = _guid;

            Event _event = Event.current;
            if (_event.type == EventType.Repaint)
            {
                GUIStyle _style = _missing ? MissingStyle() : EditorStyles.objectField;
                _style.Draw(_field, idContent, _field.Contains(_event.mousePosition), false, false, false);
            }
            else if (_event.type == EventType.MouseDown && _event.button == 0 && _field.Contains(_event.mousePosition))
            {
                // Pings the object when its scene is open.
                Object _loaded = SceneRefAssign.FindLoaded(_guid, typeof(GameObject), 0);
                if (_loaded != null) EditorGUIUtility.PingObject(_loaded);
                _event.Use();
            }
        }

        private static GUIStyle MissingStyle()
        {
            if (missingStyle != null) return missingStyle;
            missingStyle = new GUIStyle(EditorStyles.objectField);
            missingStyle.normal.textColor = MissingColor;
            missingStyle.hover.textColor = MissingColor;
            missingStyle.focused.textColor = MissingColor;
            missingStyle.active.textColor = MissingColor;
            return missingStyle;
        }

        #endregion

        #region Input

        /// <summary>Takes drags over the whole line so the object field never stores a cross scene reference on its own.</summary>
        private void HandleDrag(Rect _line, SerializedProperty _property)
        {
            Event _event = Event.current;
            if ((_event.type != EventType.DragUpdated && _event.type != EventType.DragPerform) || !_line.Contains(_event.mousePosition)) return;

            Object _accepted = null;
            Object[] _dragged = DragAndDrop.objectReferences;
            for (int _i = 0; _i < _dragged.Length && _accepted == null; _i++)
                if (SceneRefAssign.CanAssign(_property, _dragged[_i], targetType)) _accepted = _dragged[_i];

            DragAndDrop.visualMode = _accepted != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            if (_event.type == EventType.DragPerform && _accepted != null)
            {
                DragAndDrop.AcceptDrag();
                SceneRefAssign.TryAssign(_property, _accepted, targetType);
                GUI.changed = true;
            }
            _event.Use();
        }

        private static void HandleContextClick(Rect _field, SerializedProperty _property)
        {
            Event _event = Event.current;
            if (_event.type != EventType.ContextClick || !_field.Contains(_event.mousePosition)) return;

            Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;
            GenericMenu _menu = new GenericMenu();
            _menu.AddItem(new GUIContent("Clear"), false, () => Edit(_targets, _path, SceneRefAssign.Clear));
            _menu.ShowAsContext();
            _event.Use();
        }

        private static void OpenPicker(Rect _button, SerializedProperty _property, Type _targetType)
        {
            SceneIdIndex _index = SceneIdIndex.Existing;
            IReadOnlyList<SceneIdIndexEntry> _entries = _index != null ? _index.Entries : null;
            Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;

            SceneRefPickerDropdown _dropdown = new SceneRefPickerDropdown(dropdownState, _targetType, _entries, _item => Edit(_targets, _path, _ref =>
            {
                if (_item.HasEntry) SceneRefAssign.AssignFromIndex(_ref, _item.Entry, 0, _targetType);
                else SceneRefAssign.Clear(_ref);
            }));
            _dropdown.Show(_button);
        }

        // Menus and the picker answer after the drawer call, so they edit through a fresh SerializedObject (keeps Undo and multi-object editing).
        private static void Edit(Object[] _targets, string _path, Action<SerializedProperty> _edit)
        {
            using (SerializedObject _object = new SerializedObject(_targets))
            {
                SerializedProperty _ref = _object.FindProperty(_path);
                if (_ref != null) _edit(_ref);
            }
        }

        #endregion

        #region Lookup

        private static void MarkLookupDirty() => lookupDirty = true;

        private static void RebuildLookup(SceneIdIndex _index)
        {
            positionByGuid.Clear();
            IReadOnlyList<SceneIdIndexEntry> _entries = _index.Entries;
            for (int _i = 0; _i < _entries.Count; _i++)
                if (!string.IsNullOrEmpty(_entries[_i].Guid)) positionByGuid[_entries[_i].Guid] = _i;

            lookupIndex = _index;
            lookupCount = _entries.Count;
            lookupDirty = false;
        }

        private static string IdFromLabel(string _label)
        {
            if (string.IsNullOrEmpty(_label)) return null;
            int _separator = _label.IndexOf(LabelSeparator, StringComparison.Ordinal);
            return _separator >= 0 ? _label.Substring(0, _separator) : _label;
        }

        #endregion
    }
}
