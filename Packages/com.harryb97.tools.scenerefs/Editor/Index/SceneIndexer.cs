using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Exact path: reads the SceneIds of a loaded scene from memory, component types included.
    /// </summary>
    internal static class SceneIndexer
    {
        // ==================== VARIABLES ====================

        internal const string UnsavedPrefix = "unsaved:";

        private static readonly List<GameObject> rootBuffer = new List<GameObject>();
        private static readonly List<SceneId> idBuffer = new List<SceneId>();
        private static readonly List<Component> componentBuffer = new List<Component>();
        private static readonly StringBuilder pathBuilder = new StringBuilder();

        // ==================== PUBLIC API ====================

        public static List<SceneIdIndexEntry> FromLoadedScene(Scene _scene)
        {
            List<SceneIdIndexEntry> _result = new List<SceneIdIndexEntry>();
            if (!_scene.IsValid() || !_scene.isLoaded || EditorSceneManager.IsPreviewScene(_scene)) return _result;

            string _sceneGuid = SceneKey(_scene);
            _scene.GetRootGameObjects(rootBuffer);
            for (int _i = 0; _i < rootBuffer.Count; _i++)
            {
                rootBuffer[_i].GetComponentsInChildren(true, idBuffer);
                for (int _j = 0; _j < idBuffer.Count; _j++)
                {
                    SceneId _sceneId = idBuffer[_j];
                    if (string.IsNullOrEmpty(_sceneId.Guid)) continue;

                    _result.Add(new SceneIdIndexEntry
                    {
                        Guid = _sceneId.Guid,
                        Id = _sceneId.Id,
                        SceneGuid = _sceneGuid,
                        SceneName = _scene.name,
                        HierarchyPath = HierarchyPath(_sceneId.transform),
                        ComponentTypes = ComponentTypes(_sceneId.gameObject),
                    });
                }
            }

            rootBuffer.Clear();
            idBuffer.Clear();
            return _result;
        }

        /// <summary>Asset guid of the scene, or "unsaved:" + handle when it was never saved.</summary>
        internal static string SceneKey(Scene _scene) =>
            string.IsNullOrEmpty(_scene.path) ? UnsavedPrefix + _scene.handle : AssetDatabase.AssetPathToGUID(_scene.path);

        internal static string HierarchyPath(Transform _transform)
        {
            pathBuilder.Clear();
            for (Transform _current = _transform; _current != null; _current = _current.parent)
            {
                if (pathBuilder.Length > 0) pathBuilder.Insert(0, '/');
                pathBuilder.Insert(0, _current.name);
            }

            return pathBuilder.ToString();
        }

        // ==================== PRIVATE ====================

        private static string[] ComponentTypes(GameObject _gameObject)
        {
            _gameObject.GetComponents(componentBuffer);
            List<string> _types = new List<string>(componentBuffer.Count);
            for (int _i = 0; _i < componentBuffer.Count; _i++)
            {
                Component _component = componentBuffer[_i];
                // Missing scripts come back as null.
                if (_component == null || _component is Transform || _component is SceneId) continue;
                _types.Add(_component.GetType().FullName);
            }

            componentBuffer.Clear();
            return _types.ToArray();
        }
    }
}
