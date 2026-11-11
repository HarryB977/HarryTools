using System.Linq;
using UnityEditor;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Every edit of a <see cref="FlagDatabase"/>: creation, rename, deletion and value changes, with Undo and sorting.
    /// </summary>
    internal static class FlagDatabaseEditing
    {
        // ==================== CREATE / RENAME / DELETE ====================

        public static bool TryCreate(FlagDatabase _database, FlagScope _scope, string _sceneGuid, string _name, out FlagDefinition _created, out string _error)
        {
            _created = null;
            string _scopeSceneGuid = _scope == FlagScope.Local ? _sceneGuid : string.Empty;

            if (!ValidateName(_database, _scope, _scopeSceneGuid, _name, null, out _error)) return false;
            if (_scope == FlagScope.Local && string.IsNullOrEmpty(_scopeSceneGuid))
            {
                _error = "A local flag needs a scene.";
                return false;
            }

            Undo.RecordObject(_database, "Create Flag");
            _created = new FlagDefinition(FlagNaming.NewGuid(), _name, _scope, _scopeSceneGuid, SceneNameOf(_scopeSceneGuid, string.Empty));
            _database.MutableFlags.Add(_created);
            Commit(_database);
            return true;
        }

        public static bool TryRename(FlagDatabase _database, string _guid, string _newName, out string _error)
        {
            if (!_database.TryGet(_guid, out FlagDefinition _flag))
            {
                _error = "Flag not found.";
                return false;
            }
            if (!ValidateName(_database, _flag.Scope, _flag.SceneGuid, _newName, _guid, out _error)) return false;

            Undo.RecordObject(_database, "Rename Flag");
            _flag.SetName(_newName);
            Commit(_database);
            return true;
        }

        public static bool Delete(FlagDatabase _database, string _guid)
        {
            if (!_database.TryGet(_guid, out FlagDefinition _flag)) return false;

            Undo.RecordObject(_database, "Delete Flag");
            _database.MutableFlags.Remove(_flag);
            Commit(_database);
            return true;
        }

        // ==================== VALUES ====================

        public static void SetInitialValue(FlagDatabase _database, string _guid, bool _value)
        {
            if (!_database.TryGet(_guid, out FlagDefinition _flag)) return;

            Undo.RecordObject(_database, "Set Flag Initial Value");
            _flag.SetInitialValue(_value);
            Commit(_database);
        }

        public static void SetDescription(FlagDatabase _database, string _guid, string _description)
        {
            if (!_database.TryGet(_guid, out FlagDefinition _flag)) return;

            Undo.RecordObject(_database, "Set Flag Description");
            _flag.SetDescription(_description);
            Commit(_database);
        }

        /// <summary>Reassigns a local flag to another scene, for flags whose scene went missing.</summary>
        public static bool TryMoveToScene(FlagDatabase _database, string _guid, string _sceneGuid, out string _error)
        {
            if (!_database.TryGet(_guid, out FlagDefinition _flag))
            {
                _error = "Flag not found.";
                return false;
            }
            if (_flag.Scope != FlagScope.Local)
            {
                _error = "Only local flags belong to a scene.";
                return false;
            }
            if (string.IsNullOrEmpty(_sceneGuid))
            {
                _error = "A local flag needs a scene.";
                return false;
            }
            if (IsNameTaken(_database, FlagScope.Local, _sceneGuid, _flag.Name, _guid))
            {
                _error = $"The scene already has a flag named '{_flag.Name}'.";
                return false;
            }

            Undo.RecordObject(_database, "Move Flag To Scene");
            _flag.SetSceneGuid(_sceneGuid);
            _flag.SetSceneName(SceneNameOf(_sceneGuid, _flag.SceneName));
            Commit(_database);
            _error = null;
            return true;
        }

        // ==================== QUERIES ====================

        public static bool IsNameTaken(FlagDatabase _database, FlagScope _scope, string _sceneGuid, string _name, string _exceptGuid)
        {
            foreach (FlagDefinition _flag in _database.Flags)
            {
                if (_flag.Guid == _exceptGuid || _flag.Scope != _scope) continue;
                if (_scope == FlagScope.Local && _flag.SceneGuid != _sceneGuid) continue;
                if (FlagNaming.Comparer.Equals(_flag.Name, _name)) return true;
            }
            return false;
        }

        // ==================== MAINTENANCE ====================

        /// <summary>Updates the cached scene names from the project. Scenes that no longer resolve keep their last known name.</summary>
        public static void RefreshSceneNames(FlagDatabase _database)
        {
            bool _changed = false;
            foreach (FlagDefinition _flag in _database.Flags)
            {
                if (_flag.Scope != FlagScope.Local) continue;
                string _name = SceneNameOf(_flag.SceneGuid, _flag.SceneName);
                if (_name == _flag.SceneName) continue;

                if (!_changed) Undo.RecordObject(_database, "Refresh Flag Scene Names");
                _flag.SetSceneName(_name);
                _changed = true;
            }
            if (_changed) Commit(_database);
        }

        /// <summary>Orders entries: global first, then by scene name, then by flag name.</summary>
        public static void Sort(FlagDatabase _database)
        {
            var _sorted = _database.MutableFlags
                .OrderBy(_flag => _flag.Scope)
                .ThenBy(_flag => _flag.Scope == FlagScope.Local ? _flag.SceneName : string.Empty, FlagNaming.Comparer)
                .ThenBy(_flag => _flag.Name, FlagNaming.Comparer)
                .ToList();
            _database.MutableFlags.Clear();
            _database.MutableFlags.AddRange(_sorted);
            _database.Invalidate();
        }

        // ==================== PRIVATE ====================

        private static bool ValidateName(FlagDatabase _database, FlagScope _scope, string _sceneGuid, string _name, string _exceptGuid, out string _error)
        {
            if (!FlagNaming.IsValidName(_name))
            {
                _error = "Flag names can only contain letters, digits and underscores.";
                return false;
            }
            if (IsNameTaken(_database, _scope, _sceneGuid, _name, _exceptGuid))
            {
                _error = $"A flag named '{_name}' already exists in this scope.";
                return false;
            }
            _error = null;
            return true;
        }

        private static string SceneNameOf(string _sceneGuid, string _fallback)
        {
            if (string.IsNullOrEmpty(_sceneGuid)) return _fallback;
            string _path = AssetDatabase.GUIDToAssetPath(_sceneGuid);
            return string.IsNullOrEmpty(_path) ? _fallback : System.IO.Path.GetFileNameWithoutExtension(_path);
        }

        private static void Commit(FlagDatabase _database)
        {
            Sort(_database);
            EditorUtility.SetDirty(_database);
        }
    }
}
