using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Finds every serialized <see cref="SceneRefBase"/> of an object, including those inside [SerializeReference] fields and lists.
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

        // ==================== PUBLIC API ====================

        /// <summary>Copies of every SceneRef property of the object, in serialization order.</summary>
        public static IEnumerable<SerializedProperty> FindAll(SerializedObject _so)
        {
            List<SerializedProperty> _result = new List<SerializedProperty>();
            if (_so == null || _so.targetObject == null) return _result;

            SerializedProperty _iterator = _so.GetIterator();
            bool _enterChildren = true;
            while (_iterator.Next(_enterChildren))
            {
                if (IsSceneRef(_iterator))
                {
                    _result.Add(_iterator.Copy());
                    _enterChildren = false;
                    continue;
                }

                _enterChildren = ShouldEnter(_iterator);
            }

            return _result;
        }

        // ==================== PRIVATE ====================

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

        /// <summary>Only plain classes, managed references and arrays of them can hold a SceneRef.</summary>
        private static bool ShouldEnter(SerializedProperty _property)
        {
            if (_property.propertyType == SerializedPropertyType.ManagedReference) return true;
            if (_property.propertyType != SerializedPropertyType.Generic) return false;
            if (!_property.isArray) return true;
            if (_property.arraySize == 0) return false;

            SerializedPropertyType _element = _property.GetArrayElementAtIndex(0).propertyType;
            return _element == SerializedPropertyType.Generic || _element == SerializedPropertyType.ManagedReference;
        }
    }
}
