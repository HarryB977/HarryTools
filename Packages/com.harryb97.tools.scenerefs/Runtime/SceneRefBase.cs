using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = HarryTools.LoggerTool.Logger;
using UnityEngine;

namespace HarryTools.SceneRefs
{
    /// <summary>
    /// Non generic part of <see cref="SceneRef{T}"/>. The serialized field names are used by the drawer and the synchronizer.
    /// </summary>
    [Serializable]
    public abstract class SceneRefBase
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";

        private static readonly List<Component> componentBuffer = new List<Component>();
        // The loaded assemblies only change with a domain reload, which also resets this cache.
        private static readonly Dictionary<string, Type> typeByFullName = new Dictionary<string, Type>(StringComparer.Ordinal);

        [SerializeField] internal UnityEngine.Object direct;
        [SerializeField] internal string guid;
        [SerializeField] internal string componentType;
        [SerializeField] internal int componentIndex;
        [SerializeField] internal string label;

        [NonSerialized] private UnityEngine.Object cached;
        [NonSerialized] private SceneId cachedOwner;
        [NonSerialized] private Type resolvedType;
        [NonSerialized] private string resolvedTypeName;
        [NonSerialized] private int warnedVersion;
        [NonSerialized] private bool invalidTypeReported;

        // ==================== PROPERTIES ====================

        /// <summary>True when the field holds a live direct reference or a guid (even if its scene is not loaded).</summary>
        public bool IsAssigned => direct != null || !string.IsNullOrEmpty(guid);

        /// <summary>Internal guid of the referenced object (empty when only a direct reference is set).</summary>
        public string Guid => guid;

        internal abstract Type TargetType { get; }

        // ==================== METHODS ====================

        /// <summary>Tests only. Editor code writes these fields through SerializedProperty.</summary>
        internal void SetForTests(UnityEngine.Object _direct, string _guid, string _componentType, int _componentIndex, string _label)
        {
            direct = _direct;
            guid = _guid;
            componentType = _componentType;
            componentIndex = _componentIndex;
            label = _label;
            cached = null;
            cachedOwner = null;
            resolvedType = null;
            resolvedTypeName = null;
        }

        #region Resolve

        /// <summary>Resolves the target as <see cref="UnityEngine.Object"/>; null when it is empty, not loaded or missing.</summary>
        internal UnityEngine.Object ResolveObject()
        {
            Type _target = TargetType;
            if (_target != typeof(GameObject) && !typeof(Component).IsAssignableFrom(_target))
            {
                if (!invalidTypeReported)
                {
                    invalidTypeReported = true;
                    Logger.LogError(Category, $"SceneRef<{_target.Name}> is not supported. T must be GameObject or a Component.");
                }
                return null;
            }

            if (direct != null) return direct;
            if (string.IsNullOrEmpty(guid)) return null;

            if (cached != null && cachedOwner != null) return cached;
            cached = null;
            cachedOwner = null;

            if (!SceneRefs.TryGetByGuid(guid, out SceneId _owner))
            {
                WarnOnce($"SceneRef '{guid}' points to an object that is not loaded{LastKnown()}.");
                return null;
            }

            UnityEngine.Object _result = _target == typeof(GameObject) ? _owner.gameObject : FindComponent(_owner.gameObject, _target);
            if (_result == null) return null;

            cached = _result;
            cachedOwner = _owner;
            return _result;
        }

        private Component FindComponent(GameObject _gameObject, Type _fallbackType)
        {
            Type _type = ResolveComponentType(_fallbackType);
            if (_type == null)
            {
                WarnOnce($"SceneRef '{guid}' has a component type that cannot be resolved ('{componentType}'){LastKnown()}.");
                return null;
            }
            _gameObject.GetComponents(_type, componentBuffer);

            // The editor stores the index among components of the exact concrete type, so subclasses are not counted.
            // Without a stored type (hand-built references) every component assignable to T counts.
            bool _exact = !string.IsNullOrEmpty(componentType);
            Component _component = null;
            int _count = 0;
            for (int _i = 0; _i < componentBuffer.Count; _i++)
            {
                Component _candidate = componentBuffer[_i];
                if (_candidate == null || (_exact && _candidate.GetType() != _type)) continue;
                if (_count == componentIndex) _component = _candidate;
                _count++;
            }
            componentBuffer.Clear();

            if (_component == null || !_fallbackType.IsInstanceOfType(_component))
            {
                WarnOnce($"SceneRef '{guid}' found its object but not component {_type.Name} #{componentIndex} (it has {_count}){LastKnown()}.");
                return null;
            }
            return _component;
        }

        private Type ResolveComponentType(Type _fallbackType)
        {
            if (string.IsNullOrEmpty(componentType)) return _fallbackType;
            if (resolvedType != null && resolvedTypeName == componentType) return resolvedType;

            Type _type = Type.GetType(componentType) ?? FindByFullName(componentType);
            if (_type == null || !typeof(Component).IsAssignableFrom(_type)) return null;

            resolvedType = _type;
            resolvedTypeName = componentType;
            return _type;
        }

        /// <summary>
        /// Fallback for "Full.Name, Assembly" when the script moved to another assembly: the first loaded type with that full name.
        /// Results are cached per stored name, misses included.
        /// </summary>
        private static Type FindByFullName(string _storedName)
        {
            if (typeByFullName.TryGetValue(_storedName, out Type _cached)) return _cached;

            int _comma = _storedName.IndexOf(',');
            string _fullName = (_comma >= 0 ? _storedName.Substring(0, _comma) : _storedName).Trim();
            Type _found = null;
            foreach (Assembly _assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _found = _assembly.GetType(_fullName, false);
                if (_found != null) break;
            }

            typeByFullName[_storedName] = _found;
            return _found;
        }

        private string LastKnown() => string.IsNullOrEmpty(label) ? string.Empty : $" (last known as {label})";

        private void WarnOnce(string _message)
        {
            int _version = SceneRefs.LoadVersion;
            if (warnedVersion == _version) return;
            warnedVersion = _version;
            Logger.LogWarning(Category, _message);
        }

        #endregion
    }
}
