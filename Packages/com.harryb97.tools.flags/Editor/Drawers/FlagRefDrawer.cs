using System;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// One line drawer for <see cref="FlagRef"/>: scope icon and current path from the database, in red when the flag was deleted or
    /// is a local of another scene. ▾ or a click opens the picker, which only offers globals and the locals of the host scene.
    /// </summary>
    [CustomPropertyDrawer(typeof(FlagRef))]
    internal sealed class FlagRefDrawer : PropertyDrawer
    {
        // ==================== VARIABLES ====================

        private const string GuidField = "guid";
        private const string LabelField = "label";
        private const float ButtonWidth = 20f;
        private const float Spacing = 2f;
        private const string NoneText = "None";
        private const string DeletedText = "(deleted)";
        private const string ForeignText = "(other scene)";

        private static readonly Color WarningColor = new Color(0.95f, 0.3f, 0.3f);
        private static readonly GUIContent ButtonContent = new GUIContent("▾", "Pick a global flag or a flag of this scene.");

        [ClearOnReload(AssignNewInstance = true)] private static AdvancedDropdownState dropdownState = new AdvancedDropdownState();
        [ClearOnReload] private static GUIStyle warningStyle;

        private readonly GUIContent content = new GUIContent();

        // ==================== DRAWER ====================

        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
        {
            SerializedProperty _guid = _property.FindPropertyRelative(GuidField);
            SerializedProperty _flagLabel = _property.FindPropertyRelative(LabelField);
            if (_guid == null || _flagLabel == null)
            {
                EditorGUI.LabelField(_position, _label.text, "Unsupported FlagRef");
                return;
            }

            UnityEngine.Object[] _targets = _property.serializedObject.targetObjects;
            string _hostScene = FlagHostScope.SceneGuidOf(_targets, out bool _mixedScenes);
            FlagDatabase _database = FlagDatabaseLocator.Find();

            _position.height = EditorGUIUtility.singleLineHeight;
            _label = EditorGUI.BeginProperty(_position, _label, _property);
            Rect _field = EditorGUI.PrefixLabel(_position, _label);
            Rect _button = new Rect(_field.xMax - ButtonWidth, _field.y, ButtonWidth, _field.height);
            _field.xMax -= ButtonWidth + Spacing;

            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            bool _mixedValue = _guid.hasMultipleDifferentValues;
            FlagRefState _state = FlagRefState.Empty;
            if (_mixedValue) content.text = "—";
            else content.text = DisplayText(_database, _guid.stringValue, _flagLabel.stringValue, _hostScene, out _state);
            content.image = _mixedValue ? null : IconFor(_database, _guid.stringValue, _state);
            content.tooltip = _guid.stringValue;

            bool _pickable = GUI.enabled && !_mixedScenes;
            Event _event = Event.current;
            if (_event.type == EventType.Repaint)
            {
                GUIStyle _style = _state == FlagRefState.Deleted || _state == FlagRefState.ForeignLocal ? WarningStyle() : EditorStyles.objectField;
                _style.Draw(_field, content, _field.Contains(_event.mousePosition), false, false, false);
            }
            else if (_event.type == EventType.MouseDown && _event.button == 0 && _field.Contains(_event.mousePosition) && _pickable)
            {
                OpenPicker(_field, _property, _database, _hostScene);
                _event.Use();
            }
            else if (_event.type == EventType.ContextClick && _field.Contains(_event.mousePosition))
            {
                ShowContextMenu(_targets, _property.propertyPath);
                _event.Use();
            }

            EditorGUI.BeginDisabledGroup(!_pickable);
            ButtonContent.tooltip = _mixedScenes ? "The selected objects live in different scenes." : "Pick a global flag or a flag of this scene.";
            if (GUI.Button(_button, ButtonContent, EditorStyles.miniButton)) OpenPicker(_button, _property, _database, _hostScene);
            EditorGUI.EndDisabledGroup();

            EditorGUI.indentLevel = _indent;
            EditorGUI.EndProperty();
        }

        // ==================== INTERNAL ====================

        /// <summary>
        /// Text shown in the field: the current "Scope / Name" from the database. A deleted flag shows the stored label with "(deleted)",
        /// a local of another scene shows its path with "(other scene)".
        /// </summary>
        internal static string DisplayText(FlagDatabase _database, string _guid, string _label, string _hostSceneGuid, out FlagRefState _state)
        {
            _state = FlagHostScope.StateOf(_database, _guid, _hostSceneGuid);
            switch (_state)
            {
                case FlagRefState.Empty:
                    return NoneText;
                case FlagRefState.Deleted:
                    return string.IsNullOrEmpty(_label) ? DeletedText : _label + " " + DeletedText;
                default:
                    _database.TryGet(_guid, out FlagDefinition _flag);
                    return _state == FlagRefState.ForeignLocal ? _flag.DisplayPath + " " + ForeignText : _flag.DisplayPath;
            }
        }

        // ==================== PRIVATE ====================

        private static Texture IconFor(FlagDatabase _database, string _guid, FlagRefState _state)
        {
            if (_state == FlagRefState.Deleted || _state == FlagRefState.ForeignLocal) return EditorGUIUtility.FindTexture("console.warnicon.sml");
            if (_state == FlagRefState.Empty || !_database.TryGet(_guid, out FlagDefinition _flag)) return null;
            return EditorGUIUtility.FindTexture(_flag.Scope == FlagScope.Global ? "BuildSettings.Web.Small" : "SceneAsset Icon");
        }

        private static GUIStyle WarningStyle()
        {
            if (warningStyle != null) return warningStyle;
            warningStyle = new GUIStyle(EditorStyles.objectField);
            warningStyle.normal.textColor = WarningColor;
            warningStyle.hover.textColor = WarningColor;
            warningStyle.focused.textColor = WarningColor;
            warningStyle.active.textColor = WarningColor;
            return warningStyle;
        }

        private static void OpenPicker(Rect _anchor, SerializedProperty _property, FlagDatabase _database, string _hostScene)
        {
            UnityEngine.Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;

            FlagPickerDropdown _dropdown = new FlagPickerDropdown(
                dropdownState, _database, _hostScene,
                _flag => Edit(_targets, _path, _ref => Assign(_ref, _flag)),
                CreateNewHandler(_targets, _path, _hostScene));
            _dropdown.Show(_anchor);
        }

        /// <summary>
        /// Opens the Flags window ready to create a flag the host may use: a local of its scene when it lives in one, otherwise a global.
        /// The created flag is assigned to the field.
        /// </summary>
        private static Action CreateNewHandler(UnityEngine.Object[] _targets, string _path, string _hostScene)
        {
            FlagScope _scope = string.IsNullOrEmpty(_hostScene) ? FlagScope.Global : FlagScope.Local;
            return () => FlagsWindow.OpenForCreate(_scope, _hostScene, _flag => AssignCreated(_targets, _path, _flag));
        }

        // The hosts may have been destroyed (scene closed) while the window was open.
        private static void AssignCreated(UnityEngine.Object[] _targets, string _path, FlagDefinition _flag)
        {
            UnityEngine.Object[] _alive = Array.FindAll(_targets, _target => _target != null);
            if (_alive.Length == 0) return;
            Edit(_alive, _path, _ref => Assign(_ref, _flag));
        }

        private static void ShowContextMenu(UnityEngine.Object[] _targets, string _path)
        {
            GenericMenu _menu = new GenericMenu();
            if (GUI.enabled) _menu.AddItem(new GUIContent("Clear"), false, () => Edit(_targets, _path, _ref => Assign(_ref, null)));
            else _menu.AddDisabledItem(new GUIContent("Clear"));
            _menu.ShowAsContext();
        }

        private static void Assign(SerializedProperty _ref, FlagDefinition _flag)
        {
            _ref.FindPropertyRelative(GuidField).stringValue = _flag != null ? _flag.Guid : string.Empty;
            _ref.FindPropertyRelative(LabelField).stringValue = _flag != null ? _flag.DisplayPath : string.Empty;
            _ref.serializedObject.ApplyModifiedProperties();
        }

        // The picker answers after the drawer call, so it edits through a fresh SerializedObject (keeps Undo and multi-object editing).
        private static void Edit(UnityEngine.Object[] _targets, string _path, Action<SerializedProperty> _edit)
        {
            using (SerializedObject _object = new SerializedObject(_targets))
            {
                SerializedProperty _ref = _object.FindProperty(_path);
                if (_ref != null) _edit(_ref);
            }
        }
    }
}
