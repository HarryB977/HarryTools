using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Editor
{
    internal enum SceneIdConflictKind
    {
        DuplicateGuid,
        DuplicateId,
        IdInPrefabAsset,
    }

    /// <summary>
    /// A problem that blocks Play and builds: repeated guid, repeated id or a SceneId inside a prefab asset.
    /// </summary>
    internal sealed class SceneIdConflict
    {
        // ==================== VARIABLES ====================

        public SceneIdConflictKind Kind;

        /// <summary>The repeated guid or id, or the prefab path.</summary>
        public string Key;

        public List<SceneIdIndexEntry> Entries = new List<SceneIdIndexEntry>();

        public string PrefabPath;

        // ==================== PUBLIC API ====================

        public string ToReport()
        {
            if (Kind == SceneIdConflictKind.IdInPrefabAsset)
                return $"Scene id inside prefab asset '{PrefabPath}'. Prefabs cannot hold scene ids; remove the SceneId component from the asset.";

            StringBuilder _builder = new StringBuilder();
            _builder.Append(Kind == SceneIdConflictKind.DuplicateGuid ? "Duplicate scene id guid '" : "Duplicate scene id '");
            _builder.Append(Key).Append("' used by:");
            for (int _i = 0; _i < Entries.Count; _i++)
                _builder.Append("\n  ").Append(Entries[_i].SceneName).Append(" / ").Append(Entries[_i].HierarchyPath).Append(" (id '").Append(Entries[_i].Id).Append("')");
            return _builder.ToString();
        }

        /// <summary>The first object of this conflict that lives in a loaded scene, to select it from the console. Null when none is loaded.</summary>
        public Object FindContext()
        {
            List<SceneId> _ids = new List<SceneId>();
            for (int _i = 0; _i < Entries.Count; _i++)
            {
                SceneIdIndexEntry _entry = Entries[_i];
                for (int _s = 0; _s < SceneManager.sceneCount; _s++)
                {
                    Scene _scene = SceneManager.GetSceneAt(_s);
                    if (!_scene.isLoaded || EditorSceneManager.IsPreviewScene(_scene) || SceneIndexer.SceneKey(_scene) != _entry.SceneGuid) continue;

                    foreach (GameObject _root in _scene.GetRootGameObjects())
                    {
                        _root.GetComponentsInChildren(true, _ids);
                        for (int _j = 0; _j < _ids.Count; _j++)
                            if (_ids[_j].Guid == _entry.Guid && SceneIndexer.HierarchyPath(_ids[_j].transform) == _entry.HierarchyPath) return _ids[_j].gameObject;
                    }
                }
            }

            return null;
        }
    }
}
