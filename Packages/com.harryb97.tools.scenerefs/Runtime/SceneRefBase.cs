using System;
using System.Collections.Generic;
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
            _gameObject.GetComponents(_type, componentBuffer);

            Component _component = componentIndex >= 0 && componentIndex < componentBuffer.Count ? componentBuffer[componentIndex] : null;
            int _count = componentBuffer.Count;
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

            Type _type = Type.GetType(componentType);
            if (_type == null || !typeof(Component).IsAssignableFrom(_type)) return _fallbackType;

            resolvedType = _type;
            resolvedTypeName = componentType;
            return _type;
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
