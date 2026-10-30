using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HarryTools.SerializeReferences.Editor {
    public static class SerializedPropertyUtility {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        #endregion

        // ==================== METHODS ====================
        #region Children
        /// <summary> Devuelve los hijos directos de una propiedad </summary>
        public static IEnumerable<SerializedProperty> GetChildProperties(this SerializedProperty _parent) {
            SerializedProperty _iterator = _parent.Copy();
            SerializedProperty _end = _parent.GetEndProperty();

            if (!_iterator.NextVisible(true)) yield break;

            while (!SerializedProperty.EqualContents(_iterator, _end)) {
                yield return _iterator.Copy();
                if (!_iterator.NextVisible(false)) yield break;
            }
        }
        #endregion

        // =================================================

        #region Reflection
        /// <summary>
        /// Devuelve el campo C# que representa una propiedad serializada.
        /// Atraviesa arrays, listas y 'SerializeReference' (usando el tipo real de cada instancia).
        /// </summary>
        public static FieldInfo GetFieldInfo(this SerializedProperty _property) {
            if (_property == null) return null;

            SerializedObject _serializedObject = _property.serializedObject;
            if (_serializedObject.targetObject == null) return null;

            Type _currentType = _serializedObject.targetObject.GetType();
            FieldInfo _field = null;

            string[] _parts = _property.propertyPath.Replace(".Array.data[", "[").Split('.');
            string _currentPath = null;

            for (int i = 0; i < _parts.Length; i++) {
                string _part = _parts[i];
                int _bracketIndex = _part.IndexOf('[');
                string _fieldName = _bracketIndex >= 0 ? _part[.._bracketIndex] : _part;

                _field = FindField(_currentType, _fieldName);
                if (_field == null) return null;

                Type _fieldType = _field.FieldType;
                _currentPath = _currentPath == null ? _fieldName : $"{_currentPath}.{_fieldName}";

                if (_bracketIndex >= 0) {
                    _currentPath += $".Array.data{_part[_bracketIndex..]}";
                    _fieldType = GetElementType(_fieldType);
                }

                // Las 'SerializeReference' pueden contener un tipo derivado, hay que usar el real para seguir bajando
                if (i < _parts.Length - 1 && _field.IsDefined(typeof(SerializeReference), true)) {
                    SerializedProperty _referenceProperty = _serializedObject.FindProperty(_currentPath);

                    if (_referenceProperty is { propertyType: SerializedPropertyType.ManagedReference }) {
                        _fieldType = _referenceProperty.managedReferenceValue?.GetType() ?? _fieldType;
                    }
                }

                _currentType = _fieldType;
            }

            return _field;
        }

        /// <summary> Indica si el campo de la propiedad tiene el atributo [TypeSelector] </summary>
        public static bool HasTypeSelector(this SerializedProperty _property) {
            FieldInfo _field = _property.GetFieldInfo();
            return _field != null && _field.IsDefined(typeof(TypeSelectorAttribute), true);
        }

        private static FieldInfo FindField(Type _type, string _name) {
            for (Type _current = _type; _current != null && _current != typeof(object); _current = _current.BaseType) {
                FieldInfo _field = _current.GetField(_name, FieldFlags);
                if (_field != null) return _field;
            }

            return null;
        }

        private static Type GetElementType(Type _collectionType) {
            if (_collectionType.IsArray) return _collectionType.GetElementType();
            if (_collectionType.IsGenericType && _collectionType.GetGenericTypeDefinition() == typeof(List<>)) return _collectionType.GetGenericArguments()[0];

            return _collectionType;
        }
        #endregion
    }
}
