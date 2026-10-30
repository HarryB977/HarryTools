using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using Object = UnityEngine.Object;

namespace HarryTools.SerializeReferences.Editor {
    [CustomPropertyDrawer(typeof(TypeSelectorAttribute))]
    public class TypeSelectorDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const int MaxPopupLineCount = 13;
        private const float ClearButtonWidth = 60f;

        private static readonly GUIContent NullLabel = new(TypeMenuUtility.NullDisplayName);
        private static readonly GUIContent NotManagedReferenceLabel = new("El campo necesita el atributo [SerializeReference]");
        private static readonly GUIContent ClearLabel = new("Limpiar", "Elimina la referencia a un tipo que ya no existe");
        private static readonly GUIContent TempLabel = new();

        private static GUIStyle missingStyle;

        private readonly Dictionary<string, TypePopup> popups = new();
        private readonly Dictionary<Type, GUIContent> typeLabels = new();
        #endregion

        // ==================== METHODS ====================
        #region Draw
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            if (_property.propertyType != SerializedPropertyType.ManagedReference) {
                EditorGUI.LabelField(_position, _label, NotManagedReferenceLabel);
                return;
            }

            foreach (Object _target in _property.serializedObject.targetObjects) DuplicateReferenceFixer.Schedule(_target);

            TypeSelectorAttribute _attribute = (TypeSelectorAttribute)attribute;

            if (_attribute.UseToStringAsLabel && !_property.hasMultipleDifferentValues) {
                string _text = _property.managedReferenceValue?.ToString();
                if (!string.IsNullOrEmpty(_text)) _label = new GUIContent(_text, _label.tooltip);
            }

            _label = EditorGUI.BeginProperty(_position, _label, _property);

            Rect _lineRect = new(_position.x, _position.y, _position.width, EditorGUIUtility.singleLineHeight);
            Rect _popupRect = EditorGUI.PrefixLabel(_lineRect, _label);

            string _fullTypename = _property.managedReferenceFullTypename;
            Type _currentType = TypeUtility.GetManagedReferenceType(_fullTypename);
            bool _isMissing = !string.IsNullOrEmpty(_fullTypename) && _currentType == null;

            // Tipo perdido (clase renombrada o eliminada)
            if (_isMissing) {
                DrawMissingType(_popupRect, _property, _fullTypename);
                EditorGUI.EndProperty();
                return;
            }

            // Desplegable
            if (EditorGUI.DropdownButton(_popupRect, GetTypeLabel(_currentType), FocusType.Keyboard)) {
                ShowPopup(_popupRect, _property, _attribute);
            }

            // Foldout (solo sobre la zona de la etiqueta, para no robar clicks al desplegable)
            if (_currentType != null) {
                Rect _foldoutRect = new(_lineRect.x, _lineRect.y, Mathf.Max(0, _popupRect.x - _lineRect.x), _lineRect.height);
                _property.isExpanded = EditorGUI.Foldout(_foldoutRect, _property.isExpanded, GUIContent.none, true);
            }

            if (_property.isExpanded && _currentType != null) {
                using (new EditorGUI.IndentLevelScope()) {
                    DrawContent(_position, _property, _label, _currentType);
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawContent(Rect _position, SerializedProperty _property, GUIContent _label, Type _type) {
            Rect _contentRect = _position;
            _contentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Drawer personalizado del tipo concreto
            if (PropertyDrawerCache.TryGetPropertyDrawer(_type, out PropertyDrawer _customDrawer)) {
                _contentRect.height = _customDrawer.GetPropertyHeight(_property, _label);
                _customDrawer.OnGUI(_contentRect, _property, _label);
                return;
            }

            foreach (SerializedProperty _child in _property.GetChildProperties()) {
                TempLabel.text = _child.displayName;
                TempLabel.tooltip = _child.tooltip;

                _contentRect.height = EditorGUI.GetPropertyHeight(_child, TempLabel, true);
                EditorGUI.PropertyField(_contentRect, _child, true);

                _contentRect.y += _contentRect.height + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private static void DrawMissingType(Rect _rect, SerializedProperty _property, string _fullTypename) {
            missingStyle ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };

            Rect _labelRect = new(_rect.x, _rect.y, _rect.width - ClearButtonWidth - 2, _rect.height);
            Rect _buttonRect = new(_labelRect.xMax + 2, _rect.y, ClearButtonWidth, _rect.height);

            EditorGUI.LabelField(_labelRect, new GUIContent($"Tipo perdido: {_fullTypename}", _fullTypename), missingStyle);

            if (GUI.Button(_buttonRect, ClearLabel)) {
                foreach (Object _target in _property.serializedObject.targetObjects) {
                    using SerializedObject _serializedObject = new(_target);
                    SerializedProperty _individual = _serializedObject.FindProperty(_property.propertyPath);
                    if (_individual == null) continue;

                    _individual.managedReferenceValue = null;
                    _serializedObject.ApplyModifiedProperties();
                }
            }
        }
        #endregion

        // =================================================

        #region Popup
        private void ShowPopup(Rect _rect, SerializedProperty _property, TypeSelectorAttribute _attribute) {
            string _fieldTypename = _property.managedReferenceFieldTypename;
            string _key = $"{_fieldTypename}|{_attribute.AllowNull}";

            if (!popups.TryGetValue(_key, out TypePopup _popup)) {
                Type _baseType = TypeUtility.GetManagedReferenceType(_fieldTypename);
                _popup = new TypePopup(TypeUtility.GetCandidateTypes(_baseType), _attribute.AllowNull, MaxPopupLineCount, new AdvancedDropdownState());
                popups.Add(_key, _popup);
            }

            // Se guardan los objetos y la ruta: la SerializedProperty no es fiable cuando se cierra el desplegable
            Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;

            _popup.OnItemSelected = _item => ApplyType(_targets, _path, _item.Type);
            _popup.Show(_rect);
        }

        private static void ApplyType(Object[] _targets, string _path, Type _type) {
            foreach (Object _target in _targets) {
                if (_target == null) continue;

                using SerializedObject _serializedObject = new(_target);
                SerializedProperty _property = _serializedObject.FindProperty(_path);
                if (_property == null) continue;

                object _result = _property.SetManagedReference(_type);
                _property.isExpanded = _result != null;

                _serializedObject.ApplyModifiedProperties();
            }

            Undo.SetCurrentGroupName($"Change Type ({TypeMenuUtility.GetDisplayName(_type)})");
        }

        private GUIContent GetTypeLabel(Type _type) {
            if (_type == null) return NullLabel;
            if (typeLabels.TryGetValue(_type, out GUIContent _cached)) return _cached;

            GUIContent _result = new(TypeMenuUtility.GetDisplayName(_type), _type.FullName);
            typeLabels.Add(_type, _result);

            return _result;
        }
        #endregion

        // =================================================

        #region Height
        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) {
            float _height = EditorGUIUtility.singleLineHeight;

            if (_property.propertyType != SerializedPropertyType.ManagedReference || !_property.isExpanded) return _height;

            Type _type = TypeUtility.GetManagedReferenceType(_property.managedReferenceFullTypename);
            if (_type == null) return _height;

            _height += EditorGUIUtility.standardVerticalSpacing;

            if (PropertyDrawerCache.TryGetPropertyDrawer(_type, out PropertyDrawer _customDrawer)) {
                return _height + _customDrawer.GetPropertyHeight(_property, _label);
            }

            bool _first = true;

            foreach (SerializedProperty _child in _property.GetChildProperties()) {
                if (!_first) _height += EditorGUIUtility.standardVerticalSpacing;
                _first = false;

                TempLabel.text = _child.displayName;
                TempLabel.tooltip = _child.tooltip;

                _height += EditorGUI.GetPropertyHeight(_child, TempLabel, true);
            }

            return _height;
        }
        #endregion
    }
}
