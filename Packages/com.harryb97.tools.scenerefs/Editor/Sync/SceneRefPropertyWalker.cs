using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Finds every serialized <see cref="SceneRefBase"/> of an object, including those inside [SerializeReference] fields and lists.
    /// Only fields whose type can hold one are visited (<see cref="SceneRefTypeCache"/>), so plain data costs nothing.
    /// </summary>
    internal static class SceneRefPropertyWalker
    {
        // ==================== VARIABLES ====================

        internal const string DirectField = "direct";
        internal const string GuidField = "guid";
        internal const string ComponentTypeField = "componentType";
        internal const string ComponentIndexField = "componentIndex";
        internal const string LabelField = "label";

        // Unity reports generic serializable classes by their open generic name.
        private const string SceneRefTypeName = "SceneRef`1";

        // Managed references can form long chains; deeper data is not searched.
        private const int MaxDepth = 64;

        // ==================== PUBLIC API ====================

        /// <summary>Copies of every SceneRef property of the object, in serialization order.</summary>
        public static IEnumerable<SerializedProperty> FindAll(SerializedObject _so)
        {
            List<SerializedProperty> _result = new List<SerializedProperty>();
            if (_so == null || _so.targetObject == null || _so.isEditingMultipleObjects) return _result;

            FieldInfo[] _fields = SceneRefTypeCache.HoldingFields(_so.targetObject.GetType());
            for (int _i = 0; _i < _fields.Length; _i++) Visit(_so.FindProperty(_fields[_i].Name), _fields[_i].FieldType, _result, 0);
            return _result;
        }

        // ==================== PRIVATE ====================

        private static void Visit(SerializedProperty _property, Type _fieldType, List<SerializedProperty> _result, int _depth)
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
                Type _element = SceneRefTypeCache.ElementType(_fieldType);
                for (int _i = 0; _i < _property.arraySize; _i++) Visit(_property.GetArrayElementAtIndex(_i), _element, _result, _depth + 1);
                return;
            }

            if (IsSceneRef(_property))
            {
                _result.Add(_property.Copy());
                return;
            }

            VisitChildren(_property, _fieldType, _result, _depth + 1);
        }

        private static void VisitChildren(SerializedProperty _parent, Type _type, List<SerializedProperty> _result, int _depth)
        {
            FieldInfo[] _fields = SceneRefTypeCache.HoldingFields(_type);
            for (int _i = 0; _i < _fields.Length; _i++) Visit(_parent.FindPropertyRelative(_fields[_i].Name), _fields[_i].FieldType, _result, _depth);
        }

        private static bool IsSceneRef(SerializedProperty _property)
        {
            if (_property.propertyType != SerializedPropertyType.Generic || _property.type != SceneRefTypeName) return false;

            return Is(_property, DirectField, SerializedPropertyType.ObjectReference)
                && Is(_property, GuidField, SerializedPropertyType.String)
                && Is(_property, ComponentTypeField, SerializedPropertyType.String)
                && Is(_property, ComponentIndexField, SerializedPropertyType.Integer)
                && Is(_property, LabelField, SerializedPropertyType.String);
        }

        private static bool Is(SerializedProperty _parent, string _name, SerializedPropertyType _type)
        {
            SerializedProperty _child = _parent.FindPropertyRelative(_name);
            return _child != null && _child.propertyType == _type;
        }
    }
}
