using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarryTools.DomainReload;
using UnityEditor;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Light path: reads guid, id, scene and hierarchy path of every SceneId from a scene file saved as text,
    /// without opening it. Component types stay unknown (null) until the next save or Rebuild.
    /// Objects inside prefab instances get an approximate path (the instance parent plus the object name).
    /// </summary>
    internal static class SceneYamlReader
    {
        // ==================== VARIABLES ====================

        private const int TransformClass = 4;
        private const int MonoBehaviourClass = 114;
        private const int RectTransformClass = 224;
        private const int PrefabInstanceClass = 1001;

        private static readonly Regex headerRegex = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Compiled);
        private static readonly Regex fileIdRegex = new Regex(@"fileID: (-?\d+)", RegexOptions.Compiled);
        private static readonly Regex guidRegex = new Regex(@"guid: ([0-9a-fA-F]{32})", RegexOptions.Compiled);

        [ClearOnReload] private static string sceneIdScriptGuid;

        // ==================== TYPES ====================

        private sealed class Block
        {
            public int ClassId;
            public long FileId;
            public bool Stripped;
            public readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> Lines = new List<string>();
        }

        // ==================== PUBLIC API ====================

        /// <summary>Entries of the scene file. Empty when the file is missing or not text.</summary>
        public static List<SceneIdIndexEntry> FromFile(string _scenePath)
        {
            TryFromFile(_scenePath, out List<SceneIdIndexEntry> _entries);
            return _entries;
        }

        /// <summary>False when the file is missing or not serialized as text.</summary>
        internal static bool TryFromFile(string _scenePath, out List<SceneIdIndexEntry> _entries)
        {
            _entries = new List<SceneIdIndexEntry>();
            if (string.IsNullOrEmpty(_scenePath) || !File.Exists(_scenePath)) return false;

            string[] _lines = File.ReadAllLines(_scenePath);
            if (_lines.Length == 0 || !_lines[0].StartsWith("%YAML", StringComparison.Ordinal)) return false;

            string _scriptGuid = SceneIdScriptGuid();
            if (string.IsNullOrEmpty(_scriptGuid)) return false;

            Dictionary<long, Block> _blocks = Parse(_lines);
            Dictionary<long, Block> _transformByGameObject = new Dictionary<long, Block>();
            foreach (Block _block in _blocks.Values)
            {
                if (_block.ClassId != TransformClass && _block.ClassId != RectTransformClass) continue;
                long _go = FileId(_block, "m_GameObject");
                if (_go != 0) _transformByGameObject[_go] = _block;
            }

            string _sceneGuid = AssetDatabase.AssetPathToGUID(_scenePath);
            string _sceneName = Path.GetFileNameWithoutExtension(_scenePath);
            foreach (Block _block in _blocks.Values)
            {
                if (_block.ClassId != MonoBehaviourClass || _block.Stripped) continue;
                if (!_block.Fields.TryGetValue("m_Script", out string _script)) continue;
                Match _match = guidRegex.Match(_script);
                if (!_match.Success || !string.Equals(_match.Groups[1].Value, _scriptGuid, StringComparison.OrdinalIgnoreCase)) continue;

                string _guid = Value(_block, "guid");
                if (string.IsNullOrEmpty(_guid)) continue;

                _entries.Add(new SceneIdIndexEntry
                {
                    Guid = _guid,
                    Id = Value(_block, "id"),
                    SceneGuid = _sceneGuid,
                    SceneName = _sceneName,
                    HierarchyPath = HierarchyPath(FileId(_block, "m_GameObject"), _blocks, _transformByGameObject),
                    ComponentTypes = null,
                });
            }

            return true;
        }

        // ==================== PARSING ====================

        private static Dictionary<long, Block> Parse(string[] _lines)
        {
            Dictionary<long, Block> _blocks = new Dictionary<long, Block>();
            Block _current = null;
            for (int _i = 0; _i < _lines.Length; _i++)
            {
                string _line = _lines[_i];
                if (_line.StartsWith("--- ", StringComparison.Ordinal))
                {
                    _current = null;
                    Match _header = headerRegex.Match(_line);
                    if (!_header.Success) continue;

                    _current = new Block
                    {
                        ClassId = int.Parse(_header.Groups[1].Value),
                        FileId = long.Parse(_header.Groups[2].Value),
                        Stripped = _header.Groups[3].Success,
                    };
                    _blocks[_current.FileId] = _current;
                    continue;
                }

                if (_current == null) continue;
                _current.Lines.Add(_line);

                // Top level fields of the object are indented by two spaces.
                if (_line.Length < 3 || _line[0] != ' ' || _line[1] != ' ' || _line[2] == ' ' || _line[2] == '-') continue;
                int _colon = _line.IndexOf(": ", 2, StringComparison.Ordinal);
                if (_colon < 0)
                {
                    if (_line.EndsWith(":", StringComparison.Ordinal)) _current.Fields[_line.Substring(2, _line.Length - 3)] = string.Empty;
                    continue;
                }

                _current.Fields[_line.Substring(2, _colon - 2)] = _line.Substring(_colon + 2);
            }

            return _blocks;
        }

        private static long FileId(Block _block, string _field)
        {
            if (_block == null || !_block.Fields.TryGetValue(_field, out string _raw)) return 0;
            Match _match = fileIdRegex.Match(_raw);
            return _match.Success ? long.Parse(_match.Groups[1].Value) : 0;
        }

        private static string Value(Block _block, string _field) =>
            _block.Fields.TryGetValue(_field, out string _raw) ? Unquote(_raw) : string.Empty;

        private static string Unquote(string _raw)
        {
            _raw = _raw.Trim();
            if (_raw.Length >= 2 && _raw[0] == '\'' && _raw[_raw.Length - 1] == '\'')
                return _raw.Substring(1, _raw.Length - 2).Replace("''", "'");

            if (_raw.Length >= 2 && _raw[0] == '"' && _raw[_raw.Length - 1] == '"')
            {
                string _inner = _raw.Substring(1, _raw.Length - 2);
                StringBuilder _builder = new StringBuilder(_inner.Length);
                for (int _i = 0; _i < _inner.Length; _i++)
                {
                    char _c = _inner[_i];
                    if (_c != '\\' || _i + 1 >= _inner.Length)
                    {
                        _builder.Append(_c);
                        continue;
                    }

                    char _next = _inner[++_i];
                    if (_next == 'u' && _i + 4 < _inner.Length)
                    {
                        _builder.Append((char)Convert.ToInt32(_inner.Substring(_i + 1, 4), 16));
                        _i += 4;
                    }
                    else _builder.Append(_next == 'n' ? '\n' : _next == 't' ? '\t' : _next);
                }

                return _builder.ToString();
            }

            return _raw;
        }

        // ==================== HIERARCHY ====================

        private static string HierarchyPath(long _gameObjectId, Dictionary<long, Block> _blocks, Dictionary<long, Block> _transformByGameObject)
        {
            List<string> _names = new List<string>();
            long _goId = _gameObjectId;
            // Bounded walk so a malformed file can never loop forever.
            for (int _guard = 0; _goId != 0 && _guard < 1000; _guard++)
            {
                _blocks.TryGetValue(_goId, out Block _go);
                if (_go == null) break;

                if (_go.Stripped)
                {
                    // Object inside a prefab instance: its name only lives in the instance modifications or in the prefab.
                    Block _instance = _blocks.TryGetValue(FileId(_go, "m_PrefabInstance"), out Block _found) ? _found : null;
                    _names.Add(StrippedName(_go, _instance));
                    _goId = InstanceParentGameObject(_instance, _blocks);
                    continue;
                }

                _names.Add(Value(_go, "m_Name"));
                if (!_transformByGameObject.TryGetValue(_goId, out Block _transform)) break;

                _blocks.TryGetValue(FileId(_transform, "m_Father"), out Block _father);
                _goId = GameObjectOfTransform(_father, _blocks);
            }

            _names.Reverse();
            return string.Join("/", _names);
        }

        private static long GameObjectOfTransform(Block _transform, Dictionary<long, Block> _blocks)
        {
            if (_transform == null) return 0;
            if (!_transform.Stripped) return FileId(_transform, "m_GameObject");

            // Stripped transform of a prefab instance: the levels inside the prefab are not in the file, continue from the instance parent.
            _blocks.TryGetValue(FileId(_transform, "m_PrefabInstance"), out Block _instance);
            return InstanceParentGameObject(_instance, _blocks);
        }

        private static long InstanceParentGameObject(Block _instance, Dictionary<long, Block> _blocks)
        {
            if (_instance == null || _instance.ClassId != PrefabInstanceClass) return 0;
            for (int _i = 0; _i < _instance.Lines.Count; _i++)
            {
                string _line = _instance.Lines[_i].Trim();
                if (!_line.StartsWith("m_TransformParent:", StringComparison.Ordinal)) continue;

                Match _match = fileIdRegex.Match(_line);
                if (!_match.Success) return 0;
                _blocks.TryGetValue(long.Parse(_match.Groups[1].Value), out Block _parent);
                return GameObjectOfTransform(_parent, _blocks);
            }

            return 0;
        }

        private static string StrippedName(Block _go, Block _instance)
        {
            if (_instance == null || !_go.Fields.TryGetValue("m_CorrespondingSourceObject", out string _source)) return "?";
            Match _sourceId = fileIdRegex.Match(_source);
            if (!_sourceId.Success) return "?";

            string _target = "fileID: " + _sourceId.Groups[1].Value + ",";
            for (int _i = 0; _i < _instance.Lines.Count; _i++)
            {
                string _line = _instance.Lines[_i].Trim();
                if (!_line.StartsWith("- target:", StringComparison.Ordinal) || !_line.Contains(_target)) continue;
                if (_i + 2 >= _instance.Lines.Count) break;

                string _property = _instance.Lines[_i + 1].Trim();
                string _value = _instance.Lines[_i + 2].Trim();
                if (_property == "propertyPath: m_Name" && _value.StartsWith("value:", StringComparison.Ordinal))
                    return Unquote(_value.Substring("value:".Length));
            }

            return "?";
        }

        // ==================== SCRIPT GUID ====================

        private static string SceneIdScriptGuid()
        {
            if (!string.IsNullOrEmpty(sceneIdScriptGuid)) return sceneIdScriptGuid;

            MonoScript[] _scripts = MonoImporter.GetAllRuntimeMonoScripts();
            for (int _i = 0; _i < _scripts.Length; _i++)
            {
                if (_scripts[_i] == null || _scripts[_i].GetClass() != typeof(SceneId)) continue;
                sceneIdScriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_scripts[_i]));
                break;
            }

            return sceneIdScriptGuid;
        }
    }
}
