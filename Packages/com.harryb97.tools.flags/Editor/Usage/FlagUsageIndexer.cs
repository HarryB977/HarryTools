using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Reads the FlagRefs of a loaded scene or an asset exactly (from memory, with path, component and kind), or of a scene
    /// file approximately (a text search for known flag guids).
    /// </summary>
    internal static class FlagUsageIndexer
    {
        // ==================== VARIABLES ====================

        // How a FlagRef with a guid looks in a text-serialized file.
        // YAML document header of a PrefabInstance (class id 1001).
        private const string PrefabInstanceHeader = "--- !u!1001 ";

        private static readonly Regex FlagRefText = new Regex(@"\bguid: [0-9a-fA-F]{32}\r?\n\s*label:", RegexOptions.Compiled);

        // ==================== PUBLIC API ====================

        /// <summary>Every FlagRef of a loaded, saved scene, read from memory (unsaved changes included).</summary>
        public static List<FlagUsageEntry> FromLoadedScene(Scene _scene)
        {
            List<FlagUsageEntry> _result = new List<FlagUsageEntry>();
            if (!_scene.IsValid() || !_scene.isLoaded || EditorSceneManager.IsPreviewScene(_scene) || string.IsNullOrEmpty(_scene.path)) return _result;

            string _sceneGuid = AssetDatabase.AssetPathToGUID(_scene.path);
            if (string.IsNullOrEmpty(_sceneGuid)) return _result;

            GameObject[] _roots = _scene.GetRootGameObjects();
            for (int _i = 0; _i < _roots.Length; _i++) FromHierarchy(_roots[_i], _result);

            for (int _i = 0; _i < _result.Count; _i++) _result[_i].sceneGuid = _sceneGuid;
            return _result;
        }

        /// <summary>Every FlagRef of a prefab asset or of the ScriptableObjects of an asset file.</summary>
        public static List<FlagUsageEntry> FromAsset(string _assetPath)
        {
            List<FlagUsageEntry> _result = new List<FlagUsageEntry>();
            if (string.IsNullOrEmpty(_assetPath)) return _result;

            Type _mainType = AssetDatabase.GetMainAssetTypeAtPath(_assetPath);
            if (_mainType == null) return _result;

            if (_mainType == typeof(GameObject))
            {
                // Model files also load as GameObjects; only prefab assets can hold scripts.
                if (!_assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return _result;
                GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
                if (_root != null) FromHierarchy(_root, _result);
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(_mainType))
            {
                Object[] _objects = AssetDatabase.LoadAllAssetsAtPath(_assetPath);
                for (int _i = 0; _i < _objects.Length; _i++)
                    if (_objects[_i] is ScriptableObject _scriptable) FromObject(_scriptable, _scriptable.name, _result);
            }

            for (int _i = 0; _i < _result.Count; _i++) _result[_i].assetPath = _assetPath;
            return _result;
        }

        /// <summary>
        /// Light path for scene files that changed while closed: one approximate <see cref="FlagUsageKind.Other"/> entry per known
        /// flag guid that appears in the file, without path or component.
        /// </summary>
        public static List<FlagUsageEntry> FromSceneYaml(string _scenePath, ICollection<string> _knownFlagGuids)
        {
            List<FlagUsageEntry> _result = new List<FlagUsageEntry>();
            if (string.IsNullOrEmpty(_scenePath) || _knownFlagGuids == null || !File.Exists(_scenePath)) return _result;

            string _sceneGuid = AssetDatabase.AssetPathToGUID(_scenePath);
            if (string.IsNullOrEmpty(_sceneGuid)) return _result;

            string _text = File.ReadAllText(_scenePath);
            HashSet<string> _added = new HashSet<string>(StringComparer.Ordinal);
            foreach (string _guid in _knownFlagGuids)
            {
                if (string.IsNullOrEmpty(_guid) || !_added.Add(_guid) || _text.IndexOf(_guid, StringComparison.Ordinal) < 0) continue;
                _result.Add(new FlagUsageEntry { flagGuid = _guid, kind = FlagUsageKind.Other, sceneGuid = _sceneGuid, approximate = true });
            }
            return _result;
        }

        /// <summary>
        /// Kind of use of a FlagRef field: a <see cref="FlagUsageAttribute"/> on the field wins; otherwise fields declared by
        /// <see cref="FlagListener"/> read; anything else is <see cref="FlagUsageKind.Other"/>.
        /// </summary>
        public static FlagUsageKind KindOf(Type _owner, FieldInfo _field)
        {
            FlagUsageAttribute _attribute = _field?.GetCustomAttribute<FlagUsageAttribute>(true);
            if (_attribute != null) return _attribute.Kind;
            if (_owner != null && typeof(FlagListener).IsAssignableFrom(_owner)) return FlagUsageKind.Read;
            return FlagUsageKind.Other;
        }

        /// <summary>
        /// False only when the file is text-serialized, holds no assigned FlagRef and has no prefab instance, so it does not need
        /// to be loaded. Binary files always may; so does a file with a prefab instance or variant base, whose FlagRefs live in
        /// the prefab and are not written to this file unless overridden.
        /// </summary>
        public static bool MayContainFlagRef(string _path)
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return false;

            string _text = File.ReadAllText(_path);
            if (!_text.StartsWith("%YAML", StringComparison.Ordinal)) return true;
            return _text.IndexOf(PrefabInstanceHeader, StringComparison.Ordinal) >= 0 || FlagRefText.IsMatch(_text);
        }

        // ==================== PRIVATE ====================

        private static void FromHierarchy(GameObject _root, List<FlagUsageEntry> _result)
        {
            MonoBehaviour[] _components = _root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int _i = 0; _i < _components.Length; _i++)
            {
                // Missing scripts come back as null.
                MonoBehaviour _component = _components[_i];
                if (_component == null || !FlagRefTypeCache.CanHold(_component.GetType())) continue;
                FromObject(_component, HierarchyPath(_component.transform), _result);
            }
        }

        private static void FromObject(Object _target, string _path, List<FlagUsageEntry> _result)
        {
            Type _type = _target.GetType();
            if (!FlagRefTypeCache.CanHold(_type)) return;

            using (SerializedObject _so = new SerializedObject(_target))
            {
                List<FlagRefMatch> _matches = FlagRefPropertyWalker.FindAll(_so);
                for (int _i = 0; _i < _matches.Count; _i++)
                {
                    string _guid = _matches[_i].Guid;
                    if (string.IsNullOrEmpty(_guid)) continue;

                    FieldInfo _field = _matches[_i].Field;
                    _result.Add(new FlagUsageEntry
                    {
                        flagGuid = _guid,
                        kind = KindOf(_field?.DeclaringType, _field),
                        hierarchyPath = _path,
                        componentType = _type.FullName,
                    });
                }
            }
        }

        private static string HierarchyPath(Transform _transform)
        {
            StringBuilder _builder = new StringBuilder();
            for (Transform _current = _transform; _current != null; _current = _current.parent)
            {
                if (_builder.Length > 0) _builder.Insert(0, '/');
                _builder.Insert(0, _current.name);
            }
            return _builder.ToString();
        }
    }
}
