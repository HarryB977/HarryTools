using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Writes a SceneRef from a dropped object, an index entry or nothing. No GUI, so the drawer, the picker and the tests share it.
    /// </summary>
    internal static class SceneRefAssign
    {
        // ==================== VARIABLES ====================

        // FullName to type, per requested base type. TypeCache does not change within a domain.
        [ClearOnReload(AssignNewInstance = true)] private static Dictionary<Type, Dictionary<string, Type>> typesByName = new Dictionary<Type, Dictionary<string, Type>>();

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Assigns a dropped GameObject or component. A GameObject gives its T, a component gives its GameObject when T is GameObject.
        /// Inside one prefab (asset, Prefab Mode or one prefab instance of a scene) only the direct reference is stored. Scene objects get a SceneId if needed and
        /// their guid; the direct reference is kept only when owner and target share a scene. Assets and objects without T are rejected.
        /// </summary>
        public static bool TryAssign(SerializedProperty _ref, Object _dropped, Type _targetType)
        {
            if (!Resolve(_ref, _dropped, _targetType, out Object _target, out bool _directOnly)) return false;

            if (_directOnly)
            {
                _ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue = _target;
                WriteIdFields(_ref, string.Empty, string.Empty, 0, string.Empty);
                _ref.serializedObject.ApplyModifiedProperties();
                return true;
            }

            // The target is passed in, not written to the direct field first: an asset owner refuses scene objects there,
            // so reading it back would find nothing. The sync sets the direct field only when owner and target share a scene.
            SceneRefSync.SyncProperty(_ref, _target);
            _ref.serializedObject.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// Assigns an index entry. A loaded object goes through <see cref="TryAssign"/>; an object of a closed scene stores guid,
        /// component and label only. The component is the given position among components of the entry's concrete type for T.
        /// </summary>
        public static void AssignFromIndex(SerializedProperty _ref, SceneIdIndexEntry _entry, int _componentIndex, Type _targetType)
        {
            if (_ref == null || _targetType == null || string.IsNullOrEmpty(_entry.Guid)) return;

            Type _concrete = _targetType == typeof(GameObject) ? null : ConcreteType(_entry, _targetType);
            Object _live = FindLoaded(_entry.Guid, _concrete ?? _targetType, _componentIndex);
            if (_live != null && TryAssign(_ref, _live, _targetType)) return;

            _ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue = null;
            WriteIdFields(_ref, _entry.Guid, _concrete != null ? SceneRefSync.TypeName(_concrete) : string.Empty, _componentIndex, $"{_entry.Id} in {_entry.SceneName}");
            _ref.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Empties the reference.</summary>
        public static void Clear(SerializedProperty _ref)
        {
            if (_ref == null) return;
            _ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue = null;
            WriteIdFields(_ref, string.Empty, string.Empty, 0, string.Empty);
            _ref.serializedObject.ApplyModifiedProperties();
        }

        // ==================== INTERNAL ====================

        /// <summary>True when <see cref="TryAssign"/> would accept the object. Writes nothing.</summary>
        internal static bool CanAssign(SerializedProperty _ref, Object _dropped, Type _targetType) =>
            Resolve(_ref, _dropped, _targetType, out _, out _);

        /// <summary>The loaded object with that guid: its GameObject, or its component of the exact type at that position. Null when not loaded.</summary>
        internal static Object FindLoaded(string _guid, Type _type, int _componentIndex)
        {
            if (string.IsNullOrEmpty(_guid) || _type == null) return null;

            foreach (SceneId _id in SceneRefSync.LoadedSceneIds())
            {
                if (_id.Guid != _guid) continue;
                if (_type == typeof(GameObject)) return _id.gameObject;
                if (!typeof(Component).IsAssignableFrom(_type)) return null;

                Component[] _components = _id.GetComponents(_type);
                int _count = 0;
                for (int _i = 0; _i < _components.Length; _i++)
                {
                    if (_components[_i] == null || _components[_i].GetType() != _type) continue;
                    if (_count == _componentIndex) return _components[_i];
                    _count++;
                }
                return null;
            }
            return null;
        }

        /// <summary>True when the entry can hold a T: always for GameObject and for entries with unknown component types.</summary>
        internal static bool Matches(SceneIdIndexEntry _entry, Type _targetType) =>
            _targetType == typeof(GameObject) || _entry.ComponentTypes == null || ConcreteType(_entry, _targetType) != null;

        /// <summary>
        /// The component type of the entry used for T: T itself when listed, else the first listed type assignable to T.
        /// Null for unknown component types or when none fits.
        /// </summary>
        internal static Type ConcreteType(SceneIdIndexEntry _entry, Type _targetType)
        {
            string[] _types = _entry.ComponentTypes;
            if (_types == null || _targetType == null || !typeof(Component).IsAssignableFrom(_targetType)) return null;

            for (int _i = 0; _i < _types.Length; _i++)
                if (_types[_i] == _targetType.FullName) return _targetType;

            Dictionary<string, Type> _byName = TypesAssignableTo(_targetType);
            for (int _i = 0; _i < _types.Length; _i++)
                if (_types[_i] != null && _byName.TryGetValue(_types[_i], out Type _type)) return _type;
            return null;
        }

        // ==================== PRIVATE ====================

        /// <summary>
        /// The object to store for T and whether it is a prefab internal reference (direct only).
        /// False for objects without T, assets outside the owner's prefab and objects of other preview scenes.
        /// </summary>
        private static bool Resolve(SerializedProperty _ref, Object _dropped, Type _targetType, out Object _target, out bool _directOnly)
        {
            _directOnly = false;
            _target = _ref != null && _targetType != null ? Convert(_dropped, _targetType) : null;
            if (_target == null) return false;

            GameObject _go = GameObjectOf(_target);
            Object _owner = _ref.serializedObject.targetObject;
            _directOnly = InSamePrefab(_owner, _go) || (SceneRefSync.IsSceneObject(_go) && SceneRefSync.InSamePrefabInstance(_owner, _go));
            return _directOnly || SceneRefSync.IsSceneObject(_go);
        }

        /// <summary>The object to store for T, or null when the dropped object is not a GameObject or component or lacks T.</summary>
        private static Object Convert(Object _dropped, Type _targetType)
        {
            GameObject _go = GameObjectOf(_dropped);
            if (_go == null) return null;
            if (_targetType == typeof(GameObject)) return _go;
            if (!typeof(Component).IsAssignableFrom(_targetType)) return null;
            if (_targetType.IsInstanceOfType(_dropped)) return _dropped;
            return _go.GetComponent(_targetType);
        }

        private static GameObject GameObjectOf(Object _object)
        {
            if (_object is GameObject _go) return _go;
            if (_object is Component _component) return _component.gameObject;
            return null;
        }

        /// <summary>Owner and target inside the same prefab asset, or the same Prefab Mode or other preview scene.</summary>
        private static bool InSamePrefab(Object _owner, GameObject _target)
        {
            GameObject _ownerGo = GameObjectOf(_owner);
            if (_ownerGo == null || _target == null) return false;

            if (EditorUtility.IsPersistent(_ownerGo) || EditorUtility.IsPersistent(_target))
            {
                string _path = AssetDatabase.GetAssetPath(_target);
                return !string.IsNullOrEmpty(_path) && _path == AssetDatabase.GetAssetPath(_ownerGo);
            }

            bool _preview = EditorSceneManager.IsPreviewScene(_target.scene) || PrefabStageUtility.GetPrefabStage(_target) != null;
            return _preview && _ownerGo.scene == _target.scene;
        }

        private static Dictionary<string, Type> TypesAssignableTo(Type _baseType)
        {
            if (typesByName.TryGetValue(_baseType, out Dictionary<string, Type> _byName)) return _byName;

            _byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type _type in TypeCache.GetTypesDerivedFrom(_baseType))
                if (!_type.IsAbstract && !_type.IsGenericTypeDefinition && _type.FullName != null) _byName[_type.FullName] = _type;
            if (!_baseType.IsAbstract) _byName[_baseType.FullName] = _baseType;

            typesByName[_baseType] = _byName;
            return _byName;
        }

        private static void WriteIdFields(SerializedProperty _ref, string _guid, string _componentType, int _componentIndex, string _label)
        {
            _ref.FindPropertyRelative(SceneRefPropertyWalker.GuidField).stringValue = _guid;
            _ref.FindPropertyRelative(SceneRefPropertyWalker.ComponentTypeField).stringValue = _componentType;
            _ref.FindPropertyRelative(SceneRefPropertyWalker.ComponentIndexField).intValue = _componentIndex;
            _ref.FindPropertyRelative(SceneRefPropertyWalker.LabelField).stringValue = _label;
        }
    }
}
