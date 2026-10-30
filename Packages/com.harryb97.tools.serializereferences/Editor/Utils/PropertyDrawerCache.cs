using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace HarryTools.SerializeReferences.Editor {
    /// <summary> Busca el 'PropertyDrawer' personalizado de un tipo concreto para dibujar su contenido </summary>
    public static class PropertyDrawerCache {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Dictionary<Type, PropertyDrawer> Caches = new();
        #endregion

        // ==================== METHODS ====================
        public static bool TryGetPropertyDrawer(Type _type, out PropertyDrawer _drawer) {
            if (!Caches.TryGetValue(_type, out _drawer)) {
                Type _drawerType = GetCustomPropertyDrawerType(_type);
                _drawer = _drawerType != null ? (PropertyDrawer)Activator.CreateInstance(_drawerType) : null;

                Caches.Add(_type, _drawer);
            }

            return _drawer != null;
        }

        private static Type GetCustomPropertyDrawerType(Type _type) {
            Type[] _interfaces = _type.GetInterfaces();

            foreach (Type _drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>()) {
                foreach (CustomPropertyDrawer _attribute in _drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true)) {
                    if (typeof(CustomPropertyDrawer).GetField("m_Type", PrivateFlags)?.GetValue(_attribute) is not Type _targetType) continue;
                    if (_targetType == _type) return _drawerType;

                    object _useForChildren = typeof(CustomPropertyDrawer).GetField("m_UseForChildren", PrivateFlags)?.GetValue(_attribute);
                    if (_useForChildren is not true) continue;

                    if (Array.Exists(_interfaces, _interface => _interface == _targetType)) return _drawerType;

                    for (Type _base = _type.BaseType; _base != null; _base = _base.BaseType) {
                        if (_base == _targetType) return _drawerType;
                    }
                }
            }

            return null;
        }
    }
}
