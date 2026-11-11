using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace HarryTools.Flags.Editor
{
    /// <summary>A serialized FlagRef found by <see cref="FlagRefPropertyWalker"/>, with the field that declares it.</summary>
    internal readonly struct FlagRefMatch
    {
        // ==================== VARIABLES ====================

        /// <summary>Copy of the FlagRef property.</summary>
        public readonly SerializedProperty Property;

        /// <summary>The field holding the FlagRef (or the array or list of them).</summary>
        public readonly FieldInfo Field;

        // ==================== CONSTRUCTORS ====================

        public FlagRefMatch(SerializedProperty _property, FieldInfo _field)
        {
            Property = _property;
            Field = _field;
        }

        // ==================== PROPERTIES ====================

        public string Guid => Property.FindPropertyRelative(FlagRefPropertyWalker.GuidField).stringValue;
    }

    /// <summary>
    /// Finds every serialized <see cref="FlagRef"/> of an object, including those inside [SerializeReference] fields and lists.
    /// Only fields whose type can hold one are visited (<see cref="FlagRefTypeCache"/>), so plain data costs nothing.
    /// </summary>
    internal static class FlagRefPropertyWalker
    {
        // ==================== VARIABLES ====================

        internal const string GuidField = "guid";
        internal const string LabelField = "label";

        private const string FlagRefTypeName = nameof(FlagRef);

        // Managed references can form long chains; deeper data is not searched.
        private const int MaxDepth = 64;

        // ==================== PUBLIC API ====================

        /// <summary>Every FlagRef property of the object, in serialization order.</summary>
        public static List<FlagRefMatch> FindAll(SerializedObject _so)
        {
            List<FlagRefMatch> _result = new List<FlagRefMatch>();
            if (_so == null || _so.targetObject == null || _so.isEditingMultipleObjects) return _result;

            FieldInfo[] _fields = FlagRefTypeCache.HoldingFields(_so.targetObject.GetType());
            for (int _i = 0; _i < _fields.Length; _i++) Visit(_so.FindProperty(_fields[_i].Name), _fields[_i], _fields[_i].FieldType, _result, 0);
            return _result;
        }

        // ==================== PRIVATE ====================

        private static void Visit(SerializedProperty _property, FieldInfo _field, Type _fieldType, List<FlagRefMatch> _result, int _depth)
        {
            if (_property == null || _depth > MaxDepth) return;

            if (_property.propertyType == SerializedPropertyType.ManagedReference)
            {
                // The declared type says nothing here: the value can be any type.
                object _value = _property.managedReferenceValue;
                if (_value != null) VisitChildren(_property, _value.GetType(), _result, _depth + 1);
                return;
            }

            if (_property.propertyType != SerializedPropertyType.Generic) return;

            if (_property.isArray)
            {
                Type _element = FlagRefTypeCache.ElementType(_fieldType);
                for (int _i = 0; _i < _property.arraySize; _i++) Visit(_property.GetArrayElementAtIndex(_i), _field, _element, _result, _depth + 1);
                return;
            }

            if (IsFlagRef(_property))
            {
                _result.Add(new FlagRefMatch(_property.Copy(), _field));
                return;
            }

            VisitChildren(_property, _fieldType, _result, _depth + 1);
        }

        private static void VisitChildren(SerializedProperty _parent, Type _type, List<FlagRefMatch> _result, int _depth)
        {
            FieldInfo[] _fields = FlagRefTypeCache.HoldingFields(_type);
            for (int _i = 0; _i < _fields.Length; _i++) Visit(_parent.FindPropertyRelative(_fields[_i].Name), _fields[_i], _fields[_i].FieldType, _result, _depth);
        }

        private static bool IsFlagRef(SerializedProperty _property)
        {
            if (_property.propertyType != SerializedPropertyType.Generic || _property.type != FlagRefTypeName) return false;

            return IsString(_property, GuidField) && IsString(_property, LabelField);
        }

        private static bool IsString(SerializedProperty _parent, string _name)
        {
            SerializedProperty _child = _parent.FindPropertyRelative(_name);
            return _child != null && _child.propertyType == SerializedPropertyType.String;
        }
    }
}
