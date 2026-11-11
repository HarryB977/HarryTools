using System;
using System.Collections.Generic;
using System.Reflection;
using HarryTools.DomainReload;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Per type answer, computed once by reflection, to "can its serialized data hold a FlagRef", so the indexer skips scripts
    /// and fields that never can. [SerializeReference] fields always count as maybe, since their value can be any type.
    /// </summary>
    internal static class FlagRefTypeCache
    {
        // ==================== VARIABLES ====================

        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly FieldInfo[] NoFields = new FieldInfo[0];

        [ClearOnReload(AssignNewInstance = true)] private static Dictionary<Type, FieldInfo[]> fieldsByType = new Dictionary<Type, FieldInfo[]>();
        // Types being computed; a type that reaches itself counts as maybe.
        [ClearOnReload(AssignNewInstance = true)] private static HashSet<Type> inProgress = new HashSet<Type>();

        // ==================== PUBLIC API ====================

        /// <summary>True when an object of the type may serialize a FlagRef.</summary>
        public static bool CanHold(Type _type) => _type != null && (_type == typeof(FlagRef) || HoldingFields(_type).Length > 0);

        /// <summary>The serialized fields of the type that may hold a FlagRef, base class fields first.</summary>
        public static FieldInfo[] HoldingFields(Type _type)
        {
            if (_type == null) return NoFields;
            if (fieldsByType.TryGetValue(_type, out FieldInfo[] _cached)) return _cached;

            inProgress.Add(_type);
            List<FieldInfo> _result = new List<FieldInfo>();
            try
            {
                List<Type> _chain = new List<Type>();
                for (Type _current = _type; _current != null && !IsStop(_current); _current = _current.BaseType) _chain.Add(_current);
                _chain.Reverse();

                for (int _c = 0; _c < _chain.Count; _c++)
                {
                    FieldInfo[] _fields = _chain[_c].GetFields(Fields);
                    for (int _f = 0; _f < _fields.Length; _f++)
                    {
                        FieldInfo _field = _fields[_f];
                        if (!IsSerialized(_field)) continue;
                        if (_field.IsDefined(typeof(SerializeReference), true) || FieldTypeCanHold(_field.FieldType)) _result.Add(_field);
                    }
                }
            }
            finally
            {
                inProgress.Remove(_type);
            }

            FieldInfo[] _array = _result.Count == 0 ? NoFields : _result.ToArray();
            fieldsByType[_type] = _array;
            return _array;
        }

        /// <summary>Element type of an array or List; null for other types.</summary>
        public static Type ElementType(Type _type)
        {
            if (_type == null) return null;
            if (_type.IsArray) return _type.GetElementType();
            if (_type.IsGenericType && _type.GetGenericTypeDefinition() == typeof(List<>)) return _type.GetGenericArguments()[0];
            return null;
        }

        // ==================== PRIVATE ====================

        private static bool FieldTypeCanHold(Type _type)
        {
            if (_type == typeof(FlagRef)) return true;

            Type _element = ElementType(_type);
            if (_element != null) return FieldTypeCanHold(_element);

            if (_type.IsPrimitive || _type.IsEnum || _type == typeof(string) || _type.IsPointer) return false;
            if (typeof(Object).IsAssignableFrom(_type)) return false;
            // Abstract types and interfaces are only serialized through [SerializeReference], handled by the field.
            if (_type.IsAbstract || _type.IsInterface || !_type.IsSerializable) return false;
            if (inProgress.Contains(_type)) return true;
            return HoldingFields(_type).Length > 0;
        }

        private static bool IsSerialized(FieldInfo _field)
        {
            if (_field.IsStatic || _field.IsInitOnly || _field.IsLiteral || _field.IsNotSerialized) return false;
            return _field.IsPublic || _field.IsDefined(typeof(SerializeField), true) || _field.IsDefined(typeof(SerializeReference), true);
        }

        private static bool IsStop(Type _type) =>
            _type == typeof(object) || _type == typeof(Object) || _type == typeof(MonoBehaviour) || _type == typeof(ScriptableObject)
            || _type == typeof(Behaviour) || _type == typeof(Component);
    }
}
