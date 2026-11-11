using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>How a stored flag reference relates to the object that holds it.</summary>
    internal enum FlagRefState
    {
        Empty,
        Valid,
        Deleted,
        ForeignLocal
    }

    /// <summary>Decides which flags an object may reference: globals, plus the locals of the scene it lives in.</summary>
    internal static class FlagHostScope
    {
        // ==================== METHODS ====================

        /// <summary>
        /// Guid of the scene asset the host lives in. Null for assets (ScriptableObjects, prefab assets), objects in prefab mode,
        /// scenes that were never saved and missing hosts: those only see global flags.
        /// </summary>
        public static string SceneGuidOf(UnityEngine.Object _host)
        {
            GameObject _go = _host as GameObject;
            if (_go == null && _host is Component _component) _go = _component.gameObject;
            if (_go == null) return null;

            if (EditorUtility.IsPersistent(_go)) return null;
            if (PrefabStageUtility.GetPrefabStage(_go) != null) return null;

            string _path = _go.scene.path;
            if (string.IsNullOrEmpty(_path)) return null;
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            return string.IsNullOrEmpty(_guid) ? null : _guid;
        }

        /// <summary>
        /// Scene guid shared by all the hosts, taken from the first one. <paramref name="_mixed"/> is true when the hosts live in different scenes.
        /// </summary>
        public static string SceneGuidOf(UnityEngine.Object[] _hosts, out bool _mixed)
        {
            _mixed = false;
            if (_hosts == null || _hosts.Length == 0) return null;

            string _first = SceneGuidOf(_hosts[0]);
            for (int _i = 1; _i < _hosts.Length; _i++)
            {
                if (SceneGuidOf(_hosts[_i]) != _first)
                {
                    _mixed = true;
                    break;
                }
            }
            return _first;
        }

        /// <summary>Global flags followed by the locals of the host scene. Only globals when the host scene is null.</summary>
        public static IEnumerable<FlagDefinition> Selectable(FlagDatabase _database, string _hostSceneGuid)
        {
            if (_database == null) yield break;

            foreach (FlagDefinition _flag in _database.Flags)
                if (_flag != null && _flag.Scope == FlagScope.Global) yield return _flag;

            if (string.IsNullOrEmpty(_hostSceneGuid)) yield break;
            foreach (FlagDefinition _flag in _database.Flags)
                if (_flag != null && _flag.Scope == FlagScope.Local && _flag.SceneGuid == _hostSceneGuid) yield return _flag;
        }

        public static FlagRefState StateOf(FlagDatabase _database, string _guid, string _hostSceneGuid)
        {
            if (string.IsNullOrEmpty(_guid)) return FlagRefState.Empty;
            if (_database == null || !_database.TryGet(_guid, out FlagDefinition _flag)) return FlagRefState.Deleted;
            if (_flag.Scope == FlagScope.Global) return FlagRefState.Valid;

            bool _sameScene = !string.IsNullOrEmpty(_hostSceneGuid) && string.Equals(_flag.SceneGuid, _hostSceneGuid, StringComparison.Ordinal);
            return _sameScene ? FlagRefState.Valid : FlagRefState.ForeignLocal;
        }
    }
}
